namespace AgFx.Test.Mocks
{
    [CachePolicy(CachePolicy.NoCache)]
    public class NoCacheObject : ShortCacheObject
    {

        public NoCacheObject()
        {

        }

        public NoCacheObject(object id)
            : base(id)
        {
        }

        public class NCODataLoader : ShortCacheObject.SCODataLoader
        {
            protected override ShortCacheObject CreateInstance(object id)
            {
                return new NoCacheObject(id);
            }
        }
    }
}
