using AgFx.Test.Mocks;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using System.Threading;
using System.Threading.Tasks;

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
        public async Task LoadRequestCanExecute()
        {
            var mockDataLoader = new ShortCacheObject.SCODataLoader();
            var loadRequest = DataLoaderProxy.GetLoadRequest(mockDataLoader, new LoadContext("mock"), typeof(ShortCacheObject));
            Assert.IsInstanceOfType(loadRequest, typeof(ShortCacheObject.SCOLoadRequest));

            var result = await loadRequest.Execute();

            Assert.IsNotNull(result);
            Assert.IsNull(result.Error);
            Assert.AreEqual(19, result.Stream.Length);
        }
    }
}