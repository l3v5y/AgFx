namespace AgFx.Test.TestModels
{
    [CachePolicy(CachePolicy.NoCache)]
    public class NoCacheObject : ShortCacheObject
    {
        public NoCacheObject()
        {
        }

        public NoCacheObject(LoadContext loadContext) :
                base(loadContext)
        {
        }

        public class NoCacheObjectDataLoader : ShortCacheObjectDataLoader
        {
            protected override ShortCacheObject CreateInstance(LoadContext loadContext)
            {
                return new NoCacheObject(loadContext);
            }
        }
    }
}