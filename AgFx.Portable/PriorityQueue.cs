// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.


using Punchclock;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgFx
{
    /// <summary>
    /// Queing mechanism for different types of work items. Use this instead of DispatcherBeginInvoke, 
    /// ThreadPool, etc.  
    /// </summary>
    public class PriorityQueue
    {
        static OperationQueue workQueue = new OperationQueue(4);
        private const int StoragePriority = 10;
        private const int NetworkPriority = 1;
        private const int GeneralWorkPriority = 5;

        public static bool IsOnUiThread
        {
            get { return Dispatcher.IsOnUiThread; }
        }

        static PriorityQueue()
        {
        }

        public static void Initialize(IDispatcher dispatcher)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException("context");
            }
            Dispatcher = dispatcher;
        }

        private static IDispatcher Dispatcher;

       /// <summary>
        /// Add a work item to be performed on the UI thread asynchrously.
        /// </summary>
        /// <param name="workitem"></param>
        public static void AddUiWorkItem(Action workItem)
        {
            AddUiWorkItem(workItem, false);
        }

        /// <summary>
        /// Add a work item to execute on the UI thread.
        /// </summary>
        /// <param name="workitem">The Action to execute</param>
        /// <param name="checkThread">true to first check the thread, and if the thread is already the UI thread, execute the item synchrounously.</param>
        public static void AddUiWorkItem(Action workitem, bool checkThread)
        {
            if (Dispatcher == null)
            {
                throw new NullReferenceException("PriorityQueue must be initialised before being used");
            }

            if (checkThread && Dispatcher.IsOnUiThread)
            {
                workitem();
            }
            else
            {
                Dispatcher.BeginInvoke(workitem);
            }
        }

        /// <summary>
        /// Add a work item that will affect storage
        /// </summary>
        /// <param name="workItem"></param>
        public static async Task AddStorageWorkItem(Task workItem)
        {
            await workQueue.Enqueue(StoragePriority, () => workItem);
        }

        /// <summary>
        /// Add a general work item.
        /// </summary>
        /// <param name="workitem"></param>
        public static async Task AddWorkItem(Task workitem)
        {
            await workQueue.Enqueue(GeneralWorkPriority, () => workitem);
        }

        /// <summary>
        /// Add a work item that will result in a network requeset
        /// </summary>
        /// <param name="workitem"></param>
        public static async Task AddNetworkWorkItem(Task workitem)
        {
            await workQueue.Enqueue(NetworkPriority, () => workitem);
        }
    }
}
