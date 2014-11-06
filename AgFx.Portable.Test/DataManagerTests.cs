using AgFx.Test.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgFx.Test
{
    [TestClass]
    public class DataManagerTests
    {
        [TestInitialize]
        public void Initialize()
        {
            DataManager.Current.DeleteCacheAsync().Wait();
            TestHelpers.InitializePriorityQueue();
        }

        [TestCleanup]
        public void Cleanup()
        {
            DataManager.Current.DeleteCacheAsync().Wait(); ;
        }

        [TestMethod]
        public async Task TestNoDataLoader()
        {
            try
            {
                await DataManager.Current.LoadAsync<object>("foo");
            }
            catch (InvalidOperationException)
            {
                return;
            }
            Assert.Fail();
        }

        [TestMethod]
        public void TestLoad()
        {
            var resetEvent = new ManualResetEvent(false);
            DataManager.Current.LoadAsync<ShortCacheObject>(ShortCacheObject.DefaultIdentifier,
                (obj) =>
                {
                    Assert.AreEqual(ShortCacheObject.DefaultStringValue, obj.StringProp);
                    Assert.AreEqual(ShortCacheObject.DefaultIntValue, obj.IntProp);
                    resetEvent.Set();
                },
                (ex) =>
                {
                    Assert.Fail(ex.Message);
                    resetEvent.Set();
                }
            );

            resetEvent.WaitOne();
        }

        [TestMethod]
        public async Task TestLoadFromCache()
        {
            string uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), new LoadContext("LFC"));
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddHours(1));
            await DataManager.StoreProvider.WriteAsync(cii, ShortCacheObject.SCOLoadRequest.WriteToStream("LoadFromCache", -1).GetBuffer());
            // sync load that value.
            //
            var value = await DataManager.Current.LoadFromCacheAsync<ShortCacheObject>("LFC");

            Assert.AreEqual("LoadFromCache", value.StringProp);
            Assert.AreEqual(-1, value.IntProp);
        }

        [TestMethod]
        public async Task TestRefreshBeforeCacheExpires()
        {
            var resetEvent = new ManualResetEvent(false);
            IUpdatable val = null;
            LoadContext lc = new LoadContext(ShortCacheObject.DefaultIdentifier);
            // write the cache entry
            //
            string uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), lc);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddHours(1));
            var t = DateTime.Now.ToString();
            await DataManager.StoreProvider.WriteAsync(cii, ShortCacheObject.SCOLoadRequest.WriteToStream(t, -1).GetBuffer());

            val = await DataManager.Current.LoadAsync<ShortCacheObject>(lc, (v) =>
            {
                string oldDefault = ShortCacheObject.DefaultStringValue;
                // we've got a value
                Assert.AreEqual(t, v.StringProp);
                ShortCacheObject.DefaultStringValue = DateTime.Now.ToString();

                // now request a new value via refresh.
                //
                DataManager.Current.RefreshAsync<ShortCacheObject>(ShortCacheObject.DefaultIdentifier,
                    (v2) =>
                    {
                        Assert.AreEqual(v2.StringProp, ShortCacheObject.DefaultStringValue);
                        ShortCacheObject.DefaultStringValue = oldDefault;
                        resetEvent.Set();
                    },
                    (ex2) =>
                    {
                        Assert.Fail(ex2.Message);
                        resetEvent.Set();
                    });
            }, (ex) =>
            {
                Assert.Fail(ex.Message);
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }

        [TestMethod]
        public async Task TestRefreshWithValidCache()
        {
            var resetEvent = new ManualResetEvent(false);
            IUpdatable val = null;
            LoadContext lc = new LoadContext(ShortCacheObject.DefaultIdentifier);
            await DataManager.Current.ClearAsync<ShortCacheObject>(lc);
            // write the cache entry
            //
            string uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), lc);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddMinutes(1));
            var t = DateTime.Now.ToString();
            await DataManager.StoreProvider.WriteAsync(cii, ShortCacheObject.SCOLoadRequest.WriteToStream(t, -1).GetBuffer());

            string oldDefault = ShortCacheObject.DefaultStringValue;
            ShortCacheObject.DefaultStringValue = DateTime.Now.Ticks.ToString();

            val = await DataManager.Current.RefreshAsync<ShortCacheObject>(lc,
                 (v) =>
                 {
                     // we've got a value
                     Assert.AreEqual(ShortCacheObject.DefaultStringValue, v.StringProp);
                     ShortCacheObject.DefaultStringValue = oldDefault;
                     resetEvent.Set();
                 },
                 (ex) =>
                 {
                     Assert.Fail(ex.Message);
                     resetEvent.Set();
                 });

            resetEvent.WaitOne();
        }

        [TestMethod]
        public async Task TestInvalidateFromCache()
        {
            LoadContext lc = new LoadContext("InvalidateFromCache");
            var time = DateTime.Now.ToString();

            // write the cache entry
            //
            string uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), lc);
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddHours(1));
            await DataManager.StoreProvider.WriteAsync(cii, ShortCacheObject.SCOLoadRequest.WriteToStream(time, -1).GetBuffer());

            await TestInvalidateCore(lc, time);
        }

        private async Task TestInvalidateCore(LoadContext lc, string time)
        {
            var resetEvent = new ManualResetEvent(false);
            await DataManager.Current.LoadAsync<ShortCacheObject>(lc, async (sco) =>
            {
                // verify we got the right thing
                //
                Assert.AreEqual(time, sco.StringProp);

                // load again to verify it's not going to change
                await DataManager.Current.LoadAsync<ShortCacheObject>(lc, async (sco2) =>
                {
                    // verify we got the right thing
                    //
                    Assert.AreEqual(time, sco2.StringProp);

                    // invalidate it
                    //
                    DataManager.Current.Invalidate<ShortCacheObject>(lc);

                    Thread.Sleep(250);

                    Assert.AreEqual(time, sco.StringProp);

                    if (time == ShortCacheObject.DefaultStringValue)
                    {
                        ShortCacheObject.DefaultStringValue = "DefaultString";
                    }

                    // load again to verify it's changed.
                    await DataManager.Current.LoadAsync<ShortCacheObject>(lc, (sco3) =>
                    {
                        // verify we got the right thing
                        //
                        Assert.AreNotEqual(time, sco3.StringProp);

                        ShortCacheObject.DefaultStringValue = "DefaultString";
                        resetEvent.Set();
                    }, (ex) =>
                    {
                        Assert.Fail(ex.ToString());
                        ShortCacheObject.DefaultStringValue = "DefaultString";
                        resetEvent.Set();
                    });
                }, (ex) =>
                {
                    Assert.Fail(ex.ToString());
                    ShortCacheObject.DefaultStringValue = "DefaultString";
                    resetEvent.Set();
                });
            }, (ex) =>
            {
                Assert.Fail(ex.ToString());
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }

        [TestMethod]
        public async Task TestInvalidateFromLive()
        {
            LoadContext lc = new LoadContext("InvalidateFromLive");
            await DataManager.Current.ClearAsync<ShortCacheObject>(lc);
            var time = DateTime.Now.ToString();

            ShortCacheObject.DefaultStringValue = DateTime.Now.ToString();

            await TestInvalidateCore(lc, ShortCacheObject.DefaultStringValue);
        }

        [TestMethod]
        public async Task TestRefreshOfLoadFail()
        {
            var resetEvent = new ManualResetEvent(false);
            ShortCacheObject val = null;

            ShortCacheObject.FailDeserializeMessage = "expected fail.";

            ShortCacheObject.SCOLoadRequest.Error = new InvalidCastException();

            val = await DataManager.Current.LoadAsync<ShortCacheObject>(ShortCacheObject.DefaultIdentifier,
                 (v) =>
                 {
                     ShortCacheObject.SCOLoadRequest.Error = null;
                     Assert.Fail("Load should have failed.");
                     resetEvent.Set();
                 },
                 async (ex) =>
                 {
                     string oldDefault = ShortCacheObject.DefaultStringValue;

                     // we should not get a value.
                     //
                     Assert.AreNotEqual(val.StringProp, oldDefault);
                     ShortCacheObject.DefaultStringValue = DateTime.Now.ToString();

                     ShortCacheObject.FailDeserializeMessage = null;
                     ShortCacheObject.SCOLoadRequest.Error = null;

                     // now request a new value via refresh.
                     //
                     await DataManager.Current.RefreshAsync<ShortCacheObject>(ShortCacheObject.DefaultIdentifier,
                         (v2) =>
                         {
                             Assert.AreEqual(v2.StringProp, ShortCacheObject.DefaultStringValue);
                             ShortCacheObject.DefaultStringValue = oldDefault;
                             resetEvent.Set();
                         },
                         (ex2) =>
                         {
                             Assert.Fail(ex2.Message);
                             resetEvent.Set();
                         });
                 });

            resetEvent.WaitOne();
        }

        // TODO: failing assertion in the LoadAsync body
        [TestMethod]
        public async Task TestLoadWithExpiredCache()
        {
            var resetEvent = new ManualResetEvent(false);

            string uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), new LoadContext("ExpiredItem"));
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddSeconds(-10));
            await DataManager.StoreProvider.WriteAsync(cii, ShortCacheObject.SCOLoadRequest.WriteToStream("ExpiredItemValue", -1).GetBuffer());
            // slow down the load so we get the cache value.
            var oldTime = ShortCacheObject.SCOLoadRequest.LoadTimeMs;
            ShortCacheObject.SCOLoadRequest.LoadTimeMs = 1000;

            await DataManager.Current.LoadAsync<ShortCacheObject>("ExpiredItem", (v) =>
            {
                Assert.IsNotNull(v);
                Assert.AreEqual("ExpiredItemValue", v.StringProp);
                resetEvent.Set();
            }, (ex) =>
            {
                Assert.Fail(ex.Message);
                resetEvent.Set();
            });

            resetEvent.WaitOne();
            ShortCacheObject.SCOLoadRequest.LoadTimeMs = oldTime;
        }

        [TestMethod]
        public void TestLoadRequestError()
        {
            var resetEvent = new ManualResetEvent(false);
            ShortCacheObject.SCOLoadRequest.Error = new Exception("blah");

            DataManager.Current.LoadAsync<ShortCacheObject>("LoadEror",
               (v) =>
               {
                   Assert.Fail("This should have failed");
                   resetEvent.Set();
               },
               (ex) =>
               {
                   Assert.IsNotNull(ex);
                   Assert.AreEqual(ShortCacheObject.SCOLoadRequest.Error, ex.InnerException);
                   ShortCacheObject.SCOLoadRequest.Error = null;
                   resetEvent.Set();
               });

            resetEvent.WaitOne();
        }

        [TestMethod]
        public void TestLoadRequestErrorUnhandled()
        {
            var resetEvent = new ManualResetEvent(false);

            ShortCacheObject.SCOLoadRequest.Error = new Exception("blah");

            EventHandler<DataManagerUnhandledExceptionEventArgs> handler = null;

            handler = (sender, e) =>
            {
                DataManager.Current.UnhandledError -= handler;
                Assert.IsNotNull(e.Exception);
                Assert.AreEqual(ShortCacheObject.SCOLoadRequest.Error, e.Exception.InnerException);
                ShortCacheObject.SCOLoadRequest.Error = null;

                resetEvent.Set();
                e.Handled = true;
            };

            DataManager.Current.UnhandledError += handler;

            DataManager.Current.LoadAsync<ShortCacheObject>("LoadEror", (v) =>
            {
                Assert.Fail("This should have failed");
                resetEvent.Set();
            }, null);

            resetEvent.WaitOne();
        }

        // Test fails silently
        [TestMethod]
        public void TestUpdating()
        {
            var resetEvent = new ManualResetEvent(false);
            DataManager.Current.LoadAsync<ShortCacheObject>("Updating", (s) =>
            {
                Assert.IsFalse(s.IsUpdating);

                PropertyChangedEventHandler h = null;

                bool gotUpdatingTrue = false;
                bool gotUpdatingFalse = false;

                h = (sender, prop) =>
                {
                    if (prop.PropertyName == "IsUpdating")
                    {
                        if (!gotUpdatingTrue)
                        {
                            Assert.IsTrue(s.IsUpdating);
                            gotUpdatingTrue = true;
                        }
                        else if (!gotUpdatingFalse)
                        {
                            Assert.IsFalse(s.IsUpdating);
                            gotUpdatingFalse = true;
                            s.PropertyChanged -= h;
                        }
                        else
                        {
                            Assert.Fail();
                        }
                    }
                };

                s.PropertyChanged += h;

                DataManager.Current.RefreshAsync<ShortCacheObject>("Updating", (s2) =>
                {
                    Assert.IsFalse(s.IsUpdating);
                    if (!gotUpdatingTrue || !gotUpdatingFalse)
                    {
                        Assert.Fail();
                    }
                    resetEvent.Set();
                }, null);
            }, null);

            resetEvent.WaitOne();
        }

        [TestMethod]
        public async Task TestDesrializeFail()
        {
            var resetEvent = new ManualResetEvent(false);
            string id = "DeserializeFail";
            await DataManager.Current.ClearAsync<ShortCacheObject>(id);
            string msg = DateTime.Now.ToString();

            ShortCacheObject.FailDeserializeMessage = msg;
            DataManager.Current.LoadAsync<ShortCacheObject>(id,
                (sco) =>
                {
                    Assert.Fail();
                    resetEvent.Set();
                },
                (ex) =>
                {
                    Assert.AreEqual(msg, ex.Message);
                    resetEvent.Set();
                });

            resetEvent.WaitOne();

            ShortCacheObject.FailDeserializeMessage = null;
        }

        [TestMethod]
        public async Task TestDeserializeFailUnhandledFiresDataManagerUnhandledError()
        {
            var resetEvent = new ManualResetEvent(false);
            string id = "DeserializeFail";
            await DataManager.Current.ClearAsync<ShortCacheObject>(id);
            string msg = DateTime.Now.ToString();

            ShortCacheObject.FailDeserializeMessage = msg;

            EventHandler<DataManagerUnhandledExceptionEventArgs> handler = null;

            handler = (sender, e) =>
            {
                DataManager.Current.UnhandledError -= handler;
                ShortCacheObject.FailDeserializeMessage = null;
                Assert.AreEqual(msg, e.Exception.Message);
                resetEvent.Set();
                e.Handled = true;
            };

            DataManager.Current.UnhandledError += handler;

            DataManager.Current.LoadAsync<ShortCacheObject>(id,
                (sco) =>
                {
                    Assert.Fail();
                    resetEvent.Set();
                },
                null);

            resetEvent.WaitOne();
            ShortCacheObject.FailDeserializeMessage = null;
        }

        [TestMethod]
        public async Task TestDeserializeCacheFail()
        {
            var resetEvent = new ManualResetEvent(false);
            string id = "DeserializeCacheFail";

            string uniqueName = CacheEntry.BuildUniqueName(typeof(ShortCacheObject), new LoadContext(id));
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddSeconds(10));

            MemoryStream ms = new MemoryStream();
            var data = Encoding.UTF8.GetBytes("garbage");
            ms.Write(data, 0, data.Length);
            await DataManager.StoreProvider.WriteAsync(cii, data);

            var val = (int)DateTime.Now.Ticks;

            ShortCacheObject.DefaultIntValue = val;

            DataManager.Current.LoadAsync<ShortCacheObject>(id,
                (sco) =>
                {
                    ShortCacheObject.DefaultIntValue = 1234;
                    Assert.AreEqual(val, sco.IntProp);
                    resetEvent.Set();
                },
                (ex) =>
                {
                    Assert.Fail();
                    resetEvent.Set();
                }
            );
            resetEvent.WaitOne();
        }

        [TestMethod]
        public void TestDataLoaderLiveLoad()
        {
            var resetEvent = new ManualResetEvent(false);
            var dval = DateTime.Now.ToString();

            DataManager.Current.LoadAsync<TestPoco>(dval, (tp) =>
            {
                Assert.AreEqual(dval, tp.Value);
                resetEvent.Set();
            }, (ex) =>
            {
                Assert.Fail();
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }

        // TODO: Mock StoreProvider, and test cleanup as a store provider function
        [TestMethod]
        public async Task TestCleanup()
        {
            var dval = DateTime.Now.ToString();
            string uniqueName = CacheEntry.BuildUniqueName(typeof(TestPoco), new LoadContext("foo"));
            DateTime timestamp = DateTime.Now.AddDays(-2);
            var cii = new CacheItemInfo(uniqueName, timestamp, timestamp);
            await DataManager.StoreProvider.WriteAsync(cii, UTF8Encoding.UTF8.GetBytes(dval));

            var item = await DataManager.StoreProvider.GetItemAsync(uniqueName);
            Assert.IsNotNull(item);
            
            await DataManager.Current.CleanupAsync(DateTime.Now.AddDays(-1));

            item = await DataManager.StoreProvider.GetItemAsync(uniqueName);
            Assert.IsNull(item);
        }

        [TestMethod]
        public async Task TestDataLoaderCacheLoad()
        {
            var resetEvent = new ManualResetEvent(false);

            var id = "TestPocoCache";
            var dval = DateTime.Now.ToString();
            string uniqueName = CacheEntry.BuildUniqueName(typeof(TestPoco), new LoadContext(id));
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddSeconds(10));
            await DataManager.StoreProvider.WriteAsync(cii, UTF8Encoding.UTF8.GetBytes(dval));
            Thread.Sleep(250); // let the write happen;
            DataManager.Current.LoadAsync<TestPoco>(id, (tp2) =>
            {
                Assert.AreEqual(dval, tp2.Value);
                resetEvent.Set();
            }, (ex) =>
            {
                Assert.Fail();
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }

        [DataLoader(typeof(TestDataLoader))]
        public class TestPoco
        {
            public string Value { get; set; }
        }

        public class TestPocoDerived : TestPoco
        {
        }

        public class TestDataLoader : IDataLoader<LoadContext>
        {
            public static string DeserializeValue = "DefaultDeserializeValue";


            public LoadRequest GetLoadRequest(LoadContext identifier, Type objectType)
            {
                return new TestDataLoaderRequest(identifier, identifier.Identity.ToString());
            }

            public object Deserialize(LoadContext identifier, Type objectType, Stream stream)
            {
                StreamReader sr = new StreamReader(stream);

                string val = sr.ReadToEnd();

                if (typeof(TestPoco).IsAssignableFrom(objectType))
                {
                    var p = (TestPoco)Activator.CreateInstance(objectType);
                    p.Value = val;
                    return p;
                }

                throw new InvalidOperationException();
            }

            public class TestDataLoaderRequest : LoadRequest
            {
                string val;

                public TestDataLoaderRequest(LoadContext lc, string v)
                    : base(lc)
                {
                    val = v;
                }
                public override Task<LoadRequestResult> Execute()
                {
                    MemoryStream ms = new MemoryStream();

                    StreamWriter sw = new StreamWriter(ms);
                    sw.Write(val);
                    sw.Flush();

                    ms.Seek(0, SeekOrigin.Begin);

                    return Task<LoadRequestResult>.FromResult(new LoadRequestResult(ms));
                }
            }
        }

        [TestMethod]
        public void TestNoCacheObject()
        {
            var resetEvent = new ManualResetEvent(false);

            var strValue = DateTime.Now.ToString();

            ShortCacheObject.DefaultStringValue = strValue;

            DataManager.Current.LoadAsync<NoCacheObject>("nco", (v1) =>
            {
                Assert.AreEqual(strValue, v1.StringProp);

                strValue = DateTime.Now.ToString();
                ShortCacheObject.DefaultStringValue = strValue;

                DataManager.Current.LoadAsync<NoCacheObject>("nco",
                    (v2) =>
                    {

                        Assert.AreEqual(strValue, v2.StringProp);
                        resetEvent.Set();
                    },
                    (ex2) =>
                    {
                        Assert.Fail();
                        resetEvent.Set();
                    });
            }, (ex) =>
            {
                Assert.Fail();
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }

        [TestMethod]
        public async Task TestValidCacheOnlyObjectWithValidCache()
        {
            var cacheValue = DateTime.Now.ToString();
            var newValue = DateTime.Now.ToString() + "X";

            await TestValidCacheCore(cacheValue, newValue, cacheValue, 10);
        }

        [TestMethod]
        public async Task TestValidCacheOnlyObjectWithInvalidCache()
        {
            var cacheValue = DateTime.Now.ToString();
            var newValue = DateTime.Now.ToString() + "X";

            await TestValidCacheCore(cacheValue, newValue, newValue, -1);
        }

        private async Task TestValidCacheCore(string cachedValue, string newValue, string expectedValue, int secondsUntilCacheExpires)
        {
            var resetEvent = new ManualResetEvent(false);
            string uniqueName = CacheEntry.BuildUniqueName(typeof(ValidCacheOnlyObject), new LoadContext("VCO"));
            var cii = new CacheItemInfo(uniqueName, DateTime.Now, DateTime.Now.AddSeconds(secondsUntilCacheExpires));
            await DataManager.StoreProvider.WriteAsync(cii, ShortCacheObject.SCOLoadRequest.WriteToStream(cachedValue, -1).GetBuffer());

            ShortCacheObject.DefaultStringValue = newValue;

            DataManager.Current.LoadAsync<ValidCacheOnlyObject>("VCO",
              (v1) =>
              {
                  Assert.AreEqual(expectedValue, v1.StringProp);
                  resetEvent.Set();
              },
              (ex) =>
              {
                  Assert.Fail();
                  resetEvent.Set();
              });
            resetEvent.WaitOne();
        }


        [TestMethod]
        public void TestNestedLoader()
        {
            var resetEvent = new ManualResetEvent(false);
            string id = "nlo";
            DataManager.Current.LoadAsync<TestNestedLoaderObject>(id,
                (val) =>
                {
                    Assert.AreEqual(id, val.StrValue);
                    resetEvent.Set();
                },
                null);

            resetEvent.WaitOne(); 
        }

        [TestMethod]
        public void TestRegisterProxy()
        {
            var resetEvent = new ManualResetEvent(false);
            int propValue = 999;

            ShortCacheObject sco = new ShortCacheObject("Proxy");
            sco.IntProp = propValue;
            Assert.AreNotEqual(propValue, ShortCacheObject.DefaultIntValue);

            DataManager.Current.RegisterProxy<ShortCacheObject>(sco, true, (obj) =>
            {
                Assert.AreNotEqual(propValue, obj.IntProp);
                Assert.AreEqual(ShortCacheObject.DefaultIntValue, obj.IntProp);
                Assert.AreEqual(ShortCacheObject.DefaultIntValue, sco.IntProp);
                resetEvent.Set();
            }, false);

            resetEvent.WaitOne();
        }

        [TestMethod]
        public void TestUnusedObjectGC()
        {
            var resetEvent = new ManualResetEvent(false);
            DataManager.Current.LoadAsync<TestPoco>("GC", async (tp2) =>
            {
                var entry = await DataManager.Current.Get<TestPoco>("GC");

                // TODO: Does this need to be UI thread?
                // TODO: Assertions?
                PriorityQueue.AddUiWorkItem(() =>
                {
                    GC.Collect();
                  //  Assert.IsTrue(entry.HasBeenGCd);
                    resetEvent.Set();
                });
               // Assert.IsFalse(entry.HasBeenGCd);
                tp2 = null;
            }, (ex) =>
            {
                Assert.Fail();
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }

        [TestMethod]
        public async Task TestVariableExpirationDefault()
        {
            var resetEvent = new ManualResetEvent(false);

            var date = default(DateTime);
            var lc = new VariLoadContext(date);
            lc.Foo = 1;

            DataManager.Current.LoadAsync<VariableCacheObject>(lc, (vco) =>
            {
                Assert.IsNull(vco.ExpirationTime);

                // wait a second
                Thread.Sleep(1000);

                lc.Foo = 2;

                // do another load - should not come from cache.
                //
                DataManager.Current.LoadAsync<VariableCacheObject>(lc, (vco2) =>
                {
                    Assert.IsNull(vco2.ExpirationTime);
                    Assert.AreEqual(lc.Foo, vco2.Foo);

                    resetEvent.Set();
                }, (ex2) =>
                {
                    Assert.Fail();
                    resetEvent.Set();
                });
            }, (ex) =>
            {
                Assert.Fail();
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }

        [TestMethod]
        public void TestVariableExpirationTomorrow()
        {
            var resetEvent = new ManualResetEvent(false);
            var date = DateTime.Now.AddDays(1);
            var lc = new VariLoadContext(date);
            lc.Foo = 1;

            DataManager.Current.LoadAsync<VariableCacheObject>(lc,
                (vco) =>
                {
                    Assert.IsNotNull(vco.ExpirationTime);

                    // wait a second
                    Thread.Sleep(1000);

                    lc.Foo = 2;

                    // do another load - SHOULD come from cache.
                    //
                    DataManager.Current.LoadAsync<VariableCacheObject>(lc, (vco2) =>
                    {
                        Assert.IsNotNull(vco2.ExpirationTime);
                        Assert.AreNotEqual(lc.Foo, vco2.Foo);
                        Assert.AreEqual(1, vco2.Foo);

                        resetEvent.Set();
                    }, (ex2) =>
                    {
                        Assert.Fail();
                        resetEvent.Set();
                    });
                },
                (ex) =>
                {
                    Assert.Fail();
                    resetEvent.Set();
                });
            
            resetEvent.WaitOne();
        }

        [TestMethod]
        public void TestDerivedClassWithInheritedLoader()
        {
            var resetEvent = new ManualResetEvent(false);

            var id = DateTime.Now.GetHashCode().ToString();
            DataManager.Current.LoadAsync<TestDerivedNestedLoaderObject>(id, (tdnlo) =>
            {
                Assert.AreEqual(id, tdnlo.StrValue);
                resetEvent.Set();
            }, (ex) =>
            {
                Assert.Fail();
                resetEvent.Set();
            });

            resetEvent.WaitOne();
        }

        [TestMethod]
        public void TestDerivedLoaderAttribute()
        {
            var resetEvent = new ManualResetEvent(false);
            string id = DateTime.Now.ToString();
            var obj = DataManager.Current.LoadAsync<TestPocoDerived>(id,
                (vm) =>
                {
                    Assert.AreEqual(id, vm.Value);
                    resetEvent.Set();
                },
                (ex) =>
                {
                    Assert.Fail();
                    resetEvent.Set();
                });
            resetEvent.WaitOne();
        }        
    }
}