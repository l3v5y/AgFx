using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using Xunit;

namespace AgFx.Test
{
    public class NotifyPropertyChangedBaseTests
    {
        private const int AsynchronousTestTimeout = 5000;

        [Fact]
        public void ChangedOnNonUiThread_FiresPropertyChanged_OnUiThread()
        {
            var resetEvent = new ManualResetEvent(false);

            var tc = new TestChanger();

            PropertyChangedEventHandler handler = null;

            handler = (s, a) =>
            {
                Assert.True(Deployment.Current.CheckAccess());

                tc.PropertyChanged -= handler;
                resetEvent.Set();
            };

            tc.PropertyChanged += handler;

            ThreadPool.QueueUserWorkItem(s => { tc.TestProp = "123"; }, null);
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestChangedSynchronousOnUiThread()
        {
            var resetEvent = new ManualResetEvent(false);
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                var tc = new TestChanger();

                var threadId = Thread.CurrentThread.ManagedThreadId;

                var startFirstChange = false;
                var finishFirstChange = false;
                var gotNestedChange = false;

                PropertyChangedEventHandler hander = null;

                hander = (s, a) =>
                {
                    if(!startFirstChange)
                    {
                        startFirstChange = true;
                        tc.TestProp = "again";
                        finishFirstChange = true;
                        if(!gotNestedChange)
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    else if(!finishFirstChange)
                    {
                        gotNestedChange = true;
                        Assert.Equal(threadId, Thread.CurrentThread.ManagedThreadId);
                        tc.PropertyChanged -= hander;
                        resetEvent.Set();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                };

                tc.PropertyChanged += hander;

                ThreadPool.QueueUserWorkItem(s => { tc.TestProp = "123"; },
                    null
                    );
            });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        public class TestChanger : NotifyPropertyChangedBase
        {
            private string _dependentProp;
            private string _fakeProp;
            private string _multidependentProp;
            private string _oldProp;
            private string _testProp;
            private string _testProp2;

            public string MultiDependentProp
            {
                get { return _multidependentProp; }
                set
                {
                    if(_multidependentProp != value)
                    {
                        _multidependentProp = value;
                        RaisePropertyChanged("MultiDependentProp");
                    }
                }
            }

            public string FakeDependentProp
            {
                get { return _fakeProp; }
                set
                {
                    if(_fakeProp != value)
                    {
                        _fakeProp = value;
                        RaisePropertyChanged("FakeDependentProp");
                    }
                }
            }

            public string TestProp
            {
                get { return _testProp; }
                set
                {
                    if(_testProp != value)
                    {
                        _testProp = value;
                        RaisePropertyChanged("TestProp");
                    }
                }
            }

            public string TestProp2
            {
                get { return _testProp2; }
                set
                {
                    if(_testProp2 != value)
                    {
                        _testProp2 = value;
                        RaisePropertyChanged("TestProp2");
                    }
                }
            }

            public string DependentProp
            {
                get { return _dependentProp; }
                set
                {
                    if(_dependentProp != value)
                    {
                        _dependentProp = value;
                        RaisePropertyChanged("DependentProp");
                    }
                }
            }

            public string NoAttributeDependentProp
            {
                get { return _oldProp; }
                set
                {
                    if(_oldProp != value)
                    {
                        _oldProp = value;
                        RaisePropertyChanged("NoAttributeDependentProp");
                    }
                }
            }

            public void NotifyFakeProperty(string name)
            {
                RaisePropertyChanged(name);
            }
        }
    }
}