using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AgFx.Test.Mocks
{
    public class VariLoadContext : LoadContext
    {
        public int Foo { get; set; }
        public VariLoadContext(object id)
            : base(id)
        {
        }
    }

    [CachePolicy(CachePolicy.CacheThenRefresh, 1)]
    public class VariableCacheObject : ModelItemBase<VariLoadContext>, ICachedItem
    {
        DateTime? _expTime;
        public DateTime? ExpirationTime
        {
            get { return _expTime; }
            set { _expTime = value; }
        }

        public int Foo { get; set; }


        public class VariCacheLoader : IDataLoader<VariLoadContext>
        {

            public LoadRequest GetLoadRequest(VariLoadContext loadContext, Type objectType)
            {
                return new VariloadRequest(loadContext);
            }

            public object Deserialize(VariLoadContext loadContext, Type objectType, Stream stream)
            {

                VariableCacheObject vco = new VariableCacheObject();
                vco.LoadContext = loadContext;
                var date = (DateTime)loadContext.Identity;
                vco.ExpirationTime = (date == default(DateTime)) ? null : (DateTime?)date;
                vco.Foo = loadContext.Foo;
                return vco;

            }

            public class VariloadRequest : LoadRequest
            {
                public VariloadRequest(LoadContext lc)
                    : base(lc)
                {
                }

                public override Task<LoadRequestResult> Execute()
                {
                    string foo = LoadContext.Identity.ToString();
                    MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(foo));

                    return Task<LoadRequestResult>.FromResult(new LoadRequestResult(ms));
                }
            }
        }
    }
}
