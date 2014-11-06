namespace AgFx.Test.Mocks
{
    // TODO: Commonise mock object classes more
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
