namespace AgFx.Test.Mocks
{
    [CachePolicy(CachePolicy.ValidCacheOnly, 10)]
    public class ValidCacheOnlyObject : ShortCacheObject
    {
        public ValidCacheOnlyObject()
        {

        }

        public ValidCacheOnlyObject(object id)
            : base(id)
        {
        }

        public class VCODataLoader : ShortCacheObject.SCODataLoader
        {
            protected override ShortCacheObject CreateInstance(object id)
            {
                return new ValidCacheOnlyObject(id);
            }
        }
    }
}
