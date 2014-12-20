using System.Threading;
using System.Windows;
using Xunit;

namespace AgFx.Test
{
    public class WPUiDispatcherTests
    {
        private const int AsynchronousTestTimeout = 1000;

        [Fact]
        public void OffUiThread_IsOnUiThread_ReturnsFalse()
        {
            var uiDispatcher = new WPUiDispatcher();
            Assert.False(uiDispatcher.IsOnUiThread());
        }

        [Fact]
        public void OffUiThread_Dispatch_ExecutesOnUiThread()
        {
            var resetEvent = new ManualResetEvent(false);

            var uiDispatcher = new WPUiDispatcher();

            uiDispatcher.Dispatch(() =>
            {
                Assert.True(Deployment.Current.Dispatcher.CheckAccess());
                resetEvent.Set();
            });

            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void OnUiThread_IsOnUiThread_ReturnsTrue()
        {
            var resetEvent = new ManualResetEvent(false);

            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                var uiDispatcher = new WPUiDispatcher();
                Assert.True(uiDispatcher.IsOnUiThread());
                resetEvent.Set();
            });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void OnUiThread_Dispatch_ExecutesOnUiThread()
        {
            var resetEvent = new ManualResetEvent(false);

            var uiDispatcher = new WPUiDispatcher();

            uiDispatcher.Dispatch(() =>
            {
                Assert.True(Deployment.Current.Dispatcher.CheckAccess());
                resetEvent.Set();
            });

            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }
    }
}