#if WINDOWS_PHONE
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using System;
using System.IO;
using AgFx.Test.Mocks;
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

            loadRequest.Execute((result) => { 
                Assert.IsNotNull(result);
                Assert.IsNull(result.Error);
                Assert.Equals(19, result.Stream.Length);
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }
    }
}
