#if WINDOWS_PHONE
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using System;

namespace AgFx.Test
{
    [TestClass]
    public class ReflectionHelperTests
    {
        [TestMethod]
        public void TestStringIsInstanceOfObjectType()
        {
            Assert.IsTrue(ReflectionHelpers.IsInstanceOfType(typeof(object), "x"));
        }

        [TestMethod]
        public void TestIntIsNotInstanceOfStringType()
        {
            Assert.IsFalse(ReflectionHelpers.IsInstanceOfType(typeof(string), 1));
        }
    }
}
