using System;
using System.Collections.Generic;
using System.Linq;

namespace IronStone.Moldinium
{
    internal static class InternalExtensions
    {
        public static Id? ApplyTo(this Id? source, IDictionary<Id, Id> dictionary)
        {
            return source.HasValue ? dictionary[source.Value] : (Id?)null;
        }

        public static void DisposeSafely(ref IDisposable disposable)
        {
            if (disposable == null) throw new Exception("Can't dispose null disposable.");
            disposable.Dispose();
            disposable = null;
        }
    }
}
