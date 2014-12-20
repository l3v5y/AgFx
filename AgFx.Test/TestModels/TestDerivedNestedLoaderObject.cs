namespace AgFx.Test.TestModels
{
    public class TestDerivedNestedLoaderObject : TestNestedLoaderObject
    {
        public TestDerivedNestedLoaderObject()
        {
        }

        public TestDerivedNestedLoaderObject(LoadContext loadContext)
            : base(loadContext)
        {
        }
    }
}