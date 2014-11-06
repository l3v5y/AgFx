using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AgFx.Test.Mocks
{
    public class TestNestedLoaderObject
    {
        public string StrValue { get; set; }

        public class TestNestedLoaderObjectLoader : IDataLoader<LoadContext>
        {
            public TestNestedLoaderObjectLoader()
            {

            }
            public LoadRequest GetLoadRequest(LoadContext identifier, Type objectType)
            {
                return new NestedLoadRequest(identifier);
            }

            public object Deserialize(LoadContext identifier, Type objectType, Stream stream)
            {
                var sr = new StreamReader(stream);

                var tnlo = (TestNestedLoaderObject)Activator.CreateInstance(objectType);

                tnlo.StrValue = sr.ReadToEnd();

                return tnlo;
            }

            private class NestedLoadRequest : LoadRequest
            {
                public NestedLoadRequest(LoadContext id)
                    : base(id)
                {
                }

                public override Task<LoadRequestResult> Execute()
                {
                    MemoryStream ms = new MemoryStream(UTF8Encoding.UTF8.GetBytes(LoadContext.Identity.ToString()));
                    ms.Seek(0, SeekOrigin.Begin);
                    return Task<LoadRequestResult>.FromResult(new LoadRequestResult(ms));
                }
            }
        }
    }
}
