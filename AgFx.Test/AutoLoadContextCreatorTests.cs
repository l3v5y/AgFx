using Xunit;

namespace AgFx.Test
{
    public class AutoLoadContextCreatorTests
    {
        private const string LoadContextIdentity = "TestLoadContext";

        [Fact]
        public void TestLoadContextCreationOnGenericModel()
        {
            var loadContextCreator = new AutoLoadContextCreator();

            var loadContext = loadContextCreator.CreateLoadContext<ModelItemBase<LoadContext>>(LoadContextIdentity);

            Assert.IsType<LoadContext>(loadContext);
            Assert.Equal(loadContext.Identity, LoadContextIdentity);
        }

        [Fact]
        public void TestAutoContextCreation()
        {
            var loadContextCreator = new AutoLoadContextCreator();

            var loadContext = loadContextCreator.CreateLoadContext<ModelItemBase>(LoadContextIdentity);

            Assert.IsType<LoadContext>(loadContext);
            Assert.Equal(loadContext.Identity, LoadContextIdentity);
        }

        [Fact]
        public void TestAutoContextCreationFromModel()
        {
            var loadContextCreator = new AutoLoadContextCreator();
            var modelLoadContext = loadContextCreator.CreateLoadContext<ModelItemBase>(LoadContextIdentity);

            var model = new ModelItemBase(modelLoadContext);
            var loadContext = loadContextCreator.CreateLoadContext<ModelItemBase>(model);

            Assert.IsType<LoadContext>(loadContext);
            Assert.Equal(loadContext.Identity, LoadContextIdentity);
        }
    }
}