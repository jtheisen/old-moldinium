using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium
{
    static class GroupOperations
    {
        public static IGroupOperations<T> Create<T>(Func<Expression, Expression, BinaryExpression> forward, Func<Expression, Expression, BinaryExpression> backward)
        {
            return Singleton<AdditionGroupOperations<T>>.Instance;
        }
    }

    interface IGroupOperations<T>
    {
        BinaryOperation<T> Forward { get; }
        BinaryOperation<T> Backward { get; }
    }

    delegate T BinaryOperation<T>(T lhs, T rhs);

    static class Singleton<T>
    {
        public static readonly T Instance = default(T);
    }

    class AdditionGroupOperations<T> : IGroupOperations<T>
    {
        public BinaryOperation<T> Forward { get; set; }
        public BinaryOperation<T> Backward { get; set; }
        public AdditionGroupOperations()
        {
            var p1 = Expression.Parameter(typeof(T));
            var p2 = Expression.Parameter(typeof(T));
            Forward = (BinaryOperation<T>)Expression
                .Lambda(Expression.Add(p1, p2), p1, p2)
                .Compile();
            Backward = (BinaryOperation<T>)Expression
                .Lambda(Expression.Subtract(p1, p2), p1, p2)
                .Compile();
        }
    }

    namespace Rx
    {
        public static partial class LiveList
        {
            private static IObservable<T> Aggregate<S, T>(ILiveList<S> s, Func<S, T> selector, IGroupOperations<T> go, T def = default(T))
            {
                return Observable.Create<T>(o =>
                {
                    T value = def;

                    BinaryOperation<T>
                        forward = go.Forward,
                        backward = go.Backward;

                    return s.Subscribe((type, item, key, previousKey) =>
                    {
                        switch (type)
                        {
                            case ListEventType.Add:
                                value = forward(value, selector(item));
                                break;
                            case ListEventType.Remove:
                                value = backward(value, selector(item));
                                break;
                            default:
                                break;
                        }

                        o.OnNext(value);
                    }, null);
                });
            }

            private static IObservable<T> GenericSum<S, T>(this ILiveList<S> source, Func<S, T> selector)
            {
                return Aggregate(source, selector, GroupOperations.Create<T>(Expression.AddChecked, Expression.SubtractChecked), default(T));
            }

            private static IObservable<Int32> Sum<S>(ILiveList<S> source, Func<S, Int32> selector)
            {
                return source.GenericSum(selector);
            }

            private static IObservable<Int64> Sum<S>(ILiveList<S> source, Func<S, Int64> selector)
            {
                return source.GenericSum(selector);
            }

            private static IObservable<T> GenericCount<S, T>(this ILiveList<S> source, T unit)
            {
                return Aggregate(source, s => unit, GroupOperations.Create<T>(Expression.AddChecked, Expression.SubtractChecked), default(T));
            }

            public static IObservable<Int32> Count<S>(ILiveList<S> source)
            {
                return source.GenericCount(1);
            }

            public static IObservable<Int64> CountLong<S>(ILiveList<S> source)
            {
                return source.GenericCount(1L);
            }
        }

        //namespace Ko
        //{
        //    public static partial class LiveList
        //    {
        //        public static Int32 Count<S>(ILiveList<S> source)
        //        {
        //            return Rx.LiveList.Count<S>(source).Watched("Count");
        //        }
        //    }
        //}
    }
}
