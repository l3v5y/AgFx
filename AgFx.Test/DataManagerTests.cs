using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using AgFx.Test.TestModels;
using Xunit;

// ReSharper disable SpecifyACultureInStringConversionExplicitly

namespace AgFx.Test
{
    public class DataManagerTests
    {
        private const int AsynchronousTestTimeout = 5000;

        [Fact]
        public void Load_NoDataLoader_ThrowsInvalidOperationException()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            Assert.Throws(typeof(InvalidOperationException), () => dataManager.Load<ModelItemBase>("foo"));
        }

        [Fact]
        public void Load_ShortCacheObject_LoadsSuccessfully()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            dataManager.Load<ShortCacheObject>(ShortCacheObject.DefaultIdentifier,
                obj =>
                {
                    Assert.Equal(ShortCacheObject.DefaultStringValue, obj.StringProp);
                    Assert.Equal(ShortCacheObject.DefaultIntValue, obj.IntProp);
                    resetEvent.Set();
                },
                ex => { throw new InvalidOperationException(ex.Message); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void LoadFromCache_LoadsCacheObject()
        {
            var storeProvider = new InMemoryStoreProvider();
            var dataManager = new DataManager(storeProvider, new WPUiDispatcher());

            var loadContext = new LoadContext("LFC");
            var uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), loadContext);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddHours(1));
            storeProvider.Write(cii,
                ShortCacheObject.ShortCacheObjectLoadRequest.WriteToStream("LoadFromCache", -1).GetBuffer());


            var value = dataManager.LoadFromCache<ShortCacheObject>(loadContext);
            Assert.Equal("LoadFromCache", value.StringProp);
            Assert.Equal(-1, value.IntProp);
        }

        [Fact]
        public void TestRefreshBeforeCacheExpires()
        {
            var storeProvider = new InMemoryStoreProvider();
            storeProvider.Delete();
            var dataManager = new DataManager(storeProvider, new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var loadContext = new LoadContext(ShortCacheObject.DefaultIdentifier);
            // write the cache entry
            //
            var uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), loadContext);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddHours(1));
            var time = DateTime.Now.ToString();
            storeProvider.Write(cii, ShortCacheObject.ShortCacheObjectLoadRequest.WriteToStream(time, -1).GetBuffer());

            var valueReference = dataManager.Load<ShortCacheObject>(loadContext,
                v =>
                {
                    var oldDefault = ShortCacheObject.DefaultStringValue;
                    // we've got a value
                    Assert.Equal(time, v.StringProp);
                    ShortCacheObject.DefaultStringValue = DateTime.Now.ToString();

                    // now request a new value via refresh.
                    //
                    dataManager.Refresh<ShortCacheObject>(loadContext,
                        v2 =>
                        {
                            Assert.Equal(v2.StringProp, ShortCacheObject.DefaultStringValue);
                            ShortCacheObject.DefaultStringValue = oldDefault;
                            resetEvent.Set();
                        },
                        ex2 => { throw new InvalidOperationException(ex2.Message); });
                },
                ex => { throw new InvalidOperationException(ex.Message); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
            Assert.NotNull(valueReference);
        }

        [Fact]
        public void TestRefreshWithValidCache()
        {
            var storeProvider = new InMemoryStoreProvider();
            var dataManager = new DataManager(storeProvider, new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var loadContext = new LoadContext(ShortCacheObject.DefaultIdentifier);
            // write the cache entry
            //
            var uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), loadContext);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddMinutes(1));
            var t = DateTime.Now.ToString();
            storeProvider.Write(cii, ShortCacheObject.ShortCacheObjectLoadRequest.WriteToStream(t, -1).GetBuffer());

            var oldDefault = ShortCacheObject.DefaultStringValue;
            ShortCacheObject.DefaultStringValue = DateTime.Now.Ticks.ToString();

            dataManager.Refresh<ShortCacheObject>(loadContext,
                v =>
                {
                    // we've got a value
                    Assert.Equal(ShortCacheObject.DefaultStringValue, v.StringProp);
                    ShortCacheObject.DefaultStringValue = oldDefault;
                    resetEvent.Set();
                },
                ex => { throw new InvalidOperationException(ex.Message); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestInvalidateFromCache()
        {
            var storeProvider = new InMemoryStoreProvider();
            var dataManager = new DataManager(storeProvider, new WPUiDispatcher());
            var loadContext = new LoadContext("InvalidateFromCache");

            var time = DateTime.Now.ToString();

            // write the cache entry
            //
            var uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), loadContext);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddHours(1));
            storeProvider.Write(cii, ShortCacheObject.ShortCacheObjectLoadRequest.WriteToStream(time, -1).GetBuffer());

            TestInvalidateCore(dataManager, loadContext, time);
        }

        [Fact]
        public void TestInvalidateFromLive()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var loadContext = new LoadContext("InvalidateFromLive");

            ShortCacheObject.DefaultStringValue = DateTime.Now.ToString();

            TestInvalidateCore(dataManager, loadContext, ShortCacheObject.DefaultStringValue);
        }

        private void TestInvalidateCore(DataManager dataManager, LoadContext lc, string time)
        {
            var resetEvent = new ManualResetEvent(false);
            // load it
            //
            dataManager.Load<ShortCacheObject>(lc,
                sco =>
                {
                    // verify we got the right thing
                    //
                    Assert.Equal(time, sco.StringProp);

                    // load again to verify it's not going to change
                    dataManager.Load<ShortCacheObject>(lc,
                        sco2 =>
                        {
                            // verify we got the right thing
                            //
                            Assert.Equal(time, sco2.StringProp);

                            // invalidate it
                            //
                            dataManager.Invalidate<ShortCacheObject>(lc);

                            Assert.Equal(time, sco.StringProp);

                            if(time == ShortCacheObject.DefaultStringValue)
                            {
                                ShortCacheObject.DefaultStringValue = "DefaultString";
                            }

                            // load again to verify it's changed.
                            dataManager.Load<ShortCacheObject>(lc,
                                sco3 =>
                                {
                                    // verify we got the right thing
                                    //
                                    Assert.NotEqual(time, sco3.StringProp);

                                    ShortCacheObject.DefaultStringValue = "DefaultString";
                                    resetEvent.Set();
                                },
                                ex =>
                                {
                                    ShortCacheObject.DefaultStringValue = "DefaultString";
                                    throw new InvalidOperationException();
                                });
                        },
                        ex =>
                        {
                            ShortCacheObject.DefaultStringValue = "DefaultString";
                            throw new InvalidOperationException();
                        });
                },
                ex => { throw new InvalidOperationException(); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout*2));
        }

        [Fact]
        public void TestRefreshOfLoadFail()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            ShortCacheObject val = null;

            ShortCacheObject.FailDeserializeMessage = "expected fail.";

            ShortCacheObject.ShortCacheObjectLoadRequest.Error = new InvalidCastException();

            val = dataManager.Load<ShortCacheObject>(ShortCacheObject.DefaultIdentifier,
                v =>
                {
                    ShortCacheObject.ShortCacheObjectLoadRequest.Error = null;
                    throw new InvalidOperationException();
                },
                ex =>
                {
                    var oldDefault = ShortCacheObject.DefaultStringValue;

                    Assert.NotEqual(val.StringProp, oldDefault);
                    ShortCacheObject.DefaultStringValue = DateTime.Now.ToString();

                    ShortCacheObject.FailDeserializeMessage = null;
                    ShortCacheObject.ShortCacheObjectLoadRequest.Error = null;

                    dataManager.Refresh<ShortCacheObject>(ShortCacheObject.DefaultIdentifier,
                        v2 =>
                        {
                            Assert.Equal(v2.StringProp, ShortCacheObject.DefaultStringValue);
                            ShortCacheObject.DefaultStringValue = oldDefault;
                            resetEvent.Set();
                        },
                        ex2 => { throw new InvalidOperationException(ex2.Message); });
                });
            resetEvent.WaitOne();
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout*2));
        }

        [Fact]
        public void Load_WithExpiredCache_FetchesCachedVersion()
        {
            var storeProvider = new InMemoryStoreProvider();
            var dataManager = new DataManager(storeProvider, new WPUiDispatcher());
            var hasLoaded = false;

            var resetEvent = new ManualResetEvent(false);
            var loadContext = new LoadContext("ExpiredItem");
            var uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), loadContext);
            var cacheItemInfo = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddSeconds(-10));
            storeProvider.Write(cacheItemInfo,
                ShortCacheObject.ShortCacheObjectLoadRequest.WriteToStream("ExpiredItemValue", -1).GetBuffer());
            // slow down the load so we get the cache value.
            //
            var oldTime = ShortCacheObject.ShortCacheObjectLoadRequest.LoadTimeMs;
            ShortCacheObject.ShortCacheObjectLoadRequest.LoadTimeMs = 1000;

            dataManager.Load<ShortCacheObject>(loadContext,
                shortCacheObject =>
                {
                    // This prevents the live load from causing an assertion/test failure
                    if(hasLoaded)
                    {
                        return;
                    }
                    hasLoaded = true;
                    Debug.WriteLine("Asserting over ExpiredItemValue");
                    Assert.Equal("ExpiredItemValue", shortCacheObject.StringProp);
                    resetEvent.Set();
                },
                exception => { throw new InvalidOperationException(exception.Message); });

            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
            ShortCacheObject.ShortCacheObjectLoadRequest.LoadTimeMs = oldTime;
        }

        [Fact]
        public void TestLoadRequestError()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            ShortCacheObject.ShortCacheObjectLoadRequest.Error = new Exception("blah");
            dataManager.Load<ShortCacheObject>("LoadEror",
                v => { throw new InvalidOperationException(); },
                ex =>
                {
                    Assert.Equal(ShortCacheObject.ShortCacheObjectLoadRequest.Error, ex.InnerException);
                    ShortCacheObject.ShortCacheObjectLoadRequest.Error = null;
                    resetEvent.Set();
                });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestLoadRequestErrorUnhandled()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            ShortCacheObject.ShortCacheObjectLoadRequest.Error = new Exception("blah");

            EventHandler<ApplicationUnhandledExceptionEventArgs> handler = null;

            handler = (sender, e) =>
            {
                dataManager.UnhandledError -= handler;
                Assert.NotNull(e.ExceptionObject);
                Assert.Equal(ShortCacheObject.ShortCacheObjectLoadRequest.Error, e.ExceptionObject.InnerException);
                ShortCacheObject.ShortCacheObjectLoadRequest.Error = null;

                e.Handled = true;
                resetEvent.Set();
            };

            dataManager.UnhandledError += handler;

            dataManager.Load<ShortCacheObject>("LoadEror",
                v => { throw new InvalidOperationException(); },
                null);

            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestRefreshingObjectChangesIsUpdatingFlagsCorrectly()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new AutoResetEvent(false);

            var updatingChangedToTrue = false;
            var updatingChangedToFalse = false;

            var shortCacheObject = dataManager.Refresh<ShortCacheObject>("Updating",
                s => { resetEvent.Set(); }, null);
            resetEvent.WaitOne();

            PropertyChangedEventHandler h = (sender, prop) =>
            {
                if(prop.PropertyName == "IsUpdating")
                {
                    if(!updatingChangedToTrue)
                    {
                        Assert.True(shortCacheObject.IsUpdating);
                        updatingChangedToTrue = true;
                    }
                    else if(!updatingChangedToFalse)
                    {
                        Assert.False(shortCacheObject.IsUpdating);
                        updatingChangedToFalse = true;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            };

            shortCacheObject.PropertyChanged += h;
            dataManager.Refresh<ShortCacheObject>(shortCacheObject.LoadContext,
                s2 =>
                {
                    Assert.False(shortCacheObject.IsUpdating);
                    resetEvent.Set();
                },
                null);

            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
            shortCacheObject.PropertyChanged -= h;
        }

        [Fact]
        public void TestDeserializeFail()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());

            var resetEvent = new ManualResetEvent(false);

            const string id = "DeserializeFail";
            var msg = DateTime.Now.ToString();

            ShortCacheObject.FailDeserializeMessage = msg;
            dataManager.Load<ShortCacheObject>(id,
                sco =>
                {
                    ShortCacheObject.FailDeserializeMessage = null;
                    throw new InvalidOperationException();
                },
                ex =>
                {
                    ShortCacheObject.FailDeserializeMessage = null;
                    Assert.Equal(msg, ex.Message);
                    resetEvent.Set();
                });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestDeserializeFailUnhandled()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);

            const string id = "DeserializeFail";
            var msg = DateTime.Now.ToString();

            ShortCacheObject.FailDeserializeMessage = msg;

            EventHandler<ApplicationUnhandledExceptionEventArgs> handler = null;

            handler = (sender, e) =>
            {
                dataManager.UnhandledError -= handler;
                ShortCacheObject.FailDeserializeMessage = null;
                Assert.Equal(msg, e.ExceptionObject.Message);
                e.Handled = true;
                resetEvent.Set();
            };

            dataManager.UnhandledError += handler;

            dataManager.Load<ShortCacheObject>(id,
                sco =>
                {
                    ShortCacheObject.FailDeserializeMessage = null;
                    throw new InvalidOperationException();
                },
                null
                );
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestDeserializeCacheFail()
        {
            var storeProvider = new InMemoryStoreProvider();
            var dataManager = new DataManager(storeProvider, new WPUiDispatcher());

            var resetEvent = new ManualResetEvent(false);

            var loadContext = new LoadContext("DeserializeCacheFail");

            var uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), loadContext);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddSeconds(10));

            var ms = new MemoryStream();
            var data = Encoding.UTF8.GetBytes("garbage");
            ms.Write(data, 0, data.Length);
            storeProvider.Write(cii, data);

            var val = (int)DateTime.Now.Ticks;

            ShortCacheObject.DefaultIntValue = val;

            dataManager.Load<ShortCacheObject>(loadContext,
                sco =>
                {
                    ShortCacheObject.DefaultIntValue = 1234;
                    Assert.Equal(val, sco.IntProp);
                    resetEvent.Set();
                },
                ex => { throw new InvalidOperationException(); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestDataLoaderLiveLoad()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());

            var resetEvent = new ManualResetEvent(false);
            var dateValue = DateTime.Now.ToString();
            dataManager.Load<TestPoco>(dateValue,
                tp =>
                {
                    Assert.Equal(dateValue, tp.Value);
                    resetEvent.Set();
                },
                ex => { throw new InvalidOperationException(); }
                );
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestCleanup()
        {
            var storeProvider = new InMemoryStoreProvider();
            var dataManager = new DataManager(storeProvider, new WPUiDispatcher());

            var dval = DateTime.Now.ToString();
            var uniqueName = CacheEntry.BuildUniqueName(typeof(TestPoco), new LoadContext("foo"));
            var timestamp = DateTime.Now.AddDays(-2);
            var cii = new CacheItemInfo(uniqueName, timestamp, timestamp);
            storeProvider.Write(cii, Encoding.UTF8.GetBytes(dval));

            dataManager.Cleanup(DateTime.Now.AddDays(-1));
            var item = storeProvider.GetItem(uniqueName);
            Assert.Null(item);
        }

        [Fact]
        public void TestDataLoaderCacheLoad()
        {
            var storeProvider = new InMemoryStoreProvider();
            var dataManager = new DataManager(storeProvider, new WPUiDispatcher());

            var resetEvent = new ManualResetEvent(false);
            var loadContext = new LoadContext("TestPocoCache");
            var dval = DateTime.Now.ToString();
            var uniqueName = CacheEntry.BuildUniqueName(typeof(TestPoco), loadContext);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddSeconds(10));
            storeProvider.Write(cii, Encoding.UTF8.GetBytes(dval));

            dataManager.Load<TestPoco>(loadContext,
                tp2 =>
                {
                    Assert.Equal(dval, tp2.Value);
                    resetEvent.Set();
                },
                ex => { throw new InvalidOperationException(ex.Message); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestNoCacheObject()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var strValue = DateTime.Now.ToString();

            ShortCacheObject.DefaultStringValue = strValue;

            dataManager.Load<NoCacheObject>("nco",
                v1 =>
                {
                    Assert.Equal(strValue, v1.StringProp);

                    strValue = DateTime.Now.ToString();
                    ShortCacheObject.DefaultStringValue = strValue;

                    dataManager.Load<NoCacheObject>("nco",
                        v2 =>
                        {
                            Assert.Equal(strValue, v2.StringProp);
                            resetEvent.Set();
                        },
                        ex2 => { throw new InvalidOperationException(ex2.Message); });
                },
                ex => { throw new InvalidOperationException(ex.Message); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestValidCacheOnlyObjectWithValidCache()
        {
            var cacheValue = DateTime.Now.ToString();
            var newValue = DateTime.Now + "X";

            TestValidCacheCore(cacheValue, newValue, cacheValue, 10);
        }

        [Fact]
        public void TestValidCacheOnlyObjectWithInvalidCache()
        {
            var cacheValue = DateTime.Now.ToString();
            var newValue = DateTime.Now + "X";

            TestValidCacheCore(cacheValue, newValue, newValue, -1);
        }

        private void TestValidCacheCore(string cachedValue, string newValue, string expectedValue,
            int secondsUntilCacheExpires)
        {
            var storeProvider = new InMemoryStoreProvider();
            var dataManager = new DataManager(storeProvider, new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var loadContext = new LoadContext("VCO");
            var uniqueName = CacheEntry.BuildUniqueName(typeof(ValidCacheOnlyObject), loadContext);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddSeconds(secondsUntilCacheExpires));
            storeProvider.Write(cii,
                ShortCacheObject.ShortCacheObjectLoadRequest.WriteToStream(cachedValue, -1).GetBuffer());

            ShortCacheObject.DefaultStringValue = newValue;

            var x = dataManager.Load<ValidCacheOnlyObject>(loadContext,
                v1 =>
                {
                    Assert.Equal(expectedValue, v1.StringProp);
                    resetEvent.Set();
                },
                ex => { throw new InvalidOperationException(ex.Message); });
            Assert.NotNull(x);
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestNestedLoader()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var loadContext = new LoadContext("nlo");

            dataManager.Load<TestNestedLoaderObject>(loadContext,
                val =>
                {
                    Assert.Equal(loadContext.Identity, val.Value);
                    resetEvent.Set();
                },
                null);
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestRegisterProxy()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var propValue = ShortCacheObject.DefaultIntValue + 1;

            var sco = new ShortCacheObject(new LoadContext("Proxy"))
            {
                IntProp = propValue
            };

            dataManager.RegisterProxy(sco, true,
                obj =>
                {
                    Assert.NotEqual(propValue, obj.IntProp);
                    Assert.Equal(ShortCacheObject.DefaultIntValue, obj.IntProp);
                    Assert.Equal(ShortCacheObject.DefaultIntValue, sco.IntProp);
                    resetEvent.Set();
                });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestUnusedObjectGc()
        {
            var resetEvent = new ManualResetEvent(false);
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var loadContext = new LoadContext("GC");
            dataManager.Load<TestPoco>(loadContext,
                testPoco =>
                {
                    var entry = dataManager.Get<TestPoco>(loadContext);

                    Assert.False(entry.HasBeenGCd);

                    testPoco = null;

                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        GC.Collect();
                        Assert.True(entry.HasBeenGCd);
                        resetEvent.Set();
                    });
                },
                ex => { throw new InvalidOperationException(); });

            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestVariableExpirationDefault()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var date = default(DateTime);
            var lc = new VariLoadContext(date) {Foo = 1};

            dataManager.Load<VariableCacheObject>(lc,
                vco =>
                {
                    Assert.Null(vco.ExpirationTime);
                    Thread.Sleep(1000);

                    lc.Foo = 2;
                    dataManager.Load<VariableCacheObject>(lc,
                        vco2 =>
                        {
                            Assert.Null(vco2.ExpirationTime);
                            Assert.Equal(lc.Foo, vco2.Foo);

                            resetEvent.Set();
                        },
                        ex2 => { throw new InvalidOperationException(); });
                },
                ex => { throw new InvalidOperationException(); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestVariableExpirationTomorrow()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var date = DateTime.Now.AddDays(1);
            var lc = new VariLoadContext(date) {Foo = 1};

            dataManager.Load<VariableCacheObject>(lc,
                vco =>
                {
                    Assert.NotNull(vco.ExpirationTime);
                    Thread.Sleep(1000);

                    lc.Foo = 2;
                    dataManager.Load<VariableCacheObject>(lc,
                        vco2 =>
                        {
                            Assert.NotNull(vco2.ExpirationTime);
                            Assert.NotEqual(lc.Foo, vco2.Foo);
                            Assert.Equal(1, vco2.Foo);

                            resetEvent.Set();
                        },
                        ex2 => { throw new InvalidOperationException(); });
                },
                ex => { throw new InvalidOperationException(); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestDerivedClassWithInheritedLoader()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var id = DateTime.Now.GetHashCode().ToString();
            dataManager.Load<TestDerivedNestedLoaderObject>(id,
                testDerivedNestedLoaderObject =>
                {
                    Assert.Equal(id, testDerivedNestedLoaderObject.Value);
                    resetEvent.Set();
                },
                ex => { throw new InvalidOperationException(ex.Message); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
        }

        [Fact]
        public void TestDerivedLoaderAttribute()
        {
            var dataManager = new DataManager(new InMemoryStoreProvider(), new WPUiDispatcher());
            var resetEvent = new ManualResetEvent(false);
            var id = DateTime.Now.ToString();
            var valueReference = dataManager.Load<TestPocoDerived>(id,
                testPocoDerived =>
                {
                    Assert.Equal(id, testPocoDerived.Value);
                    resetEvent.Set();
                },
                ex => { throw new InvalidOperationException(ex.Message); });
            Assert.True(resetEvent.WaitOne(AsynchronousTestTimeout));
            Assert.NotNull(valueReference);
        }
    }
}