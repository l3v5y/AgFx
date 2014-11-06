// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using AgFx.HashedFileStore;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AgFx
{
    /// <summary>
    /// DataManager allows access to cached objects and manageds not only caching but reloads, etc.
    /// </summary>
    public class DataManager : NotifyPropertyChangedBase
    {
        /// <summary>
        /// Event handler for handling any errors not caught by a method level handler (e.g. Load(id, success, error).
        /// </summary>
        public event EventHandler<DataManagerUnhandledExceptionEventArgs> UnhandledError;

        private static readonly DataManager _current = new DataManager();

        /// <summary>
        /// Default singleton instance of DataManager
        /// </summary>
        public static DataManager Current
        {
            get
            {
                return _current;
            }
        }

        private int _loadingCount = 0;
        private static IStoreProvider _storeProvider;
        private static readonly AutoLoadContextCreator _loadContextCreator = new AutoLoadContextCreator();
        private static readonly Dictionary<Type, Dictionary<object, ICacheEntry>> _objectCache = new Dictionary<Type, Dictionary<object, ICacheEntry>>();

        private static readonly AsyncReaderWriterLock _objectCacheLock = new AsyncReaderWriterLock();

        internal static IStoreProvider StoreProvider
        {
            get
            {
                if (_storeProvider == null)
                {
                    _storeProvider = new HashedFileStoreProvider();
                }
                return _storeProvider;
            }
        }

        /// <summary>
        /// True when the DataManager is in the process of performing a load.
        /// </summary>
        public bool IsLoading
        {
            get
            {
                return _loadingCount > 0;
            }
            internal set
            {

                bool loading = IsLoading;
                if (value)
                {
                    Interlocked.Increment(ref _loadingCount);
                }
                else
                {
                    if (Interlocked.Decrement(ref _loadingCount) < 0)
                    {
                        //This shouldn't happen, but in case if it does we will increment counter back
                        Interlocked.Increment(ref _loadingCount);
                    }
                }

                if (loading != IsLoading)
                {
                    RaisePropertyChanged("IsLoading");
                }
            }
        }

        /// <summary>
        /// Configures automatically sending unhandled errors to the ErrorLog.  The default is true.
        /// </summary>
        public bool ShouldLogUnhandledErrors { get; set; }

        /// <summary>
        /// DataManager will collect statistics on your requests including fetch counts and load times, deserialize times and sizes, and cache hit rates.  
        /// Set this to true to enable, generate staticstics report with DataManager.GetStatisticsReport.
        /// </summary>
        public static bool ShouldCollectStatistics { get; set; }

        private DataManager() :
            base(true)
        {
        }
        
        /// <summary>
        /// Clean up the cache by deleting any cached items that have an expiration time
        /// older than the specified time.  This is a relatively expensive operation so should be 
        /// called sparingly.  
        /// </summary>
        /// <param name="maximumExpirationTime">The maximum expiration time to delete.  In other words, this call will delete
        /// any cache items who's expiration is older than this value.</param>
        /// <param name="complete">An Action that will be called when the cleanup operation has completed.</param>
        public async Task CleanupAsync(DateTime maximumExpirationTime)
        {
            await PriorityQueue.AddStorageWorkItem(StoreProvider.CleanupAsync(maximumExpirationTime));            
        }

        /// <summary>
        /// Clear any stored state for the specified item, both from memory
        /// as well as from the store.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        public async Task ClearAsync<T>(object id) where T : new()
        {
            await ClearAsync<T>(_loadContextCreator.CreateLoadContext<T>(id));
        }

        /// <summary>
        /// Clear any stored state for the specified item, both from memory
        /// as well as from the store.        
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        public async Task ClearAsync<T>(LoadContext id) where T : new()
        {
            ICacheEntry value = null;
            using (var token = await _objectCacheLock.UpgradeableReaderLockAsync())
            {
                if (_objectCache.ContainsKey(typeof(T)) && _objectCache[typeof(T)].TryGetValue(id.UniqueKey, out value))
                {
                    using (await token.UpgradeAsync())
                    {
                        _objectCache[typeof(T)].Remove(id.UniqueKey);
                    }
                }
            }
            var tasksToAwait = new List<Task>();
            if (value != null)
            {
                tasksToAwait.Add(value.ClearAsync());
            }
            tasksToAwait.Add(StoreProvider.DeleteAllAsync(id.UniqueKey));
            await Task.WhenAll(tasksToAwait);
        }

        /// <summary>
        /// Delete all items in the cache.
        /// </summary>
        public async Task DeleteCacheAsync()
        {
            using (await _objectCacheLock.WriterLockAsync())
            {
                _objectCache.Clear();
            }

            await StoreProvider.ClearAsync();
        }

        /// <summary>
        /// Set the given item's cache state to expired so that it will be re-fetched on the next request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        public async Task Invalidate<T>(object id) where T : new()
        {
            var lc = _loadContextCreator.CreateLoadContext<T>(id);
            await Invalidate<T>(lc);
        }

        /// <summary>
        /// Set the given item's cache state to expired so that it will be re-fetched on the next request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="loadContext"></param>
        public async Task Invalidate<T>(LoadContext loadContext) where T : new()
        {
            var cacheItem = await  GetAsync<T>(loadContext, null, null, false);
            if (cacheItem != null)
            {
                cacheItem.SetForRefresh();
            }
        }

        /// <summary>
        /// Load an item.
        /// </summary>
        /// <typeparam name="T">The type of the item to load</typeparam>
        /// <param name="id">A unique identifier for this item's data</param>
        /// <returns></returns>
        public async Task<T> LoadAsync<T>(object id) where T : new()
        {
            var lc = _loadContextCreator.CreateLoadContext<T>(id);
            return await LoadAsync<T>(lc, null, null);
        }

        /// <summary>
        /// Load an item.
        /// </summary>
        /// <typeparam name="T">The type of the item to load.</typeparam>
        /// <param name="loadContext">The load context that describes this item's load.</param>
        /// <returns></returns>
        public async Task<T> LoadAsync<T>(LoadContext loadContext) where T : new()
        {
            return await LoadAsync<T>(loadContext, null, null);
        }
       
        /// <summary>
        /// Load an item.
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="id">A unique identifer for the item</param>
        /// <param name="completed">An action to fire when the operation has successfully completed.</param>
        /// <param name="error">An action to fire if there is an error in the processing of the operation.</param>
        /// <returns>The instance of the item to use/databind to.  As the loads complete, the properties of this instance will be updated.</returns>
        public async Task<T> LoadAsync<T>(object id, Action<T> completed, Action<Exception> error) where T : new()
        {
            return await LoadAsync<T>(_loadContextCreator.CreateLoadContext<T>(id), completed, error);
        }

        /// <summary>
        /// Load an item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="loadContext">The LoadContext for this item.</param>
        /// <param name="completed">An action to fire when the operation has successfully completed.</param>
        /// <param name="error">An action to fire if there is an error in the processing of the operation.</param>
        /// <returns>The instance of the item to use/databind to.  As the loads complete, the properties of this instance will be updated.</returns>
        public async Task<T> LoadAsync<T>(LoadContext loadContext, Action<T> completed, Action<Exception> error) where T : new()
        {
            var cacheItem = await GetAsync<T>(loadContext, completed, error, true);
            return (T)cacheItem.GetValue(false);
        }

        /// <summary>
        /// Synchronously load an item from the cache.
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="id">The item's unique identifier.</param>
        /// <returns>The cached value, or a default instance if one is not available.</returns>
        public async Task<T> LoadFromCacheAsync<T>(object id) where T : new()
        {
            var lc = _loadContextCreator.CreateLoadContext<T>(id);
            return await LoadFromCacheAsync<T>(lc);
        }

        /// <summary>
        /// Synchronously load an item from the cache.
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="loadContext">The item's LoadContext.</param>
        /// <returns>The cached value, or a default instance if one is not available.</returns>        
        public async Task<T> LoadFromCacheAsync<T>(LoadContext loadContext) where T : new()
        {
            var cacheItem = await GetFromCacheAsync<T>(loadContext);
            return (T)cacheItem.GetValue(true);
        }

        private async void OnUnhandledError(Exception ex)
        {
            if (ShouldLogUnhandledErrors)
            {
                await ErrorLog.WriteErrorAsync("An unhandled error occurred", ex);
            }
            DataManagerUnhandledExceptionEventArgs e = new DataManagerUnhandledExceptionEventArgs(ex, false);
            if (UnhandledError != null)
            {
                UnhandledError(this, e);
            }
            if (!e.Handled && System.Diagnostics.Debugger.IsAttached)
            {
                throw ex;
            }
        }

        // TODO: Refactor StatisticsReport into a seperate class
        /// <summary>
        /// Generates a report of the load and deserialize statistics.  This report will detail the following items:
        /// 
        /// * Instance Counts
        /// * Request Counts
        /// * Cache hit/miss rates
        /// * Deserialization failures
        /// * Average and maximum fetch times for
        ///     * Network loads
        ///     * Cache loads
        /// * Average and max data load sizes
        /// * Average and max object update times.  Object updates happen on UI thread and are key to an application's performance.
        /// 
        /// The perfomance impact of collecting this information is very low.  Generating the report is relatively expensive.
        /// </summary>
        /// <param name="writer">The text writer to write the report into</param>
        /// <param name="resetStats">True to reset sttistics after calling.</param>
        public void GetStatisticsReport(TextWriter writer, bool resetStats)
        {
            if (!ShouldCollectStatistics)
            {
                throw new InvalidOperationException("ShoudlCollectStatistics is set to false, set it to true to enable statistics gathering.");
            }

            if (_objectCache != null)
            {
                // compute the totals
                //
                var statsbyType = from t in _objectCache.Values
                                  from ce in t.Values
                                  group ce by ce.ObjectType into ot
                                  select new
                                  {
                                      Type = ot.Key,
                                      Stats = ot.Select(ce2 => ce2.Stats)
                                  };


                var flatStats = from sbt in statsbyType
                                from stats in sbt.Stats
                                select stats;

                if (writer != null)
                {
                    EntryStats.GenerateStats("Totals", flatStats, writer);

                    foreach (var sbt in statsbyType)
                    {
                        EntryStats.GenerateStats("Statistics for " + sbt.Type.FullName, sbt.Stats, writer);
                    }
                }

                if (resetStats)
                {
                    foreach (var s in flatStats)
                    {
                        s.Reset();
                    }
                }
            }
        }

        /// <summary>
        /// Refresh an item's value
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="id">A unique identifer for the item</param>
        /// <param name="completed">An action to fire when the operation has successfully completed.</param>
        /// <param name="error">An action to fire if there is an error in the processing of the operation.</param>
        /// <returns>The instance of the item to use/databind to.  As the loads complete, the properties of this instance will be updated.</returns>
        public async Task<T> RefreshAsync<T>(object id, Action<T> completed, Action<Exception> error) where T : new()
        {
            return await RefreshAsync<T>(_loadContextCreator.CreateLoadContext<T>(id), completed, error);
        }

        /// <summary>
        /// Refresh an item's value
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="loadContext">The loadContext that identifies the item.</param>
        /// <param name="completed">An action to fire when the operation has successfully completed.</param>
        /// <param name="error">An action to fire if there is an error in the processing of the operation.</param>
        /// <returns>The instance of the item to use/databind to.  As the loads complete, the properties of this instance will be updated.</returns>
        public async Task<T> RefreshAsync<T>(LoadContext loadContext, Action<T> completed, Action<Exception> error) where T : new()
        {
            await Invalidate<T>(loadContext);
            return await LoadAsync<T>(loadContext, completed, error);
        }

        // TODO: Refactor the reflection out
        /// <summary>
        /// Refresh the specified IUpdateable item.
        /// </summary>
        /// <param name="item"></param>
        internal void Refresh(IUpdatable item)
        {
            var methodInfo = (from m in this.GetType().GetRuntimeMethods()
                              where m.Name == "RefreshAsync" && m.ContainsGenericParameters
                              select m).First();

            var method = methodInfo.MakeGenericMethod(item.GetType());
            
            method.Invoke(this, new object[] { item.LoadContext, null, null });
        }

        /// <summary>
        /// Register a proxy value.  This value will be updated upon calls to Load or Refresh for an object of type
        /// T that has the same LoadContext as the passed in item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The instance to update.</param>
        public async Task RegisterProxy<T>(T value) where T : ILoadContextItem, new()
        {
            await RegisterProxy<T>(value, false, null, true);
        }

        /// <summary>
        /// Register a proxy value.  This value will be updated upon calls to Load or Refresh for an object of type
        /// T that has the same LoadContext as the passed in item.        
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The instance to register as a proxy</param>
        /// <param name="doLoad">Initiate a load for this item, which will cause it to be updated with new data if a fetch is needed.</param>
        /// <param name="update">A handler to fire when this instance is updated.</param>
        /// <param name="canUseAsInitialValue">True to have this value returned for the default value of a subsequent Load call, assuming no value currently exists for the item. Otherwise this is ignored.</param>
        public async Task RegisterProxy<T>(T value, bool doLoad, Action<T> update, bool canUseAsInitialValue) where T : ILoadContextItem, new()
        {
            if (value == null || value.LoadContext == null)
            {
                throw new ArgumentNullException();
            }

            // TODO: Hide implementation a bit better?
            var pe = new ProxyManager.ProxyEntry
            {
                LoadContext = value.LoadContext,
                ObjectType = typeof(T),
                ProxyReference = new WeakReference(value),
                UpdateAction = () =>
                {
                    if (update != null)
                    {
                        update(value);
                    }
                },
                UseAsInitialValue = canUseAsInitialValue
            };

            ProxyManager.AddProxy(pe);

            if (doLoad)
            {
                await DataManager.Current.LoadAsync<T>(value.LoadContext);
            }
        }

        /// <summary>
        /// Unregister the specified value from the proxy list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        public void UnregisterProxy<T>(T value)
        {
            ProxyManager.RemoveProxy(value);
        }

        /// <summary>
        /// Saves data an instance into the cache.  The DataLoader for type T must
        /// implement IDataOptimizer for this to work, othwerise an InvalidOperationException will be thrown.
        /// 
        /// This also updates the current value in the cache and increments the update/cache time.  In other words,
        /// this simulates a live load/refresh.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance">The instance to save into the cache.</param>
        /// <param name="loadContext"></param>
        public async Task SaveAsync<T>(T instance, LoadContext loadContext) where T : new()
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }

            if (loadContext == null)
            {
                throw new ArgumentNullException("loadContext");
            }

            var cacheEntry = await GetAsync<T>(loadContext, null, null, false);

            if (!cacheEntry.SerializeDataToCache(instance, DateTime.Now, null, true))
            {
                throw new InvalidOperationException("Instance could not be serialized.  Ensure that its DataLoader implements IDataLoader and the SerializeOptimizedData method properly serializes the data.");
            }
            else
            {
                cacheEntry.UpdateValue(instance, loadContext);
            }
        }        

        // TODO: Refactor into CacheManager class
        internal async Task<ICacheEntry> Get<T>(object identity) where T : new()
        {
            return await GetAsync<T>(identity, null, null, false);
        }

        /// <summary>
        /// Get the Entry for a given type/id pair and set it up if necessary.
        /// </summary>
        private async Task<ICacheEntry> GetAsync<T>(object identity, Action<T> completed, Action<Exception> error, bool resetCallbacks) where T : new()
        {
            LoadContext loadContext = _loadContextCreator.CreateLoadContext<T>(identity);

            return await GetAsync<T>(loadContext, completed, error, resetCallbacks);
        }

        // TODO: Refactor this into less of a confusing mess
        private async  Task<ICacheEntry> GetAsync<T>(LoadContext loadContext, Action<T> completed, Action<Exception> error, bool resetCallbacks) where T : new()
        {
            if (loadContext == null)
            {
                throw new ArgumentNullException("LoadContext required.");
            }

            var identity = loadContext.UniqueKey;
            Type objectType = typeof(T);

            ICacheEntry value = null;

            // TODO: Ensure ALL _objectCache reads are locked
            using (var token =await _objectCacheLock.UpgradeableReaderLockAsync())
            {
                if (_objectCache.ContainsKey(objectType) && _objectCache[objectType].TryGetValue(identity, out value))
                {
                    using (await token.UpgradeAsync())
                    {
                        value.LoadContext = loadContext;
                    }
                }
            }
            if (value != null)
            {
                if (resetCallbacks)
                {
                    // TODO: What does this do?!
                    SetupCompletedCallback<T>(completed, error, value);
                }
                return await Task<ICacheEntry>.FromResult(value);
            }            

            // create a new one.
            //
            value = new CacheEntry(loadContext, objectType);
            value.NextCompletedAction.UnhandledError = OnUnhandledError;

            object loader = value.GetDataLoader();

            // TODO: What even is this?!
           SetupCompletedCallback<T>(completed, error, value);

            // if this thing knows how to optimize, hook that up.
            //
            if (loader is IDataOptimizer)
            {
                value.SerializeOptimizedDataAction = (obj, stream) =>
                {
                    var idl = (IDataOptimizer)loader;
                    return idl.SerializeOptimizedData(obj, stream);
                };
            }

            // finally push the value into the cache.

            using (await _objectCacheLock.WriterLockAsync())
            {
                if (!_objectCache.ContainsKey(objectType))
                {
                    Dictionary<object, ICacheEntry> typeDictionary = new Dictionary<object, ICacheEntry>();
                    _objectCache[typeof(T)] = typeDictionary;
                }
                _objectCache[typeof(T)][identity] = value;
            }
            return value;
        }

        /// <summary>
        /// Grab an item only from the cache.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identity"></param>
        /// <returns></returns>
        private async Task<ICacheEntry> GetFromCacheAsync<T>(object identity) where T : new()
        {
            var cacheEntry = await GetAsync<T>(identity, null, null, true);
            await cacheEntry.LoadFromCache();
            return cacheEntry;
        }

        /// <summary>
        /// Setup the callbacks to fire upon completiion of the next load operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="completed"></param>
        /// <param name="error"></param>
        /// <param name="value"></param>
        private void SetupCompletedCallback<T>(Action<T> completed, Action<Exception> error, ICacheEntry value) where T : new()
        {
            value.NextCompletedAction.Subscribe(completed, error);
        }
    }
}
