using System;
using System.Collections.Generic;
using System.Linq;

namespace IronStone.Moldinium
{
    internal static class InternalExtensions
    {
        public static Key? ApplyTo(this Key? source, IDictionary<Key, Key> dictionary)
        {
            return source.HasValue ? dictionary[source.Value] : (Key?)null;
        }
    }
}
