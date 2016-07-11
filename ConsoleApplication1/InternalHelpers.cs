using System;
using System.Collections.Generic;
using System.Linq;

namespace IronStone.Moldinium
{
    internal static class Extensions
    {
        public static CompositeDisposable CreateCompositeDisposable(params IDisposable[] disposables)
        {
            return new CompositeDisposable(from d in disposables where null != d select d);
        }

        public static CompositeDisposable CreateCompositeDisposable(IEnumerable<IDisposable> disposables)
        {
            return new CompositeDisposable(from d in disposables where null != d select d);
        }
    }
}
