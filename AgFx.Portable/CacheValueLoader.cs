using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AgFx
{
    /// <summary>
    /// Loader responsible for managing loads from the cache.
    /// </summary>
    internal class CacheValueLoader : ValueLoader
    {
        CacheItemInfo _cacheItemInfo;
        bool _thereIsNoCacheItem;
        AsyncLock asyncLockObject = new AsyncLock();
        public bool IsCacheAvailable
        {
            get
            {
                return LoadState != DataLoadState.Failed && FindCacheItem().Result;
            }
        }

        public override bool IsValid
        {
            get
            {
                // Valid if we're not in a failed state and there is a cached value to 
                // use that is not expired.
                //
                if (LoadState != DataLoadState.Failed && FindCacheItem().Result)
                {
                    return _cacheItemInfo != null && _cacheItemInfo.ExpirationTime > DateTime.Now;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets type of the loader
        /// </summary>
        public override LoaderType LoaderType
        {
            get { return LoaderType.CacheLoader; }
        }

        public bool SynchronousMode
        {
            get;
            set;
        }

        public CacheValueLoader(CacheEntry owningEntry)
            : base(owningEntry)
        {

        }

        /// <summary>
        /// Look in the store for a recent entry that we can load from.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> FindCacheItem()
        {
            using (await asyncLockObject.LockAsync())
            {
                if (_cacheItemInfo == null && !_thereIsNoCacheItem)
                {
                    _cacheItemInfo = await DataManager.StoreProvider.GetItemAsync(CacheEntry.UniqueName);
                }

                if (_cacheItemInfo == null)
                {
                    // flat failure.
                    if (!_thereIsNoCacheItem)
                    {
                        _thereIsNoCacheItem = true;
                        Debug.WriteLine("No cache found for {0} (ID={1})", CacheEntry.ObjectType, CacheEntry.LoadContext.Identity);
                    }
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Pull the data off the store
        /// </summary>
        protected override async Task<bool> FetchDataCore()
        {
            // First check if we have cached data.
            //                
            if (!await FindCacheItem())
            {
                // nope, nevermind.
                return false;
            }

            // if we have cached data, then mark ourself as loadable and load it up.
            //
            Debug.WriteLine(String.Format("{3}: Loading cached data for {0} (ID={4}), Last Updated={1}, Expiration={2}", CacheEntry.ObjectType.Name, _cacheItemInfo.UpdatedTime, _cacheItemInfo.ExpirationTime, DateTime.Now, CacheEntry.LoadContext.Identity));

            // load it up.
            //
            try
            {
                Data = await DataManager.StoreProvider.ReadAsync(_cacheItemInfo);


                if (Data == null)
                {
                    OnLoadFailed(new InvalidOperationException("The cache returned no data."));
                    return false;
                }

                LoadState = DataLoadState.Loaded;
                ProcessData();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Cache load failed for {0} (ID={2}): {1}", CacheEntry.ObjectType.Name, ex.ToString(), CacheEntry.LoadContext.Identity);
                OnLoadFailed(ex);
                return false;
            }
        }

        protected override void OnValueAvailable(object value, DateTime updateTime)
        {
            // substitute the cache's last updated time.
            //
            base.OnValueAvailable(value, _cacheItemInfo.UpdatedTime);
        }

        protected override object ProcessDataCore(byte[] data)
        {
            // check to see if this is an optimized cache 
            //
            bool isOptimized = _cacheItemInfo.IsOptimized;

            Debug.WriteLine("{0}: Deserializing cached data for {1} (ID={3}), IsOptimized={2}", DateTime.Now, CacheEntry.ObjectType, isOptimized, CacheEntry.LoadContext.Identity);
            using (var stream = new MemoryStream(data))
            {

                try
                {
                    CacheEntry.Stats.OnStartDeserialize();
                    return CacheEntry.DeserializeAction(CacheEntry.LoadContext, stream, isOptimized);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("{0}: Exception cached data for {1} (ID={2}) Exception=({3})", DateTime.Now, CacheEntry.ObjectType, CacheEntry.LoadContext.Identity, ex);
                    CacheEntry.Stats.OnDeserializeFail();
                    throw;
                }
                finally
                {
                    CacheEntry.Stats.OnCompleteDeserialize(data.Length);
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            _cacheItemInfo = null;
            _thereIsNoCacheItem = false;
        }

        /// <summary>
        /// Save the specified value back to the disk.
        /// </summary>
        /// <param name="uniqueName"></param>
        /// <param name="data"></param>
        /// <param name="updatedTime"></param>
        /// <param name="expirationTime"></param>
        /// <param name="isOptimized"></param>
        public void Save(string uniqueName, byte[] data, DateTime updatedTime, DateTime expirationTime, bool isOptimized)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            _cacheItemInfo = new CacheItemInfo(uniqueName, updatedTime, expirationTime);
            _cacheItemInfo.IsOptimized = isOptimized;
            Data = null;
            LoadState = DataLoadState.None;

            Debug.WriteLine("Writing cache for {0} (ID={3}), IsOptimized={1}, Will expire {2}", CacheEntry.ObjectType.Name, _cacheItemInfo.IsOptimized, _cacheItemInfo.ExpirationTime, CacheEntry.LoadContext.Identity.ToString());
            DataManager.StoreProvider.WriteAsync(_cacheItemInfo, data);
        }

        internal void SetExpired()
        {
            // mark this as a failed load to prevent it from being used in the future.
            //
            LoadState = DataLoadState.Failed;
        }
    }
}
