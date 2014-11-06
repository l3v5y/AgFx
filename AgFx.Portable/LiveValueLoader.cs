using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AgFx
{
    /// <summary>
    /// Loader responsible for loading new data.
    /// </summary>
    internal class LiveValueLoader : ValueLoader
    {
        private static TimeSpan RetryTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// If a load fails, we wait 60 seconds before retrying it.  The avoids
        /// reload loops.
        /// </summary>
        private DateTime? _loadRetryTime;

        public override bool IsValid
        {
            get
            {
                if (LoadState == DataLoadState.Failed && _loadRetryTime.GetValueOrDefault() > DateTime.Now)
                {
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Gets type of the loader
        /// </summary>
        public override LoaderType LoaderType
        {
            get { return LoaderType.LiveLoader; }
        }

        public DateTime UpdateTime
        {
            get;
            private set;
        }

        public LiveValueLoader(ICacheEntry entry)
            : base(entry)
        {
        }

        // TODO: Return data from this, then have FetchData handle it?
        protected override Task<bool> FetchDataCore()
        {
            if (!IsValid)
            {
                return Task.FromResult<bool>(false);
            }

            Debug.WriteLine("{0}: Queuing load for {1} (ID={2})", DateTime.Now, CacheEntry.ObjectType.Name, CacheEntry.LoadContext.Identity);
            CacheEntry.Stats.OnStartFetch();

            var loader = CacheEntry.GetDataLoader();
            if (loader == null) throw new InvalidOperationException("Could not find loader for type: " + CacheEntry.ObjectType.Name);
            // get a loader and ask for a load request.
            //                
            Debug.Assert(loader != null, "Failed to get loader for " + CacheEntry.ObjectType.Name);
            var request = DataLoaderProxy.GetLoadRequest(loader, CacheEntry.LoadContext, CacheEntry.ObjectType);

            if (request == null)
            {
                Debug.WriteLine("{0}: Aborting load for {1}, ID={2}, because {3}.GetLoadRequest returned null.", DateTime.Now, CacheEntry.ObjectType.Name, CacheEntry.LoadContext.Identity, loader.GetType().Name);
                return Task.FromResult<bool>(false);
            }

            // fire off the load on a seperate thread
            // TODO: Disable warnings
            PriorityQueue.AddWorkItem(Task.Run(async () =>
            {
                DataManager.Current.IsLoading = true;
                LoadRequestResult result = await request.Execute();
                // TODO: Pass result into a OnLoadCompleted, that handles success/failure
                if (result == null)
                {
                    throw new ArgumentNullException("result", "Execute must return a LoadRequestResult value.");
                }
                if (result.Error == null)
                {
                    OnLoadSuccess(result.Stream);
                }
                else
                {
                    OnLoadFail(new LoadRequestFailedException(CacheEntry.ObjectType, CacheEntry.LoadContext, result.Error));
                }
                DataManager.Current.IsLoading = false;
            }));
            return Task.FromResult<bool>(true);
        }

        protected override object ProcessDataCore(byte[] data)
        {
            Debug.WriteLine("{0}: Deserializing live data for {1} (ID={2})", DateTime.Now, CacheEntry.ObjectType, CacheEntry.LoadContext.Identity);
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    CacheEntry.Stats.OnStartDeserialize();
                    return CacheEntry.DeserializeAction(CacheEntry.LoadContext, stream, false);
                }
                catch
                {
                    CacheEntry.Stats.OnDeserializeFail();
                    throw;
                }
                finally
                {
                    CacheEntry.Stats.OnCompleteDeserialize(data.Length);
                }
            }
        }

        internal void OnLoadSuccess(Stream result)
        {
            CacheEntry.Stats.OnCompleteFetch(true);
            UpdateTime = DateTime.Now;

            if (result != null)
            {
                byte[] bytes = new byte[result.Length];
                result.Read(bytes, 0, bytes.Length);
                Data = bytes;
            }
            LoadState = DataLoadState.Loaded;
            ProcessData();
        }

        internal void OnLoadFail(LoadRequestFailedException exception)
        {
            CacheEntry.Stats.OnCompleteFetch(false);
            // the live load failed, set our retry limit.
            //
            Debug.WriteLine("Live load failed for {0} (ID={2}) Message={1}", exception.ObjectType.Name, exception.Message, exception.LoadContext.Identity);
            _loadRetryTime = DateTime.Now.Add(RetryTimeout);
            LoadState = DataLoadState.Failed;
            OnLoadFailed(exception);
        }

        protected override void OnValueAvailable(object value, DateTime updateTime)
        {
            base.OnValueAvailable(value, UpdateTime);
        }

        public override void Reset()
        {
            _loadRetryTime = null;
            UpdateTime = DateTime.MinValue;
            base.Reset();
        }
    }
}
