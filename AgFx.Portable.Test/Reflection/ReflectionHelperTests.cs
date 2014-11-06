// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AgFx.Test
{
    [TestClass]
    public class ReflectionHelperTests
    {
        [TestMethod]
        public void NullIsNotInstanceOfObjectType()
        {
            Assert.IsFalse(typeof(object).IsInstanceOfType(null));
        }

        [TestMethod]
        public void StringIsInstanceOfObjectType()
        {
            Assert.IsTrue(typeof(object).IsInstanceOfType("x"));
        }

        [TestMethod]
        public void IntIsNotInstanceOfStringType()
        {
            Assert.IsFalse(typeof(string).IsInstanceOfType(1));
        }

        [TestMethod]
        public void ObjectIsInstanceOfItsOwnType()
        {
            var parent = new Parent();

            Assert.IsTrue(typeof(Parent).IsInstanceOfType(parent));
        }

        [TestMethod]
        public void ChildIsInstanceOfItsOwnType()
        {
            var child = new Child();

            Assert.IsTrue(typeof(Child).IsInstanceOfType(child));
        }

        [TestMethod]
        public void ChildIsInstanceOfItsParentType()
        {
            var child = new Child();

            Assert.IsTrue(typeof(Parent).IsInstanceOfType(child));
        }

        [TestMethod]
        public void ParentIsNotInstanceOfChild()
        {
            var parent = new Parent();

            Assert.IsFalse(typeof(Child).IsInstanceOfType(parent));
        }

        [TestMethod]
        public void InheritanceTreeImplementsParentInterface()
        {
            var interfaceType = typeof(IParent);
            var parentType = typeof(Parent);
            var childType = typeof(Child);
            var grandChildType = typeof(GrandChild);

            Assert.IsTrue(parentType.ImplementsInterface(interfaceType));
            Assert.IsTrue(childType.ImplementsInterface(interfaceType));
            Assert.IsTrue(grandChildType.ImplementsInterface(interfaceType));
        }

        [TestMethod]
        public void OtherClassDoesNotImplementInterface()
        {
            var interfaceType = typeof(IParent);
            var otherClassType = typeof(OtherClass);

            Assert.IsFalse(otherClassType.ImplementsInterface(interfaceType));
        }

        [TestMethod]
        public void ClassIsNotEnum()
        {
            var classType = typeof(Parent);

            Assert.IsFalse(classType.IsEnum());
        }

        [TestMethod]
        public void EnumIsEnum()
        {
            var classType = typeof(TestEnum);

            Assert.IsTrue(classType.IsEnum());
        }

        [TestMethod]
        public void GetDefaultConstructorWithImplicitConstructorCanCreateNewObjects()
        {
            var implicitDefaultConstructorType = typeof(Parent);
            var constructorInfo = implicitDefaultConstructorType.GetDefaultConstructor();

            var parent = constructorInfo.Invoke(null);

            Assert.IsNotNull(constructorInfo);
            Assert.IsNotNull(parent);
            Assert.IsTrue(implicitDefaultConstructorType.IsInstanceOfType(parent));
        }

        [TestMethod]
        public void GetDefaultConstructorWithExplicitConstructorCanCreateNewObjects()
        {
            var explicitConstructorType = typeof(Child);
            var constructorInfo = explicitConstructorType.GetDefaultConstructor();

            var child = constructorInfo.Invoke(null);

            Assert.IsNotNull(constructorInfo);
            Assert.IsNotNull(child);
            Assert.IsTrue(explicitConstructorType.IsInstanceOfType(child));
            Assert.AreEqual(Child.DEFAULT_NAME, ((Child)child).ChildName);
        }

        [TestMethod]
        public void GetDefaultConstructorWithNonDefaultConstructorFails()
        {
            var explicitConstructorType = typeof(GrandChild);
            var constructorInfo = explicitConstructorType.GetDefaultConstructor();

            Assert.IsNull(constructorInfo);
        }

        [TestMethod]
        public void ObjectsCantBeAssignedFromNull()
        {
            Assert.IsFalse(typeof(object).IsAssignableFrom(null));
        }

        [TestMethod]
        public void ObjectCanBeAssignedFromTypedObject()
        {
            Assert.IsTrue(typeof(object).IsAssignableFrom(typeof(string)));
        }

        [TestMethod]
        public void IntsCantBeAssignedFromStrings()
        {
            Assert.IsFalse(typeof(int).IsAssignableFrom(typeof(string)));
        }

        [TestMethod]
        public void ClassCanBeAssignedFromItsType()
        {
            Assert.IsTrue(typeof(Parent).IsAssignableFrom(typeof(Parent)));
        }

        [TestMethod]
        public void ChildIsAssignableFromItsOwnType()
        {
            Assert.IsTrue(typeof(Child).IsAssignableFrom(typeof(Child)));
        }

        [TestMethod]
        public void ChildIsAssignableFromItsParentType()
        {
            Assert.IsTrue(typeof(Parent).IsAssignableFrom(typeof(Child)));
        }

        [TestMethod]
        public void ParentIsNotAssignableFromChild()
        {
            Assert.IsFalse(typeof(Child).IsAssignableFrom(typeof(Parent)));
        }
 
        private interface IParent { }
        private class Parent : IParent { }

        private class Child : Parent
        {
            public const string DEFAULT_NAME = "DEFAULT";
            public string ChildName { get; set; }
            public Child()
            {
                ChildName = DEFAULT_NAME;
            }

            public Child(string overridden)
            {
                ChildName = overridden;
            }
        }

        private class GrandChild : Child
        {
            public GrandChild(string grandChildName)
                : base(grandChildName) 
            { 
            }
        }

        private class OtherClass { }

        private enum TestEnum
        {
            Entry,
            Second
        }
    }
}
