using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using Xunit;

namespace AgFx.Test
{
    public class BatchObservableCollectionTests
    {
        private const int ASYNCHRONOUS_TEST_TIMEOUT = 5000;

        [Fact]
        // Needs to run on the UI thread
        public void TestAddRange()
        {
            var resetEvent = new ManualResetEvent(false);

            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                var foos = new BatchObservableCollection<Foo>(4);

                var changeCount = -1;

                NotifyCollectionChangedEventHandler handler = null;

                handler = (s, a) =>
                {
                    if(changeCount > 0)
                    {
                        foos.CollectionChanged -= handler;
                        resetEvent.Set();
                    }
                    changeCount++;
                };

                foos.CollectionChanged += handler;

                var fooList = new List<Foo>();

                for(var i = 0; i < 7; i++)
                {
                    fooList.Add(new Foo(i, i.ToString()));
                }
                foos.AddRange(fooList);

                // just make sure the add isn't synhronous
                Assert.Equal(-1, changeCount);
            });
            Assert.True(resetEvent.WaitOne(ASYNCHRONOUS_TEST_TIMEOUT));
        }

        [Fact]
        public void TestMerge()
        {
            var original = new BatchObservableCollection<Foo>(10);

            original.Add(new Foo(1, "1"));
            original.Add(new Foo(2, "1"));
            original.Add(new Foo(3, "1"));
            original.Add(new Foo(5, "1"));
            original.Add(new Foo(7, "1"));


            var update = new BatchObservableCollection<Foo>(10);

            update.Add(new Foo(1, "_2"));
            update.Add(new Foo(3, "_2"));
            update.Add(new Foo(7, "_2"));
            update.Add(new Foo(9, "2"));
            update.Add(new Foo(10, "2"));


            original.CollectionChanged += (s, a) => { Assert.NotEqual(0, original.Count); };

            original.Merge(update, (x, y) => { return x.Id - y.Id; }, EquivelentItemMergeBehavior.ReplaceEqualItems);


            Assert.Equal(5, original.Count);

            Assert.Equal(original[0].Id, 1);
            Assert.Equal(original[0].Value, "_2");

            Assert.Equal(original[1].Id, 3);
            Assert.Equal(original[1].Value, "_2");

            Assert.Equal(original[2].Id, 7);
            Assert.Equal(original[2].Value, "_2");

            Assert.Equal(original[3].Id, 9);
            Assert.Equal(original[3].Value, "2");

            Assert.Equal(original[4].Id, 10);
            Assert.Equal(original[4].Value, "2");
        }

        [Fact]
        public void TestOffThread()
        {
            var ev = new ManualResetEvent(false);

            ThreadPool.QueueUserWorkItem(x =>
            {
                var batchObservableCollection = new BatchObservableCollection<Foo>(2);

                batchObservableCollection.Add(new Foo());
                batchObservableCollection.Add(new Foo());
                batchObservableCollection.Add(new Foo());

                Thread.Sleep(50);

                batchObservableCollection.Add(new Foo());
                batchObservableCollection.Add(new Foo());
                batchObservableCollection.Add(new Foo());
                batchObservableCollection.Add(new Foo());
                ev.Set();
            },
                null);

            ev.WaitOne();
        }

        public class Foo
        {
            public Foo()
            {
            }

            public Foo(int i, string s)
            {
                Id = i;
                Value = s;
            }

            public int Id { get; set; }
            public string Value { get; set; }

            public override string ToString()
            {
                return string.Format("ID={0}, Value={1}", Id, Value);
            }

            public override bool Equals(object obj)
            {
                var other = obj as Foo;
                if(other == null)
                {
                    return false;
                }
                return other.Id == Id;
            }

            public override int GetHashCode()
            {
                return Id.GetHashCode();
            }
        }
    }
}