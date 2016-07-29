using System.Linq;
using System;
using System.Collections.Generic;
using IronStone.Moldinium;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IronStone.Moldinium.UnitTests
{
    [TestClass]
    public class LiveListTest : LinqTestsCommon
    {
        [TestMethod]
        public void Concats()
        {
            TestList<Int32>(
                e => e.Concat(e),
                e => e.Concat(e),
                GetSampleIntLists()
                );
        }

        [TestMethod]
        public void Selects()
        {
            TestList<Int32>(
                e => e.Select(m => m),
                l => l.Select(m => m),
                GetSampleIntLists()
            );

            TestList<Foo>(
                e => e.Select(m => m),
                l => l.Select(m => m),
                GetTestListEventObservables()
            );
        }

        [TestMethod]
        public void SelectsWithReeval()
        {
            TestList<Foo>(
                e => e.Select(m => m.FooValue),
                l => l.Select(m => m.FooValue),
                GetTestListEventObservables()
            );
        }

        [TestMethod]
        public void Wheres()
        {
            TestList<Int32>(
                e => e.Where(i => i % 2 == 0),
                l => l.Where(i => i % 2 == 0),
                GetSampleIntLists()
                );
        }
    }
}
