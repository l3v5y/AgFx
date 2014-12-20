using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AgFx.Test.TestModels
{
    [CachePolicy(CachePolicy.CacheThenRefresh, 10)]
    public class ShortCacheObject : ModelItemBase
    {
        public static readonly string DefaultIdentifier = "x";
        public static string DefaultStringValue = "StringValue";
        public static int DefaultIntValue = 1234;
        public static string FailDeserializeMessage;

       public ShortCacheObject()
        {
        }

        public ShortCacheObject(LoadContext id)
            : base(id)
        {
        }

        public class ShortCacheObjectDataLoader : IDataLoader<LoadContext>
        {
            public LoadRequest GetLoadRequest(LoadContext identity, Type objectType)
            {
                return new ShortCacheObjectLoadRequest(identity, DefaultStringValue, DefaultIntValue);
            }

            public object Deserialize(LoadContext id, Type objectType, Stream stream)
            {
                if(FailDeserializeMessage != null)
                {
                    throw new FormatException(FailDeserializeMessage);
                }
                var sr = new StreamReader(stream);

                var sco = CreateInstance(id);
                sco.StringProp = sr.ReadLine();

                Int32 intOutput;

                if(Int32.TryParse(sr.ReadLine(), out intOutput))
                {
                    sco.IntProp = intOutput;
                }
                else
                {
                    throw new Exception();
                }
                
                return sco;
            }

            protected virtual ShortCacheObject CreateInstance(LoadContext loadContext)
            {
                return new ShortCacheObject(loadContext);
            }

            public static ShortCacheObject Deserialize(ShortCacheObject item, Stream s)
            {
                var sr = new StreamReader(s);

                var sco = item;
                sco.StringProp = sr.ReadLine();

                // ReSharper disable once AssignNullToNotNullAttribute this is expected to fail in the DeserializeCacheFail case
                sco.IntProp = Int32.Parse(sr.ReadLine());
                return sco;
            }

            public static void Serialize(ShortCacheObject o, Stream s)
            {
                var sw = new StreamWriter(s);
                sw.WriteLine(o.StringProp);
                sw.WriteLine(o.IntProp);
                sw.Flush();
            }
        }

        public class ShortCacheObjectLoadRequest : LoadRequest
        {
            public static int LoadTimeMs = 200;
            public static Exception Error;
            private readonly int _integerValue;
            private readonly string _stringValue;

            public ShortCacheObjectLoadRequest(LoadContext id, string strValue, int intValue)
                : base(id)
            {
                _stringValue = strValue;
                _integerValue = intValue;
            }

            public override void Execute(Action<LoadRequestResult> result)
            {
                ThreadPool.QueueUserWorkItem(state =>
                {
                    Thread.Sleep(LoadTimeMs);
                    var str = _stringValue;
                    var intVal = _integerValue;
                    var ms = WriteToStream(str, intVal);
                    try
                    {
                        if(Error == null)
                        {
                            result(new LoadRequestResult(ms));
                        }
                        else
                        {
                            result(new LoadRequestResult(Error));
                        }
                    }
                    catch(Exception e)
                    {
                        Debug.WriteLine("Exception caught in LoadRequest: {0}", e);
                    }
                },
                    null);
            }

            public static MemoryStream WriteToStream(string str, int intVal)
            {
                var ms = new MemoryStream();
                var sw = new StreamWriter(ms);
                sw.WriteLine(str);
                sw.WriteLine(intVal);
                sw.Flush();
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }

        private string _stringProp;

        public string StringProp
        {
            get { return _stringProp; }
            set
            {
                if(_stringProp != value)
                {
                    _stringProp = value;
                    RaisePropertyChanged("StringProp");
                }
            }
        }

        private int _intProp;

        public int IntProp
        {
            get { return _intProp; }
            set
            {
                if(_intProp != value)
                {
                    _intProp = value;
                    RaisePropertyChanged("IntProp");
                }
            }
        }
    }
}