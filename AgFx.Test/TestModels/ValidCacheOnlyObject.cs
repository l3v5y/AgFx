using System.Threading;

namespace AgFx.Test.TestModels
{
    [CachePolicy(CachePolicy.ValidCacheOnly, 10)]
    public class ValidCacheOnlyObject : ShortCacheObject
    {
        public ValidCacheOnlyObject()
        {
        }

        public ValidCacheOnlyObject(LoadContext id) : base(id)
        {
        }

        public class ValidCacheOnlyDataLoader : ShortCacheObjectDataLoader
        {
            protected override ShortCacheObject CreateInstance(LoadContext loadContext)
            {
                return new ValidCacheOnlyObject(loadContext);
            }
        }
    }
}