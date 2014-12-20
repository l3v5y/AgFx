using System;
using System.IO;
using System.Text;
using System.Threading;

namespace AgFx.Test.TestModels
{
    public class TestContextObject : ModelItemBase<TestLoadContext>
    {
        public TestContextObject(TestLoadContext loadContext)
            : base(loadContext)
        {
        }

        public string DeserializedValue { get; set; }

        public class TestContextDataLoader : IDataLoader<TestLoadContext>
        {
            public LoadRequest GetLoadRequest(TestLoadContext loadContext, Type objectType)
            {
                return new TestLoadRequest(loadContext,
                    String.Format("{0}.{1}", loadContext.Identity, loadContext.Option));
            }

            public object Deserialize(TestLoadContext loadContext, Type objectType, Stream stream)
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);

                var val = Encoding.UTF8.GetString(bytes, 0, bytes.Length);

                var tco = new TestContextObject(loadContext)
                {
                    DeserializedValue = val
                };
                return tco;
            }
        }
    }
}