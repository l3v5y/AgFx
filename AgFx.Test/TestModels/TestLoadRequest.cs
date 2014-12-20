using System;
using System.IO;
using System.Text;

namespace AgFx.Test.TestModels
{
    public class TestLoadRequest : LoadRequest
    {
        private readonly string _value;

        public TestLoadRequest(LoadContext context, string value)
            : base(context)
        {
            _value = value;
        }

        public override void Execute(Action<LoadRequestResult> result)
        {
            var str = new MemoryStream(Encoding.UTF8.GetBytes(_value));
            str.Seek(0, SeekOrigin.Begin);
            result(new LoadRequestResult(str));
        }
    }
}