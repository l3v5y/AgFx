using System;
using System.IO;
using Xunit;

namespace AgFx.Test
{
    // TODO: Add Update/CloneProperties tests
    public class ReflectionSerializerTests
    {
        private static readonly string _data =
            "AgFx.Test.ReflectionSerializerTests+TestClass\r\nString:blah%20blah%20blah%0D%0Ablah%20blah\r\nInt:1234\r\nDateTime:01%2F05%2F1997%2000%3A00%3A00\r\nDouble:1234.4321\r\nBool:True\r\n::\r\n";

        private TestClass CreateTestClass()
        {
            return new TestClass
            {
                String = "blah blah blah\r\nblah blah",
                Int = 1234,
                DateTime = new DateTime(1997, 1, 5),
                Bool = true,
                Double = 1234.4321
            };
        }

        [Fact]
        public void TestSerialize()
        {
            var tc = CreateTestClass();

            var sw = new StringWriter();

            ReflectionSerializer.Serialize(tc, sw);

            var data = sw.ToString();


            Assert.Equal<string>(_data, data);
        }

        [Fact]
        public void TestDeserialize()
        {
            var tc = new TestClass();
            var resultClass = CreateTestClass();

            Assert.NotEqual(tc, resultClass);

            ReflectionSerializer.Deserialize(tc, new StringReader(_data));

            Assert.Equal(resultClass, tc);
        }

        public class TestClass
        {
            public string String { get; set; }
            public int Int { get; set; }
            public DateTime DateTime { get; set; }
            public double Double { get; set; }
            public bool Bool { get; set; }

            public override bool Equals(object obj)
            {
                var other = (TestClass)obj;

                return String == other.String &&
                       Int == other.Int &&
                       Double == other.Double &&
                       DateTime == other.DateTime &&
                       Bool == other.Bool;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
    }
}