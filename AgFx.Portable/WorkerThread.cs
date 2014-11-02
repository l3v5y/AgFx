using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AgFx
{
    public class WorkerThread : IWorkerThread, IDisposable
    {
        Task t;
        Queue<Action> q = new Queue<Action>();
        AutoResetEvent e = new AutoResetEvent(false);

        public string _name;
    
        public WorkerThread(int sleepyTime, string name)
        {
            _name = name;
            SleepyTime = sleepyTime;
            t = new Task(WorkerThreadProc);
            t.Start();
        }

        private async void WorkerThreadProc()
        {
            while (true)
            {
                {
                    IEnumerable<Action> workItems = null;

                    lock (q)
                    {
                        workItems = q.ToArray();
                        q.Clear();
                    }

                    foreach (var item in workItems)
                    {
                        if (item != null)
                        {
                            Task.Run(() =>
                            {
                                item();
                            });
                            await Task.Delay(SleepyTime);
                        }
                    }
                }
                e.WaitOne();
            }
        }

        public int SleepyTime { get; set; }
        public void AddWorkItem(Action a)
        {
            lock (q)
            {
                q.Enqueue(a);
            }
            e.Set();
        }

        public void Dispose()
        {
            e.Dispose();
        }
    }
}