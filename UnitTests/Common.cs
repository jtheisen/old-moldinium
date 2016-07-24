using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium.UnitTests
{
    public class LinqTestsCommon
    {
        [DebuggerDisplay("Foo Int={IntValue} Bool={BooleanValue} Foo=[{FooValue}]")]
        public abstract class Foo
        {
            public abstract Boolean BooleanValue { get; set; }

            public abstract Int32 IntValue { get; set; }

            public abstract Foo FooValue { get; set; }

            public Foo()
            {
                FooValue = this;
            }
        }

        protected IEnumerable<Func<Tuple<ILiveList<Int32>, Action<Action>>>> GetSampleIntLists()
        {
            yield return () =>
            {
                var list = new LiveList<Int32>();

                return Tuple.Create<ILiveList<Int32>, Action<Action>>(list, check =>
                {
                    list.Add(0); check();
                    list.Add(1); check();
                    list.Add(2); check();
                    list.Add(3); check();

                    list.RemoveAt(0); check();
                    list.RemoveAt(2); check();
                    list.Insert(0, 1); check();
                    list.Insert(3, 1); check();

                    list.RemoveAt(2); check();
                });
            };
        }

        protected IEnumerable<Func<Tuple<ILiveList<Foo>, Action<Action>>>> GetTestListEventObservables()
        {
            yield return () => {
                var list = new LiveList<Foo>();

                var factory = new ModelFactory();

                Func<Int32, Foo> create = i => factory.Create<Foo>(f => f.IntValue = i);

                return Tuple.Create<ILiveList<Foo>, Action<Action>>(list, check =>
                {
                    list.Add(create(0)); check();
                    list.Add(create(1)); check();
                    list.Add(create(2)); check();
                    list[0].BooleanValue = true; check();
                    list[2].BooleanValue = true; check();
                    list[0].BooleanValue = false; check();
                    list[2].BooleanValue = false; check();
                    list[0].FooValue = list[2]; check();
                    list[2].FooValue = list[0]; check();
                    list.RemoveAt(1); check();
                    list.Insert(0, create(42)); check();
                    list.Insert(1, create(43)); check();
                    list.RemoveAt(0); check();
                    list.RemoveAt(list.Count - 1); check();
                });
            };
        }

        protected void TestList<T>(
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

                    var x = expected[0].Equals(actual.First());

                    CollectionAssert.AreEqual(expected, actual);
                };

                work(doCheck);
            }
        }

    }
}
