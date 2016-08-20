using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium.UnitTests
{
    [TestClass]
    public class ConcreteLiveListTests
    {
        public void ConcreteLiveListTest()
        {
            var list = new LiveList<Int32>();

            var wrapped = list.Wrap();

            var notifyPropertyChanged = wrapped as INotifyPropertyChanged;

            Assert.IsNotNull(notifyPropertyChanged);

            list.Add(1);

            PropertyChangedEventHandler shouldnthappen = (s, a) => Assert.Fail();

            // Should not fire on mere subscription:
            notifyPropertyChanged.PropertyChanged += shouldnthappen;
            notifyPropertyChanged.PropertyChanged -= shouldnthappen;

            // Should fire on mere subscription:
            int c = 0;
            list.Subscribe((type, item, key, previousKey) => ++c).Dispose();
            Assert.AreEqual(1, c);
        }
    }
}
