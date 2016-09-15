using System;
using System.Collections.Generic;
using System.Linq;

namespace IronStone.Moldinium
{
    internal static class InternalExtensions
    {
        public static TResult? ApplyTo<TResult>(this Id? source, IDictionary<Id, TResult> dictionary)
            where TResult : struct
        {
            return source.HasValue ? dictionary[source.Value] : (TResult?)null;
        }

        public static TResult ApplyTo<TSource, TResult>(this TSource source, IDictionary<TSource, TResult> dictionary)
        {
            return dictionary[source];
        }

        public static void DisposeProperly<TDisposable>(ref TDisposable disposable)
            where TDisposable : IDisposable
        {
            if (disposable == null) throw new Exception("Can't dispose null disposable.");
            disposable.Dispose();
            disposable = default(TDisposable);
        }
    }
}
