using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgFx
{
    public sealed class WorkerThread : IWorkerThread, IDisposable
    {
        private const int MaximumConcurrentTasks = 4;
        private SemaphoreSlim semaphore = new SemaphoreSlim(MaximumConcurrentTasks);

        public int CurrentlyExecutingTasks
        {
            get
            {
                return MaximumConcurrentTasks - semaphore.CurrentCount;
            }
        }

        private void ProcessAsync(Action action)
        {
            Task.Run(() =>
            {
                semaphore.Wait();
                try
                {
                    Task.Run(action).Wait();
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        public void AddWorkItem(Action action)
        {
            ProcessAsync(action);
        }

        public void Dispose()
        {
            semaphore.Dispose();
        }
    }
}