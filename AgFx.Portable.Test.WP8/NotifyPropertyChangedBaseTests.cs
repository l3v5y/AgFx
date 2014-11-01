#if WINDOWS_PHONE
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using System.Threading;
using System;
using System.ComponentModel;
using System.Windows;

namespace AgFx.Test
{
    [TestClass]
    public class NotifyPropertyChangedBaseTests
    {
        [TestInitialize]
        public void Initialise()
        {
            TestHelpers.InitializePriorityQueue();
        }

        [TestMethod]
        public void TestChangedOnUIThread()
        {
            TestChanger tc = new TestChanger(false);

            bool gotChange = false;

            tc.PropertyChanged += (s, a) =>
            {
                gotChange = true;
            };

            tc.TestProp = "xyz";

            Assert.IsTrue(gotChange);
        }

        [TestMethod]
        public void TestChangedOnNonUiThread()
        {
            var resetEvent = new ManualResetEvent(false);
            TestChanger tc = new TestChanger(true);

            var threadId = Thread.CurrentThread.ManagedThreadId;

            PropertyChangedEventHandler handler = null;

            handler = (s, a) =>
            {
#if WINDOWS_PHONE
                Assert.IsTrue(Deployment.Current.Dispatcher.CheckAccess());
#endif

                tc.PropertyChanged -= handler;
                resetEvent.Set();
            };

            tc.PropertyChanged += handler;

            ThreadPool.QueueUserWorkItem((s) =>
            {
                tc.TestProp = "123";
            }, null);

            resetEvent.WaitOne();
        }

        [TestMethod]
        public void TestChangedSynchronousOnUiThread()
        {
            var resetEvent = new ManualResetEvent(false);
#if WINDOWS_PHONE
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
#endif
                TestChanger tc = new TestChanger(true);

                var threadId = Thread.CurrentThread.ManagedThreadId;

                bool startFirstChagne = false;
                bool finishFirstchange = false;
                bool gotNestedChange = false;

                PropertyChangedEventHandler hander = null;

                hander = (s, a) =>
                {
                    bool testComplete = false;

                    if (!startFirstChagne)
                    {
                        startFirstChagne = true;
                        tc.TestProp = "again";
                        finishFirstchange = true;
                        if (!gotNestedChange)
                        {
                            Assert.Fail();
                            testComplete = true;
                        }
                    }
                    else if (!finishFirstchange)
                    {
                        gotNestedChange = true;
                        Assert.AreEqual(threadId, Thread.CurrentThread.ManagedThreadId);
                        testComplete = true;
                    }
                    else
                    {
                        Assert.Fail();
                        testComplete = true;
                    }

                    if (testComplete)
                    {
                        tc.PropertyChanged -= hander;
                        resetEvent.Set();
                    }
                };

                tc.PropertyChanged += hander;

                ThreadPool.QueueUserWorkItem((s) =>
                {
                    tc.TestProp = "123";
                },
                    null
                );
#if WINDOWS_PHONE
            });
#endif
            resetEvent.WaitOne();
        }

        [TestMethod]
        [UITestMethod]
        public void TestDependantProperty()
        {
            var resetEvent = new ManualResetEvent(false);
            TestChanger tc = new TestChanger();

            bool gotDependantChange = false;

                tc.PropertyChanged += (s, a) =>
                {
                    gotDependantChange |= a.PropertyName == "DependentProp";
                };

                tc.TestProp = "changed";
                resetEvent.Set();

            resetEvent.WaitOne();
            Assert.IsTrue(gotDependantChange);
        }

        [TestMethod]
        [UITestMethod]
        public void TestMultiDependantProperty()
        {
            var resetEvent = new ManualResetEvent(false);
            TestChanger tc = new TestChanger();

            int notifyCount = 0;

            tc.PropertyChanged += (s, a) =>
            {

                if (a.PropertyName == "MultiDependentProp")
                {
                    notifyCount++;
                }

            };

            tc.TestProp = "changed";
            tc.TestProp2 = "changed";

            Assert.AreEqual(2, notifyCount);
        }

        [TestMethod]
        [UITestMethod]
        public void TestFakeDependantProperty()
        {
            TestChanger tc = new TestChanger();

            bool gotDependantChange = false;

            tc.PropertyChanged += (s, a) =>
            {
                gotDependantChange |= a.PropertyName == "FakeDependentProp";
            };

            tc.NotifyFakeProperty("FakeProp");

            Assert.IsTrue(gotDependantChange);
        }

        [TestMethod]
        public void TestBadPropertyDependency()
        {

            try
            {
                var tc = new TestChanger_Bad();
            }
            catch (ArgumentException)
            {
                return;
            }
            Assert.Fail();
        }

        public class TestChanger_Bad : NotifyPropertyChangedBase
        {

            [DependentOnProperty("Nada")]
            public string SomeThing
            {
                get;
                set;
            }

        }

        public class TestChanger : NotifyPropertyChangedBase
        {

            public TestChanger()
                : this(true)
            {
            }

            public TestChanger(bool notifyOnUiThread)
                : base(notifyOnUiThread)
            {
            }

            #region Property TestProp
            private string _TestProp;
            public string TestProp
            {
                get
                {
                    return _TestProp;
                }
                set
                {
                    if (_TestProp != value)
                    {
                        _TestProp = value;
                        RaisePropertyChanged("TestProp");
                    }
                }
            }
            #endregion

            #region Property TestProp2
            private string _TestProp2;
            public string TestProp2
            {
                get
                {
                    return _TestProp2;
                }
                set
                {
                    if (_TestProp2 != value)
                    {
                        _TestProp2 = value;
                        RaisePropertyChanged("TestProp2");
                    }
                }
            }
            #endregion

            #region Property TestProp
            private string _dependentProp;

            [DependentOnProperty("TestProp")]
            public string DependentProp
            {
                get
                {
                    return _dependentProp;
                }
                set
                {
                    if (_dependentProp != value)
                    {
                        _dependentProp = value;
                        RaisePropertyChanged("DependentProp");
                    }
                }
            }
            #endregion

            private string _mulltidependentProp;

            [DependentOnProperty("TestProp")]
            [DependentOnProperty("TestProp2")]
            public string MultiDependentProp
            {
                get
                {
                    return _mulltidependentProp;
                }
                set
                {
                    if (_mulltidependentProp != value)
                    {
                        _mulltidependentProp = value;
                        RaisePropertyChanged("MultiDependentProp");
                    }
                }
            }

            private string _fakeProp;

            [DependentOnProperty(PrimaryPropertyName = "FakeProp", IsNotARealPropertyName = true)]
            public string FakeDependentProp
            {
                get
                {
                    return _fakeProp;
                }
                set
                {
                    if (_fakeProp != value)
                    {
                        _fakeProp = value;
                        RaisePropertyChanged("FakeDependentProp");
                    }
                }
            }

            #region Property TestProp
            private string _oldProp;

            [DependentOnProperty("TestProp")]
            public string NoAttributeDependentProp
            {
                get
                {
                    return _oldProp;
                }
                set
                {
                    if (_oldProp != value)
                    {
                        _oldProp = value;
                        RaisePropertyChanged("NoAttributeDependentProp");
                    }
                }
            }
            #endregion

            public void NotifyFakeProperty(string name)
            {
                RaisePropertyChanged(name);
            }



        }
    }
}