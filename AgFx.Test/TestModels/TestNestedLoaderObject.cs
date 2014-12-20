using System;
using System.IO;
using System.Text;

namespace AgFx.Test.TestModels
{
    public class TestNestedLoaderObject : ModelItemBase
    {
        public TestNestedLoaderObject()
        {
        }

        public TestNestedLoaderObject(LoadContext loadContext)
            : base(loadContext)
        {
        }

        public string Value { get; set; }

        public class NestedLoaderObjectDataLoader : IDataLoader<LoadContext>
        {
            public LoadRequest GetLoadRequest(LoadContext identifier, Type objectType)
            {
                return new NestedLoadRequest(identifier);
            }

            public object Deserialize(LoadContext identifier, Type objectType, Stream stream)
            {
                var sr = new StreamReader(stream);

                var tnlo =
                    (TestNestedLoaderObject)
                        Activator.CreateInstance(objectType, identifier);

                tnlo.Value = sr.ReadToEnd();
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
                    var ms = new MemoryStream(Encoding.UTF8.GetBytes(LoadContext.Identity.ToString()));
                    ms.Seek(0, SeekOrigin.Begin);
                    result(new LoadRequestResult(ms));
                }
            }
        }
    }
}