using System;
using System.IO;
using System.Text;
using System.Threading;

namespace AgFx.Test.TestModels
{
    [CachePolicy(CachePolicy.CacheThenRefresh, 1)]
    public class VariableCacheObject : ModelItemBase<VariLoadContext>, ICachedItem
    {
        public int Foo { get; set; }
        public DateTime? ExpirationTime { get; set; }

        public class VariCacheLoader : IDataLoader<VariLoadContext>
        {
            public LoadRequest GetLoadRequest(VariLoadContext loadContext, Type objectType)
            {
                return new VariloadRequest(loadContext);
            }

            public object Deserialize(VariLoadContext loadContext, Type objectType, Stream stream)
            {
                var date = (DateTime)loadContext.Identity;
                var vco = new VariableCacheObject
                {
                    LoadContext = loadContext,
                    ExpirationTime = (date == default(DateTime)) ? null : (DateTime?)date,
                    Foo = loadContext.Foo
                };
                return vco;
            }

            public class VariloadRequest : LoadRequest
            {
                public VariloadRequest(LoadContext lc)
                    : base(lc)
                {
                }

                public override void Execute(Action<LoadRequestResult> result)
                {
                    var foo = LoadContext.Identity.ToString();
                    var ms = new MemoryStream(Encoding.Unicode.GetBytes(foo));

                    result(new LoadRequestResult(ms));
                }
            }
        }
    }
}