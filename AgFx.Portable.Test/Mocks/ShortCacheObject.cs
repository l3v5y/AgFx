using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgFx.Test.Mocks
{
    [CachePolicy(CachePolicy.CacheThenRefresh, 10)]
    public class ShortCacheObject : ModelItemBase
    {
        public static string DefaultIdentifier = "x";
        public static string DefaultStringValue = "StringValue";
        public static int DefaultIntValue = 1234;
        public static string FailDeserializeMessage = null;

        #region Property StringProp
        private string _StringProp;
        public string StringProp
        {
            get
            {
                return _StringProp;
            }
            set
            {
                if (_StringProp != value)
                {
                    _StringProp = value;
                    RaisePropertyChanged("StringProp");
                }
            }
        }
        #endregion



        #region Property IntProp
        private int _IntProp;
        public int IntProp
        {
            get
            {
                return _IntProp;
            }
            set
            {
                if (_IntProp != value)
                {
                    _IntProp = value;
                    RaisePropertyChanged("IntProp");
                }
            }
        }
        #endregion

        public ShortCacheObject()
        {

        }

        public ShortCacheObject(object id)
            : base(id)
        {
        }

        public class SCODataLoader : IDataLoader<LoadContext>
        {
            protected virtual ShortCacheObject CreateInstance(object identity)
            {
                return new ShortCacheObject(identity);
            }

            public LoadRequest GetLoadRequest(LoadContext identity, Type objectType)
            {
                return new SCOLoadRequest(identity, DefaultStringValue, DefaultIntValue);
            }

            public static ShortCacheObject Deserialize(ShortCacheObject item, Stream s)
            {
                StreamReader sr = new StreamReader(s);

                ShortCacheObject sco = item;
                sco.StringProp = sr.ReadLine();

                // this is expected to fail in the DeserializeCacheFail case
                sco.IntProp = Int32.Parse(sr.ReadLine());
                return sco;
            }

            public static void Serialize(ShortCacheObject o, Stream s)
            {
                StreamWriter sw = new StreamWriter(s);
                sw.WriteLine(o.StringProp);
                sw.WriteLine(o.IntProp);
                sw.Flush();
            }

            public object Deserialize(LoadContext id, Type objectType, Stream stream)
            {
                if (FailDeserializeMessage != null)
                {
                    throw new FormatException(FailDeserializeMessage);
                }
                StreamReader sr = new StreamReader(stream);

                ShortCacheObject sco = CreateInstance(id.Identity);
                sco.StringProp = sr.ReadLine();

                // this is expected to fail in the DeserializeCacheFail case
                sco.IntProp = Int32.Parse(sr.ReadLine());
                return sco;
            }
        }

        public class SCOLoadRequest : LoadRequest
        {
            public static int LoadTimeMs = 20;
            public static Exception Error = null;

            string s;
            int i;

            public SCOLoadRequest(LoadContext id, string strValue, int intValue)
                : base(id)
            {
                s = strValue;
                i = intValue;
            }

            public override async Task<LoadRequestResult> Execute()
            {
                await Task.Delay(LoadTimeMs);
                string str = s;
                int intVal = i;
                MemoryStream ms = WriteToStream(str, intVal);

                if (Error == null)
                {
                    return new LoadRequestResult(ms);
                }
                else
                {
                    return new LoadRequestResult(Error);
                }
            }

            public static MemoryStream WriteToStream(string str, int intVal)
            {
                MemoryStream ms = new MemoryStream();
                StreamWriter sw = new StreamWriter(ms);
                sw.WriteLine(str);
                sw.WriteLine(intVal);
                sw.Flush();
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }
    }
}
