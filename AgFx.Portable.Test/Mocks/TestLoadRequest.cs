using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AgFx.Test.Mocks
{
    public class TestLoadRequest : LoadRequest
    {
        string _value;

        public TestLoadRequest(LoadContext context, string value)
            : base(context)
        {
            _value = value;
        }

        public override Task<LoadRequestResult> Execute()
        {
            var str = new MemoryStream(UTF8Encoding.UTF8.GetBytes(_value));
            str.Seek(0, SeekOrigin.Begin);

            return Task<LoadRequestResult>.FromResult(new LoadRequestResult(str));
        }
    }
}
