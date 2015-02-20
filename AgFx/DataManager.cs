// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace AgFx
{
    /// <summary>
    ///     DataManager allows access to cached objects and manageds not only caching but reloads, etc.
    /// </summary>
    public class DataManager : NotifyPropertyChangedBase
    {
        private int _loadingCount;
        private bool _logUnhandledErrors = true;
        // TODO: How many of these caches do we actually need for perf?
        private readonly Dictionary<Type, object> _loaders;
        private readonly Dictionary<Type, Dictionary<object, CacheEntry>> _objectCache;
        private readonly Dictionary<object, List<ProxyEntry>> _proxies;
        private readonly IStoreProvider _storeProvider;
        private readonly AutoLoadContextCreator _loadContextCreator;
        private readonly IUiDispatcher _uiDispatcher;

        /// <summary>
        ///     Initialise the DataManager
        /// </summary>
        /// <param name="storeProvider">Store provider to use for long term value caching</param>
        /// <param name="uiDispatcher">The dispatcher to use for internal dispatching</param>
        public DataManager(IStoreProvider storeProvider, IUiDispatcher uiDispatcher)
        {
            _storeProvider = storeProvider;
            _uiDispatcher = uiDispatcher;
            _loaders = new Dictionary<Type, object>();
            _proxies = new Dictionary<object, List<ProxyEntry>>();
            _objectCache = new Dictionary<Type, Dictionary<object, CacheEntry>>();
            _loadContextCreator = new AutoLoadContextCreator();
        }

        /// <summary>
        ///     True when the DataManager is in the process of performing a load.
        /// </summary>
        public bool IsLoading
        {
            get { return _loadingCount > 0; }
            internal set
            {
                var loading = IsLoading;
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

                if(loading != IsLoading)
                {
                    RaisePropertyChanged("IsLoading");
                }
            }
        }

        /// <summary>
        ///     Configures automatically sending unhandled errors to the ErrorLog.  The default is true.
        /// </summary>
        public bool ShouldLogUnhandledErrors
        {
            get { return _logUnhandledErrors; }
            set { _logUnhandledErrors = value; }
        }

        /// <summary>
        ///     DataManager will collect statistics on your requests including fetch counts and load times, deserialize times and
        ///     sizes, and cache hit rates.
        ///     Set this to true to enable, generate staticstics report with DataManager.GetStatisticsReport.
        /// </summary>
        public bool ShouldCollectStatistics { get; set; }

        /// <summary>
        ///     Event handler for handling any errors not caught by a method level handler (e.g. Load(id, success, error).
        /// </summary>
        public event EventHandler<ApplicationUnhandledExceptionEventArgs> UnhandledError;

        /// <summary>
        ///     Clean up the cache by deleting any cached items that have an expiration time
        ///     older than the specified time.  This is a relatively expensive operation so should be
        ///     called sparingly.
        /// </summary>
        /// <param name="maximumExpirationTime">
        ///     The maximum expiration time to delete.  In other words, this call will delete
        ///     any cache items who's expiration is older than this value.
        /// </param>
        public void Cleanup(DateTime maximumExpirationTime)
        {       var retries = 3;

            while(retries-- > 0)
            {
                try
                {
                    // get the list of items.
                    //
                    var itemsToCleanup = from item in _storeProvider.GetItems()
                        where item.ExpirationTime <= maximumExpirationTime
                        select item.UniqueName;

                    // we snap the enumerable to an array to guard against any provider
                    // implementations that might have returned an enumerator that would be affected
                    // by the delete operation.
                    //
                    foreach(var uniqueName in itemsToCleanup.ToArray())
                    {
                        _storeProvider.Delete(uniqueName);
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine("Exception trying to clean up (attempt {0}): {1}", 3 - retries, ex);

                    // someetimes exceptions come out of the isostore stack trying to get the directory names,
                    // so we just wait a bit and try again.
                    //
                    Thread.Sleep(50);
                }
            }
        }

        /// <summary>
        ///     Clear any stored state for the specified item, both from memory
        ///     as well as from the store.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        public void Clear<T>(object id) where T : new()
        {
            Clear<T>(_loadContextCreator.CreateLoadContext<T>(id));
        }

        /// <summary>
        ///     Clear any stored state for the specified item, both from memory
        ///     as well as from the store.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        public void Clear<T>(LoadContext id) where T : new()
        {
            lock (_objectCache)
            {
                CacheEntry value;
                if (_objectCache.ContainsKey(typeof (T)) &&
                    _objectCache[typeof (T)].TryGetValue(id.UniqueKey, out value))
                {
                    value.Clear();
                    _objectCache[typeof (T)].Remove(id.UniqueKey);
                    _storeProvider.Delete(id.UniqueKey);
                }
            }
        }

        /// <summary>
        ///     Delete all items in the cache.
        /// </summary>
        public void DeleteCache()
        {
            lock (_objectCache)
            {
                _objectCache.Clear();
                _storeProvider.Delete();
            }
        }

        /// <summary>
        ///     Set the given item's cache state to expired so that it will be re-fetched on the next request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        public void Invalidate<T>(object id) where T : new()
        {
            var lc = _loadContextCreator.CreateLoadContext<T>(id);
            Invalidate<T>(lc);
        }

        /// <summary>
        ///     Set the given item's cache state to expired so that it will be re-fetched on the next request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="loadContext"></param>
        public void Invalidate<T>(LoadContext loadContext) where T : new()
        {
            var cacheItem = Get<T>(loadContext, null, null, false);
            if (cacheItem != null)
            {
                cacheItem.SetForRefresh();
            }
        }

        /// <summary>
        ///     Load an item.
        /// </summary>
        /// <typeparam name="T">The type of the item to load</typeparam>
        /// <param name="id">A unique identifier for this item's data</param>
        /// <returns></returns>
        public T Load<T>(object id) where T : new()
        {
            var lc = _loadContextCreator.CreateLoadContext<T>(id);
            return Load<T>(lc, null, null);
        }

        /// <summary>
        ///     Load an item.
        /// </summary>
        /// <typeparam name="T">The type of the item to load.</typeparam>
        /// <param name="loadContext">The load context that describes this item's load.</param>
        /// <returns></returns>
        public T Load<T>(LoadContext loadContext) where T : new()
        {
            return Load<T>(loadContext, null, null);
        }

        /// <summary>
        ///     Non-generic version of load.
        /// </summary>
        /// <param name="objectType">The object type to load</param>
        /// <param name="id"></param>
        /// <returns></returns>
        public object Load(Type objectType, object id)
        {
            var methodInfo = (from m in GetType().GetMethods()
                where m.Name == "Load" && m.ContainsGenericParameters && m.GetParameters().Length == 3
                select m).First();

            var method = methodInfo.MakeGenericMethod(objectType);

            return method.Invoke(this, new[] {id, null, null});
        }

        /// <summary>
        ///     Non-generic version of load.
        /// </summary>
        public object Load(Type objectType, LoadContext loadContext)
        {
            return Load(objectType, (object) loadContext);
        }

        /// <summary>
        ///     Load an item.
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="id">A unique identifer for the item</param>
        /// <param name="completed">An action to fire when the operation has successfully completed.</param>
        /// <param name="error">An action to fire if there is an error in the processing of the operation.</param>
        /// <returns>
        ///     The instance of the item to use/databind to.  As the loads complete, the properties of this instance will be
        ///     updated.
        /// </returns>
        public T Load<T>(object id, Action<T> completed, Action<Exception> error) where T : new()
        {
            return Load(_loadContextCreator.CreateLoadContext<T>(id), completed, error);
        }

        /// <summary>
        ///     Load an item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="loadContext">The LoadContext for this item.</param>
        /// <param name="completed">An action to fire when the operation has successfully completed.</param>
        /// <param name="error">An action to fire if there is an error in the processing of the operation.</param>
        /// <returns>
        ///     The instance of the item to use/databind to.  As the loads complete, the properties of this instance will be
        ///     updated.
        /// </returns>
        public T Load<T>(LoadContext loadContext, Action<T> completed, Action<Exception> error) where T : new()
        {
            var cacheItem = Get(loadContext, completed, error, true);
            return (T) cacheItem.GetValue(false);
        }

        /// <summary>
        ///     Synchronously load an item from the cache.
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="id">The item's unique identifier.</param>
        /// <returns>The cached value, or a default instance if one is not available.</returns>
        public T LoadFromCache<T>(object id) where T : new()
        {
            var lc = _loadContextCreator.CreateLoadContext<T>(id);
            return LoadFromCache<T>(lc);
        }

        /// <summary>
        ///     Synchronously load an item from the cache.
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="loadContext">The item's LoadContext.</param>
        /// <returns>The cached value, or a default instance if one is not available.</returns>
        public T LoadFromCache<T>(LoadContext loadContext) where T : new()
        {
            var cacheItem = GetFromCache<T>(loadContext);

            return (T) cacheItem.GetValue(true);
        }

        private void OnUnhandledError(Exception ex)
        {
            if (ShouldLogUnhandledErrors)
            {
                ErrorLog.WriteError("An unhandled error occurred", ex);
            }
            var e = new ApplicationUnhandledExceptionEventArgs(ex, false);
            if (UnhandledError != null)
            {
                UnhandledError(this, e);
            }
            if (!e.Handled && Debugger.IsAttached)
            {
                throw ex;
            }
        }

        // TODO: Shift this to an extension method?
        /// <summary>
        ///     Generates a report of the load and deserialize statistics.  This report will detail the following items:
        ///     * Instance Counts
        ///     * Request Counts
        ///     * Cache hit/miss rates
        ///     * Deserialization failures
        ///     * Average and maximum fetch times for
        ///     * Network loads
        ///     * Cache loads
        ///     * Average and max data load sizes
        ///     * Average and max object update times.  Object updates happen on UI thread and are key to an application's
        ///     performance.
        ///     The perfomance impact of collecting this information is very low.  Generating the report is relatively expensive.
        /// </summary>
        /// <param name="writer">The text writer to write the report into</param>
        /// <param name="resetStats">True to reset sttistics after calling.</param>
        public void GetStatisticsReport(TextWriter writer, bool resetStats)
        {
            if (!ShouldCollectStatistics)
            {
                throw new InvalidOperationException(
                    "ShoudlCollectStatistics is set to false, set it to true to enable statistics gathering.");
            }

            if (_objectCache == null)
            {
                return;
            }
            // compute the totals
            //
            var statsbyType = from t in _objectCache.Values
                from ce in t.Values
                group ce by ce.ObjectType
                into ot
                select new
                {
                    Type = ot.Key,
                    Stats = ot.Select(ce2 => ce2._stats)
                };

            var flatStats = from sbt in statsbyType
                from stats in sbt.Stats
                select stats;

            if (writer != null)
            {
                GenerateStats("Totals", flatStats, writer);

                foreach (var sbt in statsbyType)
                {
                    GenerateStats("Statistics for " + sbt.Type.FullName, sbt.Stats, writer);
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

        private static void GenerateStats(string groupName, IEnumerable<EntryStats> flatStats, TextWriter writer)
        {
            var totalInstances = flatStats.Count();
            var totalRequests = flatStats.Sum(s => s.RequestCount);
            var totalFetchReqeusts = flatStats.Sum(s => s.FetchCount);
            var totalFetchFail = flatStats.Sum(s => s.FetchFailCount);
            var totalDeserializeFail = flatStats.Sum(s => s.DeserializeFailCount);

            var skipAvg = !flatStats.Any();

            var cacheHitRate = skipAvg ? 0.0 : flatStats.Average(s => s.CacheHitRatio);
            var avgFetch = skipAvg ? 0.0 : flatStats.Average(s => s.AverageFetchTime);
            var avgDeserialize = skipAvg ? 0.0 : flatStats.Average(s => s.AverageDeserializeTime);
            var avgSize = skipAvg ? 0.0 : flatStats.Average(s => s.AverageDataSize);
            var avgUpdate = skipAvg ? 0.0 : flatStats.Average(s => s.AverageUpdateTime);

            var maxFetch = from s in flatStats
                where s.MaxFetchTime == flatStats.Max(s2 => s2.MaxFetchTime)
                select s;

            var maxDeserialize = from s in flatStats
                where s.MaxDeserializeTime == flatStats.Max(s2 => s2.MaxDeserializeTime)
                select s;

            var maxSize = from s in flatStats
                where s.MaxDataSize == flatStats.Max(s2 => s2.MaxDataSize)
                select s;

            var maxUpdate = from s in flatStats
                where s.MaxUpdateTime == flatStats.Max(s2 => s2.MaxUpdateTime)
                select s;

            writer.WriteLine("{0}:", groupName);
            writer.WriteLine("\tTotal Instances: {0}", totalInstances);
            writer.WriteLine("\tTotal Requests: {0}", totalRequests);
            writer.WriteLine("\tTotal Fetches: {0}", totalFetchReqeusts);
            writer.WriteLine("\tTotal Fetch Failures: {0}", totalFetchFail);
            writer.WriteLine("\tTotal Deserialization Failures: {0}", totalDeserializeFail);
            writer.WriteLine("\tCache Hit Rate: {0:0.00}", cacheHitRate);
            writer.WriteLine("\tAverage Fetch Time: {0:0.0}ms", avgFetch);

            if (maxFetch.Any())
            {
                var mf = maxFetch.First();
                writer.WriteLine("\tMaximum Fetch Time: {0:0.0}ms (Object Type={1}, ID={2})", mf.MaxFetchTime,
                    mf._cacheEntry.ObjectType.Name, mf._cacheEntry.LoadContext.Identity);
            }

            writer.WriteLine("\tAverage Deserialize Time: {0:0.0}ms", avgDeserialize);
            if (maxDeserialize.Any())
            {
                var md = maxDeserialize.First();
                writer.WriteLine("\tMaximum Deserialize Time: {0:0.0}ms (Object Type={1}, ID={2})",
                    md.MaxDeserializeTime, md._cacheEntry.ObjectType.Name, md._cacheEntry.LoadContext.Identity);
            }


            writer.WriteLine("\tAverage Deserialize Size: {0:0.0} bytes", avgSize);
            if (maxSize.Any())
            {
                var ms = maxSize.First();
                writer.WriteLine("\tMaximum Deserialize Size: {0} bytes (Object Type={1}, ID={2})", ms.MaxDataSize,
                    ms._cacheEntry.ObjectType.Name, ms._cacheEntry.LoadContext.Identity);
            }

            writer.WriteLine("\tAverage Update Time (UI Thread): {0:0.0}ms", avgUpdate);
            if (maxUpdate.Any())
            {
                var mu = maxUpdate.First();
                writer.WriteLine("\tMaximum Update Time (UI Thread): {0:0.0}ms (Object Type={1}, ID={2})",
                    mu.MaxUpdateTime, mu._cacheEntry.ObjectType.Name, mu._cacheEntry.LoadContext.Identity);
            }

            writer.WriteLine("\r\n\r\n");
        }

        /// <summary>
        ///     Refresh an item's value
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="id">A unique identifer for the item</param>
        /// <param name="completed">An action to fire when the operation has successfully completed.</param>
        /// <param name="error">An action to fire if there is an error in the processing of the operation.</param>
        /// <returns>
        ///     The instance of the item to use/databind to.  As the loads complete, the properties of this instance will be
        ///     updated.
        /// </returns>
        public T Refresh<T>(object id, Action<T> completed, Action<Exception> error) where T : new()
        {
            return Refresh(_loadContextCreator.CreateLoadContext<T>(id), completed, error);
        }

        /// <summary>
        ///     Refresh an item's value
        /// </summary>
        /// <typeparam name="T">The type of item to load</typeparam>
        /// <param name="loadContext">The loadContext that identifies the item.</param>
        /// <param name="completed">An action to fire when the operation has successfully completed.</param>
        /// <param name="error">An action to fire if there is an error in the processing of the operation.</param>
        /// <returns>
        ///     The instance of the item to use/databind to.  As the loads complete, the properties of this instance will be
        ///     updated.
        /// </returns>
        public T Refresh<T>(LoadContext loadContext, Action<T> completed, Action<Exception> error) where T : new()
        {
            Invalidate<T>(loadContext);
            return Load(loadContext, completed, error);
        }

        /// <summary>
        ///     Non-generic version of load.
        /// </summary>
        /// <param name="objectType">The object type to load</param>
        /// <param name="id"></param>
        /// <returns></returns>
        public object Refresh(Type objectType, object id)
        {
            var methodInfo = (from m in GetType().GetMethods()
                              where m.Name == "Refresh" && m.ContainsGenericParameters && m.GetParameters().Length == 3
                              select m).First();

            var method = methodInfo.MakeGenericMethod(objectType);

            return method.Invoke(this, new[] { id, null, null });
        }

        
        /// <summary>
        ///     Register a proxy value.  This value will be updated upon calls to Load or Refresh for an object of type
        ///     T that has the same LoadContext as the passed in item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The instance to update.</param>
        public void RegisterProxy<T>(T value) where T : ILoadContextItem, new()
        {
            RegisterProxy(value, false, null);
        }

        /// <summary>
        ///     Register a proxy value.  This value will be updated upon calls to Load or Refresh for an object of type
        ///     T that has the same LoadContext as the passed in item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The instance to register as a proxy</param>
        /// <param name="doLoad">
        ///     Initiate a load for this item, which will cause it to be updated with new data if a fetch is
        ///     needed.
        /// </param>
        /// <param name="update">A handler to fire when this instance is updated.</param>
        public void RegisterProxy<T>(T value, bool doLoad, Action<T> update)
            where T : ILoadContextItem, new()
        {
            if (value == null || value.LoadContext == null)
            {
                throw new ArgumentNullException();
            }

            var pe = new ProxyEntry
            {
                LoadContext = value.LoadContext,
                ObjectType = typeof (T),
                ProxyReference = new WeakReference(value),
                UpdateAction = () =>
                {
                    if(update != null)
                    {
                        update(value);
                    }
                }
            };

            AddProxy(pe);

            if (doLoad)
            {
                Load<T>(value.LoadContext);
            }
        }

        /// <summary>
        ///     Unregister the specified value from the proxy list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        public void UnregisterProxy<T>(T value)
        {
            RemoveProxy(value);
        }

        /// <summary>
        ///     Saves data an instance into the cache.  The DataLoader for type T must
        ///     implement IDataOptimizer for this to work, othwerise an InvalidOperationException will be thrown.
        ///     This also updates the current value in the cache and increments the update/cache time.  In other words,
        ///     this simulates a live load/refresh.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance">The instance to save into the cache.</param>
        /// <param name="loadContext"></param>
        public void Save<T>(T instance, LoadContext loadContext) where T : new()
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }

            if (loadContext == null)
            {
                throw new ArgumentNullException("loadContext");
            }

            var cacheEntry = Get<T>(loadContext, null, null, false);

            if (!cacheEntry.SerializeDataToCache(instance, DateTime.Now, null, true))
            {
                throw new InvalidOperationException(
                    "Instance could not be serialized.  Ensure that its DataLoader implements IDataLoader and the SerializeOptimizedData method properly serializes the data.");
            }
            cacheEntry.UpdateValue(instance, loadContext);
        }

        private void AddProxy(ProxyEntry pe)
        {
            List<ProxyEntry> proxyList;

            if (!_proxies.TryGetValue(pe.LoadContext, out proxyList))
            {
                proxyList = new List<ProxyEntry>();
                _proxies[pe.LoadContext] = proxyList;
            }

            var existingProxy = from p in proxyList
                where p.ProxyReference.Target == pe.ProxyReference.Target
                select p;

            if (!existingProxy.Any())
            {
                proxyList.Add(pe);
            }
        }

        private IEnumerable<ProxyEntry> GetProxies<T>(LoadContext lc)
        {
            List<ProxyEntry> proxyList;

            if (!_proxies.TryGetValue(lc, out proxyList))
            {
                return new ProxyEntry[0];
            }

            var proxies = from p in proxyList
                where p.ObjectType.IsAssignableFrom(typeof (T)) && p.ProxyReference.IsAlive
                select p;

            return proxies.ToArray();
        }

        private void RemoveProxy(ProxyEntry pe)
        {
            List<ProxyEntry> proxyList;

            if (_proxies.TryGetValue(pe.LoadContext, out proxyList))
            {
                proxyList.Remove(pe);
            }
        }

        private void RemoveProxy(object value)
        {
            var proxy = from pel in _proxies.Values
                from p in pel
                where p.ProxyReference != null && p.ProxyReference.Target == value
                select p;


            foreach (var p in proxy)
            {
                RemoveProxy(p);
            }
        }

        internal CacheEntry Get<T>(object identity) where T : new()
        {
            return Get<T>(identity, null, null, false);
        }

        /// <summary>
        ///     Get the Entry for a given type/id pair and set it up if necessary.
        /// </summary>
        private CacheEntry Get<T>(object identity, Action<T> completed, Action<Exception> error, bool resetCallbacks)
            where T : new()
        {
            var loadContext = _loadContextCreator.CreateLoadContext<T>(identity);

            return Get(loadContext, completed, error, resetCallbacks);
        }

        private CacheEntry Get<T>(LoadContext loadContext, Action<T> completed, Action<Exception> error,
            bool resetCallbacks) where T : new()
        {
            if (loadContext == null)
            {
                throw new ArgumentNullException("loadContext");
            }

            object identity = loadContext.UniqueKey;

            CacheEntry value;
            lock (_objectCache)
            {
                if (_objectCache.ContainsKey(typeof (T)) && _objectCache[typeof (T)].TryGetValue(identity, out value))
                {
                    value.LoadContext = loadContext;
                    if(resetCallbacks)
                    {
                        SetupCompletedCallback(completed, error, value);
                    }
                    return value;
                }
            }

            var objectType = typeof (T);

            // TODO: Push as much of this into the CacheEntry itself as possible
            Action<CacheEntry> proxyCallback =
                cacheEntry =>
                {
                    var v = (T) cacheEntry.ValueInternal;
                    foreach (var proxy in GetProxies<T>(cacheEntry.LoadContext))
                    {
                        // copy the values over
                        //
                        ReflectionSerializer.UpdateObject(v, proxy.ProxyReference.Target, null);

                        // fire the update notification
                        //
                        if (proxy.UpdateAction != null)
                        {
                            proxy.UpdateAction();
                        }
                    }
                };

            // create a new one.
            //
            value = new CacheEntry(loadContext, objectType, proxyCallback, _storeProvider, ShouldCollectStatistics);
            value.NextCompletedAction.UnhandledError = OnUnhandledError;

            var loader = GetDataLoader(value);

            // How to create a new value.  It's just a new.
            //
            value.CreateDefaultAction = () =>
            {
                // if there is a proxy already registered, use it as the key value.
                //
                var proxy = GetProxies<T>(loadContext).FirstOrDefault();

                if (proxy != null && proxy.ProxyReference != null && proxy.ProxyReference.IsAlive)
                {
                    return proxy.ProxyReference.Target;
                }

                var item = new T();

                var contextItem = item as ILoadContextItem;
                if(contextItem != null)
                {
                    contextItem.LoadContext = value.LoadContext;
                }
                return item;
            };

            SetupCompletedCallback(completed, error, value);

            // How to load a new value.
            //
            value.LoadAction = lvl =>
            {
                if (loader == null)
                    throw new InvalidOperationException("Could not find loader for type: " + typeof (T).Name);
                // get a loader and ask for a load request.
                //                
                Debug.Assert(loader != null, "Failed to get loader for " + typeof (T).Name);
                var request = DataLoaderProxy.GetLoadRequest(loader, value.LoadContext, typeof (T));

                if (request == null)
                {
                    Debug.WriteLine("{0}: Aborting load for {1}, ID={2}, because {3}.GetLoadRequest returned null.",
                        DateTime.Now, typeof (T).Name, value.LoadContext.Identity, loader.GetType().Name);
                    return false;
                }

                // fire off the load.
                //
                IsLoading = true;
                request.Execute(
                    result =>
                    {
                        if (result == null)
                        {
                            throw new ArgumentNullException("result", "Execute must return a LoadRequestResult value.");
                        }
                        if (result.Error == null)
                        {
                            lvl.OnLoadSuccess(result.Stream);
                        }
                        else
                        {
                            lvl.OnLoadFail(new LoadRequestFailedException(lvl.CacheEntry.ObjectType, value.LoadContext,
                                result.Error));
                        }
                        IsLoading = false;
                    }
                    );
                return true;
            };


            // how to deserialize.
            value.DeserializeAction = (id, data, isOptimized) =>
            {
                if (loader == null)
                {
                    throw new InvalidOperationException("Could not find loader for type: " + typeof (T).Name);
                }

                // get the loader and ask for deserialization
                //                
                object deserializedObject;

                if (isOptimized)
                {
                    var idl = loader as IDataOptimizer;

                    if (idl == null)
                    {
                        throw new InvalidOperationException(
                            "Data is optimized but object does not implement IDataOptimizer");
                    }
                    deserializedObject = idl.DeserializeOptimizedData(value.LoadContext, objectType, data);
                }
                else
                {
                    deserializedObject = DataLoaderProxy.Deserialize(loader, value.LoadContext, objectType, data);
                }

                if (deserializedObject == null)
                {
                    throw new InvalidOperationException(String.Format("Deserialize returned null for {0}, id='{1}'",
                        objectType.Name, id));
                }

                if (!objectType.IsInstanceOfType(deserializedObject))
                {
                    throw new InvalidOperationException(String.Format("Returned object is {0} when {1} was expected",
                        deserializedObject.GetType().Name, objectType.Name));
                }
                return deserializedObject;
            };

            // if this thing knows how to optimize, hook that up.
            //
            var dataOptimizer = loader as IDataOptimizer;
            if (dataOptimizer != null)
            {
                value.SerializeOptimizedDataAction = (obj, stream) =>
                {
                    var idl = dataOptimizer;
                    return idl.SerializeOptimizedData(obj, stream);
                };
            }

            // finally push the value into the cache.

            lock (_objectCache)
            {
                if (!_objectCache.ContainsKey(objectType))
                {
                    var typeDictionary = new Dictionary<object, CacheEntry>();
                    _objectCache[typeof (T)] = typeDictionary;
                }
                _objectCache[typeof (T)][identity] = value;
            }
            return value;
        }
        
        /// <summary>
        ///     Grab an item only from the cache.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identity"></param>
        /// <returns></returns>
        private CacheEntry GetFromCache<T>(object identity) where T : new()
        {
            var cacheEntry = Get<T>(identity, null, null, true);

            cacheEntry.LoadFromCache();
            return cacheEntry;
        }

        // TODO: Can this be pushed into a CacheEntry instead?
        /// <summary>
        ///     Setup the callbacks to fire upon completiion of the next load operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="completed"></param>
        /// <param name="error"></param>
        /// <param name="value"></param>
        private void SetupCompletedCallback<T>(Action<T> completed, Action<Exception> error, CacheEntry value)
            where T : new()
        {
            Action<T> completedActionOnSynchronizationThread = null;
            Action<Exception> errorActionOnSynchronizationThread = null;
            if(completed != null)
            {
                completedActionOnSynchronizationThread =
                    newValue => _uiDispatcher.Dispatch(() => completed(newValue));
            }
            if(error != null)
            {
                errorActionOnSynchronizationThread =
                    exception => _uiDispatcher.Dispatch(() => error(exception));
            }
            value.NextCompletedAction.Subscribe(
                completedActionOnSynchronizationThread,
                errorActionOnSynchronizationThread);
            // TODO: Why is this only debug/should DataManager know this much about how CacheEntryies work?
#if DEBUG
            value.LastLoadStackTrace = new StackTrace();
#endif
        }

        // TODO: Push this into a seperate class and test it
        /// <summary>
        ///     Figure out what dataloader to use for the entry type.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private object GetDataLoader(CacheEntry entry)
        {
            object loader;

            var objectType = entry.ObjectType;

            lock (_loaders)
            {
                if (_loaders.TryGetValue(objectType, out loader))
                {
                    return loader;
                }
            }

            var attrs = objectType.GetCustomAttributes(typeof (DataLoaderAttribute), true);

            if (attrs.Length > 0)
            {
                var dla = (DataLoaderAttribute) attrs[0];

                if (dla.DataLoaderType != null)
                {
                    loader = Activator.CreateInstance(dla.DataLoaderType);
                }
            }
            else
            {
                // GetNestedTypes returns the nested types defined on the current
                // type only, so it will not get loaders defined on super classes.
                // So we just walk through the types until we find one or hit System.Object.
                //
                for (var modelType = objectType;
                    loader == null && modelType != typeof (object);
                    modelType = modelType.BaseType)
                {
                    // see if we already have a loader at this level
                    //
                    if (_loaders.TryGetValue(modelType, out loader) && loader != null)
                    {
                        break;
                    }

                    var loaders = from nt in modelType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                        where
                            nt.GetInterfaces()
                                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof (IDataLoader<>))
                        select nt;

                    var loaderType = loaders.FirstOrDefault();

                    if (loaderType != null)
                    {
                        loader = Activator.CreateInstance(loaderType);

                        // if we're walking base types, save this value so that the subsequent requests will get it.
                        //
                        if (loader != null && modelType != objectType)
                        {
                            _loaders[modelType] = loader;
                        }
                    }
                }
            }

            lock (_loaders)
            {
                if (loader != null)
                {
                    _loaders[objectType] = loader;
                    return loader;
                }
            }
            throw new InvalidOperationException(
                String.Format(
                    "DataLoader not specified for type {0}.  All DataManager-loaded types must implement a data loader by one of the following methods:\r\n\r\n1. Specify a IDataLoader-implementing type on the class with the DataLoaderAttribute.\r\n2. Create a public nested type that implements IDataLoader.",
                    objectType.Name));
        }

        // TODO: Push into a seperate class file
        private class ProxyEntry
        {
            public WeakReference ProxyReference { get; set; }
            public Type ObjectType { get; set; }
            public LoadContext LoadContext { get; set; }
            public Action UpdateAction { get; set; }
        }
    }
}