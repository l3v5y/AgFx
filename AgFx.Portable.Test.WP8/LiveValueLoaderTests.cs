using AgFx.Test.Mocks;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;

namespace AgFx.Test
{
    [TestClass]
    public class LiveValueLoaderTests
    {
        [TestMethod]
        public void LoaderTypeIsLiveLoader()
        {
            var loadContext = new LoadContext("LoaderType");
            var cacheEntry = new CacheEntry(loadContext, typeof(ModelItemBase));
            var liveValueLoader = new LiveValueLoader(cacheEntry);

            Assert.AreEqual(LoaderType.LiveLoader, liveValueLoader.LoaderType);
        }

        [TestMethod]
        public void DefaultLoadStateIsUnloaded()
        {
            var loadContext = new LoadContext("LoaderType");
            var cacheEntry = new CacheEntry(loadContext, typeof(ModelItemBase));
            var liveValueLoader = new LiveValueLoader(cacheEntry);

            Assert.AreEqual(DataLoadState.None, liveValueLoader.LoadState);
        }

        [TestMethod]
        public async Task LiveValueLoaderGetsLoaderFromCacheEntry()
        {
            // TODO: Mocks!
          //  var loadContext = new LoadContext("LoaderType");
           // var cacheEntry = new CacheEntry(loadContext, typeof(ShortCacheObject));

            var cacheEntryMock = new Mock<ICacheEntry>();
            var loadRequest = new Mock<LoadRequest>();
            cacheEntryMock.Setup(cacheEntry => cacheEntry.GetDataLoader()).Returns(loadRequest.Object);

            var liveValueLoader = new LiveValueLoader(cacheEntryMock.Object);

            await liveValueLoader.FetchData();

            Assert.IsTrue(liveValueLoader.IsValid);
            // TODO: Some assertions here or something useful...
        }
    }
}
