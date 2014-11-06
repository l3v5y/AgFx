using System;
using System.IO;
using System.Text;

namespace AgFx.Test.Mocks
{
    public class TestContextObject : ModelItemBase<TestLoadContext>
    {
        public TestContextObject()
        {
        }

        public string DeserializedValue { get; set; }

        public class TestContextDataLoader : IDataLoader<TestLoadContext>
        {
            public LoadRequest GetLoadRequest(TestLoadContext loadContext, Type objectType)
            {
                return new TestLoadRequest(loadContext, String.Format("{0}.{1}", loadContext.Identity, loadContext.Option));
            }

            public object Deserialize(TestLoadContext loadContext, Type objectType, Stream stream)
            {
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);

                string val = UTF8Encoding.UTF8.GetString(bytes, 0, bytes.Length);

                var tco = new TestContextObject();
                tco.LoadContext = loadContext;
                tco.DeserializedValue = val;
                return tco;
            }
        }
    }
}
