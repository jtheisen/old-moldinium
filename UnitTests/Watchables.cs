using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IronStone.Moldinium.UnitTests
{
    [TestClass]
    public class WatchablesTests
    {
        [TestMethod]
        public void Fundamentals()
        {
            var foo = Watchable.Var(42);

            Assert.AreEqual(42, foo.Value);

            foo.Value = 43;

            Assert.AreEqual(43, foo.Value);

            var bar = Watchable.Eval(() => foo.Value);

            Assert.AreEqual(43, bar.Value);

            foo.Value = 44;

            Assert.AreEqual(44, foo.Value);
            Assert.AreEqual(44, bar.Value);
        }

        [TestMethod]
        public void Caching()
        {
            var counter = new Counter();

            var foo = Watchable.Eval(() => counter.Get(42));

            Assert.AreEqual(0, counter.count);

            Ignore(foo.Value);

            Assert.AreEqual(1, counter.count);

            Ignore(foo.Value);

            Assert.AreEqual(1, counter.count);
        }

        [TestMethod]
        public void Exceptions()
        {
            var shouldThrow = Watchable.Var(true);

            var throwing = Watchable.Eval(() => { if (shouldThrow.Value) throw new InvalidOperationException(); else return 0; });

            AssertThrows(() => { Ignore(throwing.Value); }, typeof(InvalidOperationException));

            AssertThrows(() => { Ignore(throwing.Value); }, typeof(RethrowException));

            shouldThrow.Value = false;

            Assert.AreEqual(0, throwing.Value);
        }

        [TestMethod]
        public void ExceptionsIndirect()
        {
            var shouldThrow = Watchable.Var(true);

            var throwing = Watchable.Eval(() => { if (shouldThrow.Value) throw new InvalidOperationException(); else return 0; });

            var relay = Watchable.Eval(() => throwing.Value);

            AssertThrows(() => { Ignore(relay.Value); }, typeof(InvalidOperationException));

            AssertThrows(() => { Ignore(relay.Value); }, typeof(RethrowException));

            shouldThrow.Value = false;

            Assert.AreEqual(0, relay.Value);
        }

        struct Counter
        {
            public int count;

            public T Get<T>(T t)
            {
                ++count;
                return t;
            }
        }

        static void Ignore(Object dummy) { }

        static void AssertThrows(Action action, Type exceptionType)
        {
            try
            {
                action();

                Assert.Fail("Unexpectedly no exception.");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, exceptionType);
            }
        }
    }
}
