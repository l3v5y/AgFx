using System;
using System.IO;

namespace AgFx.Test.TestModels
{
    public class TestDataLoader : IDataLoader<LoadContext>
    {
        public static string DeserializeValue = "DefaultDeserializeValue";

        public LoadRequest GetLoadRequest(LoadContext loadContext, Type objectType)
        {
            return new TestDataLoaderRequest(loadContext, loadContext.Identity.ToString());
        }

        public object Deserialize(LoadContext loadContext, Type objectType, Stream stream)
        {
            var sr = new StreamReader(stream);

            var val = sr.ReadToEnd();

            if(typeof(TestPoco).IsAssignableFrom(objectType))
            {
                var p = (TestPoco)Activator.CreateInstance(objectType);
                p.Value = val;
                return p;
            }

            throw new InvalidOperationException();
        }

        public class TestDataLoaderRequest : LoadRequest
        {
            private readonly string _val;

            public TestDataLoaderRequest(LoadContext lc, string v)
                : base(lc)
            {
                _val = v;
            }

            public override void Execute(Action<LoadRequestResult> result)
            {
                var ms = new MemoryStream();

                var sw = new StreamWriter(ms);
                sw.Write(_val);
                sw.Flush();

                ms.Seek(0, SeekOrigin.Begin);

                var lrr = new LoadRequestResult(ms);
                result(lrr);
            }
        }
    }
}