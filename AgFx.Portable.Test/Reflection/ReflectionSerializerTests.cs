// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace AgFx.Test
{
    [TestClass]
    public class ReflectionSerializerTests
    {
        [TestMethod]
        public void UpdateObject_SameType_Succeeds()
        {
            var destination = new TestClass();
            var source = CreateTestClass();

            ReflectionSerializer.UpdateObject(source, destination, false, null);

            Assert.AreEqual(destination, source);
        }

        [TestMethod]
        public void UpdateObject_SameTypeExplicitlyCheckUpdateable_Succeeds()
        {
            var destination = new TestClass();
            var source = CreateTestClass();

            ReflectionSerializer.UpdateObject(source, destination, true, null);
            
            Assert.AreEqual(destination, source);
        }

        [TestMethod]
        public void UpdateObject_IUpdatable_Succeeds()
        {
            var source = CreateUpdatableTestClass();
            var destination = new UpdatableTestClass();

            ReflectionSerializer.UpdateObject(source, destination, true, null);

            Assert.AreEqual(source, destination);
            Assert.IsTrue(destination.UpdatedFromMethod);
        }

        [TestMethod]
        public void UpdateObject_IUpdatable_UpdatesTime()
        {
            var source = CreateUpdatableTestClass();
            var destination = new UpdatableTestClass();
            var updateTime = DateTime.Now;

            ReflectionSerializer.UpdateObject(source, destination, true, updateTime);

            Assert.AreEqual(updateTime, destination.LastUpdated);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void UpdateObject_NullInput_ThrowsError()
        {
            var source = CreateUpdatableTestClass();
            UpdatableTestClass destination = null;

            ReflectionSerializer.UpdateObject(source, destination, true, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void UpdateObject_DifferentTypes_ThrowsError()
        {
            var source = CreateUpdatableTestClass();
            var destination = new TestClass();

            ReflectionSerializer.UpdateObject(source, destination, true, null);
        }

        [TestMethod]
        public void Serialize_SerializesObjectToString()
        {
            var testClass = CreateTestClass();

            StringWriter sw = new StringWriter();

            ReflectionSerializer.Serialize(testClass, sw);

            string data = sw.ToString();

            Assert.AreEqual<string>(_data, data);
        }

        static string _data = "AgFx.Test.ReflectionSerializerTests+TestClass\r\nString:blah%20blah%20blah%0D%0Ablah%20blah\r\nInt:1234\r\nDateTime:01%2F05%2F1997%2000%3A00%3A00\r\nDouble:1234.4321\r\nBool:True\r\n::\r\n";

        [TestMethod]
        public void Deserialize_DeserializesObjectFromString()
        {
            TestClass tc = new TestClass();
            TestClass resultClass = CreateTestClass();

            Assert.AreNotEqual(tc, resultClass);

            ReflectionSerializer.Deserialize(tc, new StringReader(_data));

            Assert.AreEqual<TestClass>(resultClass, tc);
        }
        
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

        private UpdatableTestClass CreateUpdatableTestClass()
        {
            return new UpdatableTestClass
            {
                String = "blah blah blah\r\nblah blah",
                Int = 1234,
                DateTime = new DateTime(1997, 1, 5),
                Bool = true,
                Double = 1234.4321
            };
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
                TestClass other = (TestClass)obj;

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

        public class UpdatableTestClass : TestClass, IUpdatable
        {
            public bool IsUpdating { get; set; }
            public DateTime LastUpdated { get; set; }
            public LoadContext LoadContext { get; set; }

            public bool UpdatedFromMethod { get; private set; }

            public void UpdateFrom(object source)
            {
                if (!this.GetType().IsAssignableFrom(source.GetType()))
                {
                    throw new ArgumentException(string.Format("{0} cannot be assigned from {1}", this.GetType(), source.GetType()));
                }

                var other = (UpdatableTestClass)source;
                String = other.String;
                Int = other.Int;
                Double = other.Double;
                DateTime = other.DateTime;
                Bool = other.Bool;

                UpdatedFromMethod = true;
            }

            public void Refresh()
            {
                throw new NotImplementedException();
            }
        }
    }
}