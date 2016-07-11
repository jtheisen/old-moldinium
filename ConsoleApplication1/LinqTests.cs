using System.Linq;
using System;
using System.Collections.Generic;
using IronStone.Moldinium;

namespace UnitTests.Lists
{
    public class LiveListTest
    {
        public LiveListTest()
        {
        }

        public void Wheres()
        {
            TestList<Int32>(
                e => e.Where(i => i % 2 == 0),
                l => l.Where(i => i % 2 == 0),
                GetSampleIntLists()
                );
        }

        IEnumerable<Func<Tuple<ILiveList<Int32>, Action<Action>>>> GetSampleIntLists()
        {
            return new Func<Tuple<ILiveList<Int32>, Action<Action>>>[] { () =>
            {
                var list = new LiveList<Int32>();

                return Tuple.Create<ILiveList<Int32>, Action<Action>>(list, o =>
                {
                    list.Add(0);
                    list.Add(1);
                    list.Add(2);
                    list.Add(3);

                    list.RemoveAt(0);
                    list.RemoveAt(2);
                    list.Insert(0, 1);
                    list.Insert(3, 1);

                    list.RemoveAt(2);
                });
            } };
        }

        void TestList<T>(
            Func<IEnumerable<T>, IEnumerable<T>> expectedSelector,
            Func<ILiveList<T>, ILiveList<T>> actualSelector,
            IEnumerable<Func<Tuple<ILiveList<T>, Action<Action>>>> samples
            )
        {
            foreach (var sample in samples)
            {
                var tuple = sample();

                var source = tuple.Item1;

                var work = tuple.Item2;

                var application = actualSelector(source);

                var actual = new ManifestedLiveList<T>(application);

                Action doCheck = () =>
                {
                    var expected = expectedSelector(source).ToArray();

                    //CollectionAssert.AreEqual(expected, actual);
                };

                using (source.Subscribe(v =>
                {
                    doCheck();
                }, null)
                )
                {
                    work(doCheck);
                }
            }
        }
    }
}
