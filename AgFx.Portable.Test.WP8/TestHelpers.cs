using System.Threading;
using System.Windows;

namespace AgFx.Test
{
    public class TestHelpers
    {
        public static void InitializePriorityQueue()
        {
            var manualResetEvent = new ManualResetEvent(false);
#if WINDOWS_PHONE
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                PriorityQueue.Initialize(new Dispatcher());
                manualResetEvent.Set();
            });
#else
            PriorityQueue.Initialize(new Dispatcher());
            manualResetEvent.Set();
#endif
            manualResetEvent.WaitOne();
        }
    }
}
