using AgFx.Test.Mocks;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

namespace AgFx.Test
{
    [TestClass]
    public class AutoLoadContextCreatorTests
    {
        [TestMethod]
        public void TestAutoContextCreation()
        {
            var loadContextCreator = new AutoLoadContextCreator();

            var loadContext = loadContextCreator.AutoCreateLoadContext<TestContextObject>(4321);

            Assert.AreEqual(loadContext.Identity, 4321);
            Assert.IsInstanceOfType(loadContext, typeof(TestLoadContext));
        }
    }
}
