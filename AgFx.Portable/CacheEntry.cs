// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using Nito.AsyncEx;

namespace AgFx
{
    /// <summary>
    /// This class does most of the heavy lifting for this framework.
    /// 
    /// The CacheEntry has a few jobs:
    /// 
    /// 1) Allow access to the current value for this item
    /// 2) Know how to get to the cached value for this item
    /// 3) Know how to kick of a new load for this item
    /// 4) Know how to decide which is the current value (cached or new load, based on expiration, etc.)    
    /// </summary>
    internal class CacheEntry : ICacheEntry, IDisposable
    {
        // TODO: Refactor CacheEntry into a few seperate classes/files, matching the 4 jobs above

        // some items we use in a bit field to improve instance size.
        //
        private const int SynchronousModeMask = 0x00010000;
        private const int GettingValueMask = 0x00100000;
        private const int UsingCachedValueMask = 0x01000000;
        private const int LoadPendingMask = 0x10000000;
        private const int LiveValueSuccessMask = 0x00001000;
        private const int VersionMask = 0x00000FFF;
        private int _bitfield;

        private readonly AsyncLock lockObject = new AsyncLock();

        // TODO: Declare this locally
        public Func<object, Stream, bool> SerializeOptimizedDataAction { get; set; }

        // Completion notifications
        public UpdateCompletionHandler NextCompletedAction { get; private set; }

        // Object information
        //
        public LoadContext LoadContext { get; set; }
        public Type ObjectType { get; set; }
        private string _uniqueName;
        private TimeSpan? _cacheTime;
        private CachePolicy? _cachePolicy;
        private WeakReference _valueReference = null;
        private object _rootedValue = null;

        // stats
        //
        public EntryStats Stats { get; set; }

        // refresh and update state 
        //
        private DateTime _lastUpdatedTime;
        private DateTime expirationTime;

        // these guys manage the loading of values from the 
        // cache or from the wire.
        //
        private CacheValueLoader _cacheLoader;
        private LiveValueLoader _liveLoader;

        // we cache policies based on type for perf 
        //
        // TODO: replace with Cache<T, T2>
        static Dictionary<Type, CachePolicyAttribute> _cachedPolicies = new Dictionary<Type, CachePolicyAttribute>();

        /// <summary>
        /// The cache policy for this item
        /// </summary>
        public CachePolicy CachePolicy
        {
            get
            {
                EnsureCachePolicy();
                return _cachePolicy.Value;
            }
        }

        /// <summary>
        /// The intended cache lifetime for this item.
        /// </summary>
        private TimeSpan CacheTime
        {
            get
            {
                // TODO: What does this do?
                EnsureCachePolicy();
                return _cacheTime.Value;
            }
        }

        // Flag for when value is being fetched.
        //
        private bool GettingValue
        {
            get
            {
                return GetBoolValue(GettingValueMask);
            }
            set
            {
                SetBoolValue(GettingValueMask, value);
            }
        }

        private bool HaveWeEverGottenALiveValue
        {
            get
            {
                return GetBoolValue(LiveValueSuccessMask);
            }
            set
            {
                SetBoolValue(LiveValueSuccessMask, value);
            }
        }

        /// <summary>
        /// Checks if the current data is valid and should be returned to the caller.
        /// </summary>
        private bool IsDataValid
        {
            get
            {
                if (CachePolicy == AgFx.CachePolicy.NoCache)
                {
                    return false;
                }
                if (GetBoolValue(UsingCachedValueMask) && _cacheLoader.IsValid)
                {
                    return true;
                }
                else if (expirationTime > DateTime.Now)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// The "version" of the data that's being held.  This is sometimes incremented
        /// to allow calls to know if they've been pre-empted.
        /// </summary>
        private int Version
        {
            get
            {
                return (int)(_bitfield & VersionMask);
            }
            set
            {
                lock (this)
                {
                    int data = _bitfield & ~VersionMask;

                    _bitfield = data | (value & VersionMask);
                }
            }
        }

        /// <summary>
        /// Makes all load operations happen synchronously for LoadFromCache
        /// </summary>
        private bool SynchronousMode
        {
            get
            {
                return GetBoolValue(SynchronousModeMask);
            }
            set
            {
                SetBoolValue(SynchronousModeMask, value);
            }
        }

        /// <summary>
        /// The last time this object's data was updated.
        /// </summary>
        private DateTime LastUpdatedTime
        {
            get
            {
                return _lastUpdatedTime;
            }
            set
            {
                _lastUpdatedTime = value;
                UpdateLastUpdated();
            }
        }

        // TODO: Should this be somewhere else -> Type extension method
        internal static string BuildUniqueName(Type objectType, LoadContext context)
        {
            return string.Format("{0}_{1}", objectType.Name, context.UniqueKey);
        }

        /// <summary>
        /// The unique name for this ObjectType + Identifier combo.
        /// </summary>
        public string UniqueName
        {
            get
            {
                if (_uniqueName == null)
                {
                    _uniqueName = BuildUniqueName(ObjectType, LoadContext);
                }
                return _uniqueName;
            }
        }

        internal bool HasBeenGCd
        {
            get
            {
                return _valueReference != null && !_valueReference.IsAlive;
            }
        }

        /// <summary>
        /// Gets the object out of the WeakRef, with the ability to surpess any loads, etc.        
        /// </summary>
        /// <param name="load"></param>
        /// <param name="obj"></param>
        private void GetRootedObjectInternal(bool load, out object obj)
        {
            if (_valueReference == null ||
                   !_valueReference.IsAlive)
            {
                // Create a new value if necessary
                //
                bool isNew = _valueReference == null;
                obj = CreateDefaultValue();
                _valueReference = new WeakReference(obj);

                // if it's not new, that means we're resurrecting the object value
                // after it's been GC'd, so reset the state and let the load continue.
                //
                if (!isNew)
                {
                    Debug.WriteLine("A {0} (ID={1}) value has been GC'd, reloading.", ObjectType.Name, LoadContext.Identity);
                    _cacheLoader.Reset();
                    _liveLoader.Reset();
                    SetBoolValue(UsingCachedValueMask, false);
                    expirationTime = DateTime.MinValue;
                    HaveWeEverGottenALiveValue = false;
                }

                if (load)
                {
                    Load(false);
                }
            }
            else
            {
                // otherwise jut grab the value.
                //
                obj = _valueReference.Target;
            }
        }

        // Retrieves the value without queuing any loads.
        //
        public object ValueInternal
        {
            get
            {
                object obj;
                GetRootedObjectInternal(false, out obj);
                return obj;
            }
            set
            {
                if (_valueReference == null)
                {
                    _valueReference = new WeakReference(value);
                }
            }
        }

        /// <summary>
        /// Retrieve the value and do the right loading stuff 
        /// </summary>
        public object GetValue(bool cacheOnly)
        {
            if (GettingValue)
            {
                return ValueInternal;
            }

            try
            {
                GettingValue = true;
                object value;
                lock (this)
                {
                    GetRootedObjectInternal(false, out value);

                    Stats.OnRequest();

                    if (IsDataValid)
                    {
                        // do nothing, we're done!
                        //
                        NotifyCompletion(null, null);
                    }
                    else if (!cacheOnly)
                    {
                        // Data is out of date or not valid - kick off a new load.
                        //
                        Load(false);
                    }
                }
                Debug.Assert(value != null, "Fail: returning a null value!");
                return value;
            }
            finally
            {
                GettingValue = false;
            }
        }

        /// <summary>
        /// Set up all the handlers for this CacheEntry
        /// </summary>
        /// <param name="objectType"></param>
        /// <param name="context"></param>
        /// <param name="proxyCallback">callback that should be invoked when update is finished</param>
        public CacheEntry(LoadContext context, Type objectType)
        {
            LoadContext = context;
            ObjectType = objectType;

            Stats = new EntryStats(this);

            // set up our value loaders.
            //
            _cacheLoader = new CacheValueLoader(this);
            _cacheLoader.Loading += ValueLoader_Loading;
            _cacheLoader.ValueAvailable += Cached_ValueAvailable;
            _cacheLoader.LoadFailed += CacheLoader_Failed;

            _liveLoader = new LiveValueLoader(this);
            _liveLoader.Loading += ValueLoader_Loading;
            _liveLoader.ValueAvailable += Live_ValueAvailable;
            _liveLoader.LoadFailed += LiveValueLoader_Failed;

            NextCompletedAction = new UpdateCompletionHandler(this);
        }

        // Helpers for accessing bit field.
        //
        private bool GetBoolValue(int mask)
        {
            return (_bitfield & mask) != 0;
        }

        private void SetBoolValue(int mask, bool value)
        {
            if (value)
            {
                _bitfield |= mask;
            }
            else
            {
                _bitfield &= ~mask;
            }
        }

        // Checks to make sure our value hasn't been cleaned up.
        // if it has, stop doing work because no one is holding the value reference.
        //
        public bool CheckIfAnyoneCares()
        {
            bool doesAnyoneCare = _valueReference != null && _valueReference.IsAlive;

            if (!doesAnyoneCare)
            {
                Debug.WriteLine("Object has been GCd - stopping load.");
            }

            return doesAnyoneCare;
        }

        internal void DoRefresh()
        {
            if (CheckIfAnyoneCares())
            {
                SetForRefresh();
                Load(true);
            }
        }

        // Check to ensure we've loaded the cache policy.
        //
        private void EnsureCachePolicy()
        {
            if (_cachePolicy == null || _cacheTime == null)
            {
                Debug.Assert(ObjectType != null, "Can't get policy before debug type is set.");
                CachePolicyAttribute cpa;
                if (!_cachedPolicies.TryGetValue(ObjectType, out cpa))
                {
                    // check the cache policy
                    //
                    var cpattributes = ObjectType.GetTypeInfo().GetCustomAttributes(typeof(CachePolicyAttribute), true);

                    cpa = (CachePolicyAttribute)cpattributes.FirstOrDefault();

                    if (cpa == null)
                    {
                        cpa = CachePolicyAttribute.Default;
                    }
                    _cachedPolicies[ObjectType] = cpa;
                }

                _cachePolicy = cpa.CachePolicy;

                if (_cachePolicy == CachePolicy.NoCache)
                {
                    _cacheTime = TimeSpan.Zero;
                }
                else
                {
                    _cacheTime = TimeSpan.FromSeconds(cpa.CacheTimeInSeconds);
                }

            }
        }

        /// <summary>
        /// Callback that fires when the LiveValueLoader has completed it's load and is handing
        /// us a new value.
        /// </summary>
        void Live_ValueAvailable(object sender, ValueAvailableEventArgs e)
        {
            // is this the same value we already have?
            //
            if (e.UpdateTime == LastUpdatedTime)
            {
                return;
            }

            // update update and expiration times.
            //
            var value = e.Value;

            ValueLoader loader = (ValueLoader)sender;

            UpdateExpiration(e.UpdateTime, value as ICachedItem);

            if (value != null)
            {
                UpdateFrom(loader, value);
            }
            else
            {
                // not clear what to do with null values.
                //
                NotifyCompletion(loader, null);
                return;
            }

            HaveWeEverGottenALiveValue = true;

            // We are no longer using the cached value.
            //
            SetBoolValue(UsingCachedValueMask, false);

            // as long as this thing isn't NoCache, write
            // it to the store.
            //
            if (CachePolicy != AgFx.CachePolicy.NoCache)
            {
                SerializeDataToCache(value, _liveLoader.UpdateTime, expirationTime, false);
            }
        }

        private void ProxyCompletion()
        {
            // TODO: Cast to array first to prevent changes causing issues?
            foreach (var proxy in ProxyManager.GetProxies(LoadContext, ObjectType))
            {
                // copy the values over
                //
                ReflectionSerializer.UpdateObject(ValueInternal, proxy.ProxyReference.Target, true, null);

                // fire the update notification
                //
                if (proxy.UpdateAction != null)
                {
                    proxy.UpdateAction();
                }
            }
        }

        public bool SerializeDataToCache(object value, DateTime updateTime, DateTime? expirationTime, bool optimizedOnly)
        {
            bool isOptimized = false;
            byte[] data = null;

            // see if we can optimize first
            //
            if (SerializeOptimizedDataAction != null)
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    if (SerializeOptimizedDataAction(value, outputStream))
                    {
                        outputStream.Flush();
                        outputStream.Seek(0, SeekOrigin.Begin);
                        var bytes = new byte[outputStream.Length];
                        outputStream.Read(bytes, 0, bytes.Length);
                        isOptimized = true;
                        data = bytes;
                    }
                }
            }

            if (optimizedOnly && !isOptimized)
            {
                return false;
            }

            // oh well, no optimized stream, fall back
            // to normal data.
            if (data == null)
            {
                data = _liveLoader.Data;
            }

            if (expirationTime == null)
            {
                expirationTime = updateTime.Add(CacheTime);
            }

            // write the value out to the cache store.
            //
            _cacheLoader.Save(UniqueName, data, updateTime, expirationTime.Value, isOptimized);
            return true;
        }

        private void UpdateExpiration(DateTime lastUpdatedTime, ICachedItem cachedItem)
        {
            LastUpdatedTime = lastUpdatedTime;

            if (cachedItem == null || cachedItem.ExpirationTime == null)
            {
                expirationTime = LastUpdatedTime.Add(CacheTime);
            }
            else
            {
                expirationTime = cachedItem.ExpirationTime.Value;
            }
        }

        /// <summary>
        /// Updates the live value from  an inentional DataManager.Save operation.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="loadContext"></param>
        public void UpdateValue(object instance, LoadContext loadContext)
        {
            UpdateExpiration(DateTime.Now, instance as ICachedItem);

            LoadContext = loadContext;
            if (_valueReference != null && _valueReference.IsAlive)
            {
                UpdateFrom(null, instance);
            }
            else
            {
                ValueInternal = instance;
            }
        }

        /// <summary>
        /// The CacheLoader has finished loading and is handing us a new value.
        /// </summary>
        void Cached_ValueAvailable(object sender, ValueAvailableEventArgs e)
        {
            // live loader has data, so we don't care about it anymore.
            //
            if (HaveWeEverGottenALiveValue)
            {
                NextCompletedAction.UnregisterLoader(LoaderType.CacheLoader);
                return;
            }

            // copy the cached value into our state.
            //
            UpdateFrom((ValueLoader)sender, e.Value);
            SetBoolValue(UsingCachedValueMask, true);


            UpdateExpiration(e.UpdateTime, e.Value as ICachedItem);
        }

        async void CacheLoader_Failed(object s, ExceptionEventArgs e)
        {
            // if the cache load failed, make sure we're doing a live load.
            // if we aren't, kick one off.
            //
            NextCompletedAction.UnregisterLoader(LoaderType.CacheLoader);
            if (!_liveLoader.IsBusy && !HaveWeEverGottenALiveValue)
            {
                NextCompletedAction.RegisterActiveLoader(LoaderType.LiveLoader);
                await _liveLoader.FetchData();
            }
        }

        void LiveValueLoader_Failed(object s, ExceptionEventArgs e)
        {
            NotifyCompletion((ValueLoader)s, e.Exception);
        }

        void ValueLoader_Loading(object s, EventArgs e)
        {
            IUpdatable iupd = ValueInternal as IUpdatable;

            if (iupd != null)
            {
                iupd.IsUpdating = true;
            }
        }

        /// <summary>
        /// Initiate a load
        /// </summary>
        /// <param name="force">True to always load a new value.</param>
        private void Load(bool force)
        {
            lock (this)
            {
                // someone is already trying to do a load.
                if (GetBoolValue(LoadPendingMask) || _cacheLoader.IsBusy || _liveLoader.IsBusy || !_liveLoader.IsValid)
                {
                    return;
                }
                SetBoolValue(LoadPendingMask, true);

                // root the object value so that it doesn't get GC'd while we're processing.
                //
                GetRootedObjectInternal(false, out _rootedValue);
            }

            // Conciously don't wait here, we want to make sure we do a load but don't block the UI thread at all
            // TODO: Disable warnings
            PriorityQueue.AddWorkItem(Task.Run(() =>
                LoadInternal(force)
            ));
        }

        /// <summary>
        /// Synchronously load a value from the cache and return it.
        /// </summary>
        /// <returns></returns>
        public async Task<object> LoadFromCache()
        {
            try
            {
                SynchronousMode = true;
                _cacheLoader.SynchronousMode = true;

                // only return a valid cache.
                if (_cacheLoader.IsValid)
                {
                    object value = ValueInternal;
                    // TODO StartLoading could return something, instead of mutating state?
                    await StartLoading(_cacheLoader);
                    return value;
                }
            }
            finally
            {
                _cacheLoader.SynchronousMode = false;
                SynchronousMode = false;
            }
            return null;
        }

        /// <summary>
        /// LoadInternal does the heavy lifting of the load.
        /// </summary>
        /// <param name="force"></param>
        private async Task LoadInternal(bool force)
        {
            try
            {
                // first to see if we have a valid live value.
                //
                if (!force && DateTime.Now < expirationTime)
                {
                    // somehow we got in here, so do nothing.
                    //
                    return;
                }
                else if (force || HaveWeEverGottenALiveValue)
                {
                    // we had a value but it expired, kick off a new live load.
                    //
                    if (!_liveLoader.IsBusy)
                    {
                        Debug.WriteLine("{0}: Data for {1} (ID={2}) has expired, reloading.", DateTime.Now, ObjectType.Name, LoadContext.Identity);
                        await StartLoading(_liveLoader);
                    }
                    return;
                }
                else if (GetBoolValue(UsingCachedValueMask) && !_cacheLoader.IsValid)
                {
                    Debug.WriteLine("{0}: Failed cache load for {1} (ID={2}) reloading live data.", DateTime.Now, ObjectType.Name, LoadContext.Identity);

                    await StartLoading(_liveLoader);
                    return;
                }
                else if (_cacheLoader.LoadState == DataLoadState.ValueAvailable)
                {
                    // we already loaded the cache and nothing has changed, so do nothing.
                    //                
                    return;
                }

                lock (this)
                {
                    // this is the initial load state, so we check the cache then figure
                    // out what to do.

                    // first check the cache state.
                    //
                    var isCacheValid = _cacheLoader.IsValid;

                    if (!isCacheValid)
                    {
                        // if the cache is NOT valid, figure out what to do about
                        // it based on the cache policy.
                        //

                        // start a live load.
                        StartLoading(_liveLoader).Wait();

                        switch (CachePolicy)
                        {
                            case CachePolicy.NoCache:
                            case CachePolicy.ValidCacheOnly:
                                return;
                            case CachePolicy.CacheThenRefresh:
                            case CachePolicy.Forever:
                                // fall through to kick off a cache load.
                                break;
                        }
                    }


                    Debug.WriteLine("{0}: Checking cache for {1} (ID={2})", DateTime.Now, ObjectType.Name, LoadContext.Identity);
                    try
                    {
                        if (_cacheLoader.IsCacheAvailable)
                        {
                            StartLoading(_cacheLoader).Wait();
                        }
                    }
                    catch
                    {
                        if (isCacheValid)
                        {
                            Debug.WriteLine("{0}: Error cache for {1} (ID={2}), reloading", DateTime.Now, ObjectType.Name, LoadContext.Identity);
                            StartLoading(_liveLoader).Wait();
                        }
                    }
                }
            }
            finally
            {
                SetBoolValue(LoadPendingMask, false);
            }
        }

        /// <summary>
        /// Start loading operation
        /// </summary>
        /// <param name="loader">loader that should be invoked</param>
        private async Task StartLoading(ValueLoader loader)
        {
            NextCompletedAction.RegisterActiveLoader(loader.LoaderType);
            try
            {
                await loader.FetchData();
            }
            catch (Exception)
            {
                NextCompletedAction.UnregisterLoader(loader.LoaderType);
                throw;
            }
        }

        /// <summary>
        /// Notify any listeners of completion of the load, or of any exceptions
        /// that occurred during loading.
        /// </summary>
        /// <param name="loader"></param>
        /// <param name="ex"></param>
        private void NotifyCompletion(ValueLoader loader, Exception ex)
        {
            IUpdatable iupd = ValueInternal as IUpdatable;

            if (iupd != null)
            {
                iupd.IsUpdating = false;
            }

            LoaderType loaderType = loader != null ? loader.LoaderType : LoaderType.CacheLoader;

            //  UpdateCompletionHandler makes sure to call handler on UI thread
            //
            try
            {
                if (ex == null)
                {
                    NextCompletedAction.OnSuccess(loaderType);
                    ProxyCompletion();
                }
                else
                {
                    NextCompletedAction.OnError(loaderType, ex);
                }
            }
            finally
            {
                // free our value root
                //
                _rootedValue = null;
            }
        }

        /// <summary>
        /// Updates the object value from a source object of the same type.        
        /// </summary>
        /// <param name="loader"></param>
        /// <param name="source"></param>
        private void UpdateFrom(ValueLoader loader, object source)
        {
            Version++;

            // if no one is holding the value, don't bother updating.
            //
            if (!CheckIfAnyoneCares())
            {
                return;
            }

            int version = Version;

            object value = ValueInternal;

            // sure source matches dest
            //
            if (!value.GetType().IsInstanceOfType(source))
            {
                throw new InvalidOperationException("Types not compatible");
            }

            Action handler = () =>
                {
                    // make sure another update hasn't beat us to the punch.
                    if (Version > version)
                    {
                        return;
                    }

                    try
                    {

                        Stats.OnStartUpdate();

                        ReflectionSerializer.UpdateObject(source, value, true, LastUpdatedTime);
                    }
                    finally
                    {
                        Stats.OnCompleteUpdate();
                    }
                    // notify successful completion.
                    NotifyCompletion(loader, null);
                };

            if (SynchronousMode)
            {
                handler();
            }
            else
            {
                PriorityQueue.AddUiWorkItem(
                   handler
                );
            }
        }

        /// <summary>
        /// Update the LastUpdated field of our IUpdateble value, 
        /// on the UI thread (it may be bound to UI)
        /// </summary>
        private void UpdateLastUpdated()
        {
            if (CheckIfAnyoneCares())
            {
                IUpdatable updateable = ValueInternal as IUpdatable;

                if (updateable != null)
                {
                    PriorityQueue.AddUiWorkItem(
                        () =>
                        {
                            updateable.LastUpdated = LastUpdatedTime;
                        }
                   );
                }
            }
        }

        /// <summary>
        /// Sets this item into an expired state in order to force a refresh
        /// on the next fetch.
        /// </summary>
        public void SetForRefresh()
        {
            expirationTime = DateTime.Now;

            if (!_liveLoader.IsValid)
            {
                _liveLoader.Reset();
            }

            // Set the cache loader as expired, which will
            // essentially stop cache loads for this item going forward.
            //
            _cacheLoader.SetExpired();
        }

        /// <summary>
        /// Clear the cache for this item.
        /// </summary>
        public async Task ClearAsync()
        {
            await DataManager.StoreProvider.DeleteAllAsync(UniqueName);
        }

        /// <summary>
        ///  cache loaders by type.
        /// </summary>
        private static readonly Dictionary<Type, object> _loaders = new Dictionary<Type, object>();

        /// <summary>
        /// Figure out what dataloader to use for the entry type.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public object GetDataLoader()
        {
            object loader;

            lock (_loaders)
            {
                if (_loaders.TryGetValue(ObjectType, out loader))
                {
                    return loader;
                }
            }

            var attrs = ObjectType.GetTypeInfo().GetCustomAttributes(typeof(DataLoaderAttribute), true);

            if (attrs.Any())
            {
                DataLoaderAttribute dla = (DataLoaderAttribute)attrs.First();

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
                for (Type modelType = ObjectType;
                    loader == null && modelType != typeof(object);
                    modelType = modelType.GetTypeInfo().BaseType)
                {

                    // see if we already have a loader at this level
                    //
                    if (_loaders.TryGetValue(modelType, out loader) && loader != null)
                    {
                        break;
                    }

                    // TODO: Refactor this into reflection helper
                    var loaders = from nt in modelType.GetTypeInfo().DeclaredNestedTypes
                                  where nt.ImplementedInterfaces.Any(i => i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(IDataLoader<>))
                                  select nt;

                    var loaderTypeInfo = loaders.FirstOrDefault();

                    if (loaderTypeInfo != null)
                    {
                        loader = Activator.CreateInstance(loaderTypeInfo.AsType());

                        // if we're walking base types, save this value so that the subsequent requests will get it.
                        //
                        if (loader != null && modelType != ObjectType)
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
                    _loaders[ObjectType] = loader;
                    return loader;
                }
            }
            throw new InvalidOperationException(String.Format("DataLoader not specified for type {0}.  All DataManager-loaded types must implement a data loader by one of the following methods:\r\n\r\n1. Specify a IDataLoader-implementing type on the class with the DataLoaderAttribute.\r\n2. Create a public nested type that implements IDataLoader.", ObjectType.Name));
        }

        private object CreateDefaultValue()
        {
            var proxy = ProxyManager.GetProxies(LoadContext, ObjectType).FirstOrDefault();

            if (proxy != null && proxy.ProxyReference != null && proxy.ProxyReference.IsAlive)
            {
                return proxy.ProxyReference.Target;
            }

            var ci = ObjectType.GetDefaultConstructor();

            var item = ci.Invoke(null);

            if (item is ILoadContextItem)
            {
                ((ILoadContextItem)item).LoadContext = LoadContext;
            }
            return item;
        }

        public object DeserializeAction(LoadContext id, Stream data, bool isOptimized)
        {
            var loader = GetDataLoader();
            if (loader == null) throw new InvalidOperationException("Could not find loader for type: " + ObjectType.Name);

            // get the loader and ask for deserialization
            //                
            object deserializedObject = null;

            if (isOptimized)
            {
                var idl = (IDataOptimizer)loader;

                if (idl == null)
                {
                    throw new InvalidOperationException("Data is optimized but object does not implmenent IDataOptimizer");
                }
                deserializedObject = idl.DeserializeOptimizedData(LoadContext, ObjectType, data);
            }
            else
            {
                deserializedObject = DataLoaderProxy.Deserialize(loader, LoadContext, ObjectType, data);
            }

            if (deserializedObject == null)
            {
                throw new InvalidOperationException(String.Format("Deserialize returned null for {0}, id='{1}'", ObjectType.Name, id));
            }

            if (!ObjectType.IsInstanceOfType(deserializedObject))
            {
                throw new InvalidOperationException(String.Format("Returned object is {0} when {1} was expected", deserializedObject.GetType().Name, ObjectType.Name));
            }
            return deserializedObject;
        }

        public void Dispose()
        {
        }
    }
}