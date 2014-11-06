using AgFx.Test.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AgFx.Test
{
    [TestClass]
    public class AutoLoadContextCreatorTests
    {
        [TestMethod]
        public void TestGenericLoadContextCreation()
        {
            var loadContextCreator = new AutoLoadContextCreator();

            var loadContext = loadContextCreator.CreateLoadContext<TestContextObject>(4321);

            Assert.AreEqual(loadContext.Identity,"TestLoadContext:4321");
            Assert.IsInstanceOfType(loadContext, typeof(LoadContext));
            Assert.IsNotNull((TestLoadContext)loadContext);
            Assert.IsTrue(typeof(TestLoadContext).IsAssignableFrom(loadContext.GetType()));
        }

        [TestMethod]
        public void TestAutoContextCreation()
        {
            var loadContextCreator = new AutoLoadContextCreator();

            var loadContext = loadContextCreator.CreateLoadContext<ShortCacheObject>("TEST");

            Assert.AreEqual(loadContext.Identity, "TEST");
            Assert.IsInstanceOfType(loadContext, typeof(LoadContext));
        }
    }
}