using System;
using System.IO;
using System.Text;

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

                public override void Execute(Action<LoadRequestResult> result)
                {
                    MemoryStream ms = new MemoryStream(UTF8Encoding.UTF8.GetBytes(LoadContext.Identity.ToString()));
                    ms.Seek(0, SeekOrigin.Begin);
                    result(new LoadRequestResult(ms));
                }
            }
        }
    }
}
