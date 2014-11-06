using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    }
}
