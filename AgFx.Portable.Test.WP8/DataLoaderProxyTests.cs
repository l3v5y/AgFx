using AgFx.Test.Mocks;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using System.Threading;

namespace AgFx.Test
{
    [TestClass]
    public class DataLoaderProxyTests
    {
        [TestMethod]
        public void DataLoaderProxyCanGetLoadRequest()
        {
            var mockDataLoader = new ShortCacheObject.SCODataLoader();
            var loadRequest = DataLoaderProxy.GetLoadRequest(mockDataLoader, new LoadContext("mock"), typeof(ShortCacheObject));
            Assert.IsInstanceOfType(loadRequest, typeof(ShortCacheObject.SCOLoadRequest));
        }

        [TestMethod]
        public void LoadRequestCanExecute()
        {
            var resetEvent = new ManualResetEvent(false);
            var mockDataLoader = new ShortCacheObject.SCODataLoader();
            var loadRequest = DataLoaderProxy.GetLoadRequest(mockDataLoader, new LoadContext("mock"), typeof(ShortCacheObject));
            Assert.IsInstanceOfType(loadRequest, typeof(ShortCacheObject.SCOLoadRequest));

            loadRequest.Execute((result) =>
            {
                Assert.IsNotNull(result);
                Assert.IsNull(result.Error);
                Assert.Equals(19, result.Stream.Length);
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }
    }
}
