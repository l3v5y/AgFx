using Nito.AsyncEx;
using System;
using System.Threading.Tasks;

namespace AgFx
{
    /// <summary>
    /// Base ValueLoader class.  This class knows the basic steps
    /// for loading a value and keeping track of where it's at in the load
    /// lifecycle.
    /// </summary>
    internal abstract class ValueLoader
    {
        protected readonly AsyncReaderWriterLock readWriteLock = new AsyncReaderWriterLock();
        /// <summary>
        /// The CacheEntry this ValueLoader is associated with.
        /// </summary>
        public ICacheEntry CacheEntry
        {
            get;
            private set;
        }

        public UpdateCompletionHandler NextCompletedAction { get; set; }

        public event EventHandler Loading;
        public event EventHandler<ValueAvailableEventArgs> ValueAvailable;
        public event EventHandler<ExceptionEventArgs> LoadFailed;

        public ValueLoader(ICacheEntry owningEntry)
        {
            CacheEntry = owningEntry;
        }

        public DataLoadState LoadState
        {
            get;
            protected set;
        }

        /// <summary>
        /// The raw data associated with this loader.
        /// </summary>
        public byte[] Data
        {
            get;
            protected set;
        }

        /// <summary>
        /// The loader is busy if it is in a load or a process
        /// action.
        /// </summary>
        public bool IsBusy
        {
            get
            {
                switch (LoadState)
                {
                    case DataLoadState.None:
                    case DataLoadState.Failed:
                    case DataLoadState.ValueAvailable:
                        return false;
                    default:
                        return true;
                }
            }
        }

        /// <summary>
        /// Is this loader in a valid state?
        /// </summary>
        public abstract bool IsValid
        {
            get;
        }

        /// <summary>
        /// Gets type of the loader
        /// </summary>
        public abstract LoaderType LoaderType { get; }

        /// <summary>
        /// Fetch the data for this loader.
        /// </summary>
        public async Task FetchData()
        {
            NextCompletedAction = CacheEntry.NextCompletedAction;

            // make sure we're not already in a loading state.
            //
            switch (LoadState)
            {
                case DataLoadState.Loading:
                case DataLoadState.Processing:
                    return;
                case DataLoadState.Loaded:
                    FireLoading();
                    ProcessData();
                    return;
            }

            LoadState = DataLoadState.Loading;

            try
            {
                // kick off the derived class's load
                if (!await FetchDataCore())
                {
                    LoadState = DataLoadState.None;
                }
                else
                {
                    FireLoading();
                }
            }
            catch (Exception ex)
            {
                LoadState = DataLoadState.Failed;
                OnLoadFailed(ex);
            }
        }

        protected abstract Task<bool> FetchDataCore();

        /// <summary>
        /// Fire the Loading event.
        /// </summary>
        private void FireLoading()
        {
            if (Loading != null)
            {
                Loading(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// We have data, now deserialize it.
        /// </summary>
        protected void ProcessData()
        {
            if (LoadState == DataLoadState.Processing)
            {
                return;
            }
            LoadState = DataLoadState.Processing;

            var data = Data;

            try
            {
                if (data != null)
                {
                    if (!CacheEntry.CheckIfAnyoneCares())
                    {
                        // no one is listening, so just quit.
                        LoadState = DataLoadState.Loaded;
                        return;
                    }

                    var value = ProcessDataCore(data);

                    // copy the value.
                    //          
                    OnValueAvailable(value, DateTime.MinValue);
                    Data = null;
                }
            }
            catch (Exception ex)
            {
                OnLoadFailed(ex);
                LoadState = DataLoadState.Failed;
                return;
            }
        }

        protected abstract object ProcessDataCore(byte[] data);

        // Fire the load failed event
        //
        protected void OnLoadFailed(Exception ex)
        {
            LoadState = DataLoadState.Failed;
            if (LoadFailed != null)
            {
                LoadFailed(this,
                    new ExceptionEventArgs()
                    {
                        Exception = ex
                    }
                );
            }
        }

        protected virtual void OnValueAvailable(object value, DateTime updateTime)
        {
            LoadState = DataLoadState.ValueAvailable;

            if (ValueAvailable != null)
            {
                ValueAvailable(this, new ValueAvailableEventArgs()
                {
                    Value = value,
                    UpdateTime = updateTime
                });
            }
        }

        // Reset the state of this Loader
        public virtual void Reset()
        {
            LoadState = DataLoadState.None;
            Data = null;
        }
    }
}
