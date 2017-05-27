using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace IronStone.Moldinium
{
    interface IWatchSubscription : IDisposable
    {
        IEnumerable<IWatchable> Dependencies { get; }
    }

    interface IWatchable
    {
        IWatchSubscription Subscribe(Action watcher);
    }

    interface IWatchableValue : IWatchable
    {
        Object UntypedValue { get; }

        Type Type { get; }
    }

    interface IWatchable<out T> : IWatchableValue
    {
        T Value { get; }
    }

    interface IWatchableVariable : IWatchable
    {
        Object UntypedValue { get; set; }
    }

    interface IWatchableVariable<T> : IWatchable<T>
    {
        new T Value { get; set; }
    }

    class ConcreteWatchable : IWatchable
    {
        Action watchers;

        internal String Name { get; set; }

        public IWatchSubscription Subscribe(Action watcher)
        {
            watchers += watcher;

            return WatchSubscription.Create(this, () => watchers -= watcher);
        }

        public void Notify()
        {
            watchers?.Invoke();
        }

        public override string ToString()
        {
            return Name;
        }
    }

    abstract class WatchableValueBase : ConcreteWatchable, IWatchableValue
    {
        public abstract Type Type { get; }

        Object IWatchableValue.UntypedValue
            => GetUntypedValue();

        Type IWatchableValue.Type => Type;

        protected abstract Object GetUntypedValue();
    }

    abstract class WatchableVariable : WatchableValueBase, IWatchableVariable
    {
        public Object UntypedValue
        {
            get
            {
                return GetUntypedValue();
            }

            set
            {
                SetUntypedValue(value);
            }
        }

        protected abstract void SetUntypedValue(Object value);
    }

    class WatchableVariable<T> : WatchableVariable, IWatchableVariable<T>
    {
        T value;

        public WatchableVariable()
        {
            this.value = default(T);
        }

        public WatchableVariable(T def)
        {
            this.value = def;
        }

        public T Value
        {
            get
            {
                Repository.Instance.NoteEvaluation(this);

                return value;
            }
            set
            {
                this.value = value;

                Notify();
            }
        }

        protected override Object GetUntypedValue() => Value;
        protected override void SetUntypedValue(Object value) => Value = (T)value;

        public override Type Type => typeof(T);
    }

    /// <summary>
    /// Encapsulates an exception encountered on a previous evaluation that is now rethrown on a repeated get operation.
    /// </summary>
    /// <seealso cref="System.Exception" />
    public class RethrowException : Exception
    {
        internal RethrowException(Exception innerException)
            : base("The value evaluation threw the inner exception last time it was attempted, and the dependencies didn't change since.", innerException)
        {
        }
    }

    class CachedComputedWatchable<T> : WatchableValueBase, IWatchable<T>
    {
        Func<T> evaluation;

        Action invalidateAndNotify;

        Boolean dirty = true;

        T value;

        Exception exception;

        SerialWatchSubscription subscriptions = new SerialWatchSubscription();

        public CachedComputedWatchable(Func<T> evaluation)
        {
            this.evaluation = evaluation;
            this.invalidateAndNotify = InvalidateAndNotify;
        }

        public T Value
        {
            get
            {
                EnsureUpdated();

                if (null != exception)
                    throw new RethrowException(exception);
                else
                    return value;
            }
        }

        void EnsureUpdated()
        {
            Repository.Instance.NoteEvaluation(this);

            if (dirty)
            {
                try
                {
                    value = Repository.Instance.EvaluateAndSubscribe(
                        Name, ref subscriptions, evaluation, invalidateAndNotify);

                    exception = null;

                    dirty = false;
                }
                catch (Exception ex)
                {
                    value = default(T);

                    exception = ex;

                    dirty = false;

                    throw;
                }
            }
        }

        public override Type Type => typeof(T);

        protected override Object GetUntypedValue() => Value;

        void InvalidateAndNotify()
        {
            dirty = true;
            EnsureUpdated();
            Notify();
        }
    }

    interface IWatchablesLogger
    {
        void BeginEvaluationFrame(Object evaluator);
        void CloseEvaluationFrameWithResult(Object result, IEnumerable<IWatchable> dependencies);
        void CloseEvaluationFrameWithException(Exception ex);
    }

    class WatchablesLogger : IWatchablesLogger
    {
        Stack<Object> evaluators = new Stack<Object>();

        public void WriteLine(String text)
        {
            System.Diagnostics.Debug.WriteLine(new string(' ', evaluators.Count * 2) + text);
        }

        public void BeginEvaluationFrame(object evaluator)
        {
            WriteLine($"Evaluating [{evaluator}]");

            evaluators.Push(evaluator);
        }

        public void CloseEvaluationFrameWithResult(object result, IEnumerable<IWatchable> dependencies)
        {
            var evaluator = evaluators.Pop();

            WriteLine($"Evaluating [{evaluator}] completed with ({result}), now listening to [{String.Join(", ", dependencies)}].");
        }

        public void CloseEvaluationFrameWithException(Exception ex)
        {
            var evaluator = evaluators.Pop();
        }
    }


    class Repository
    {
        public static Repository Instance { get { return instance.Value; } }

        static Lazy<Repository> instance = new Lazy<Repository>(() => new Repository());

        IWatchablesLogger logger = null;

        Repository()
        {
            evaluationStack.Push(new EvaluationRecord());
        }

        class EvaluationRecord
        {
            internal List<IWatchable> evaluatedWatchables = new List<IWatchable>();
        }

        Stack<EvaluationRecord> evaluationStack = new Stack<EvaluationRecord>();

        TSource Evaluate<TSource>(Object evaluator, Func<TSource> evaluation, out IEnumerable<IWatchable> dependencies)
        {
            logger?.BeginEvaluationFrame(evaluator);

            evaluationStack.Push(new EvaluationRecord());

            try
            {
                var result = evaluation();

                dependencies = evaluationStack.Pop().evaluatedWatchables;

                logger?.CloseEvaluationFrameWithResult(result, dependencies);

                return result;
            }
            catch (Exception ex)
            {
                dependencies = evaluationStack.Pop().evaluatedWatchables;

                logger?.CloseEvaluationFrameWithException(ex);

                throw;
            }
        }

        TSource Evaluate<TSource, TContext>(Object evaluator, Func<TContext, TSource> evaluation, TContext context, out IEnumerable<IWatchable> dependencies)
        {
            logger?.BeginEvaluationFrame(evaluator);

            evaluationStack.Push(new EvaluationRecord());

            try
            {
                var result =  evaluation(context);

                dependencies = evaluationStack.Pop().evaluatedWatchables;

                logger?.CloseEvaluationFrameWithResult(result, dependencies);

                return result;
            }
            catch (Exception ex)
            {
                evaluationStack.Pop();

                logger?.CloseEvaluationFrameWithException(ex);

                throw;
            }
        }

        internal TResult EvaluateAndSubscribe<TResult>(Object evaluator, ref SerialWatchSubscription subscriptions, Func<TResult> evaluation, Action onChange)
        {
            if (onChange == null) throw new ArgumentException(nameof(onChange));

            IEnumerable<IWatchable> dependencies = null;

            try
            {
                var result = Evaluate(evaluator, evaluation, out dependencies);

                SubscribeAll(ref subscriptions, dependencies, onChange);

                return result;
            }
            catch (Exception)
            {
                SubscribeAll(ref subscriptions, dependencies, onChange);

                throw;
            }
        }

        internal TResult EvaluateAndSubscribe<TResult, TContext>(Object evaluator, ref SerialWatchSubscription subscriptions, Func<TContext, TResult> evaluation, TContext context, Action onChange)
        {
            if (onChange == null) throw new ArgumentException(nameof(onChange));

            IEnumerable<IWatchable> dependencies = null;

            try
            {
                var result = Evaluate(evaluator, evaluation, context, out dependencies);

                SubscribeAll(ref subscriptions, dependencies, onChange);

                return result;
            }
            catch (Exception)
            {
                SubscribeAll(ref subscriptions, dependencies, onChange);

                throw;
            }
        }

        void SubscribeAll(ref SerialWatchSubscription subscriptions, IEnumerable<IWatchable> dependencies, Action onChange)
        {
            if (dependencies != null)
            {
                if (subscriptions == null)
                    subscriptions = new SerialWatchSubscription();

                subscriptions.Subscription = new CompositeWatchSubscription(
                    from d in dependencies select d.Subscribe(onChange));
            }
            else if (subscriptions != null)
            {
                subscriptions.Subscription = null;
            }
        }

        internal void NoteEvaluation(IWatchable watchable)
        {
            evaluationStack.Peek().evaluatedWatchables.Add(watchable);
        }
    }

    /// <summary>
    /// Represents a watchable variable that can be written to and read from. It can participate in automatic dependency tracking.
    /// </summary>
    /// <typeparam name="T">The type of the variable.</typeparam>
    public struct Var<T>
    {
        IWatchableVariable<T> watchable;

        internal Var(IWatchableVariable<T> watchable)
        {
            this.watchable = watchable;
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="Var{T}"/> to <see cref="Eval{T}"/>.
        /// </summary>
        /// <param name="var">The variable.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator Eval<T>(Var<T> var)
            => new Eval<T>(var.watchable);

        /// <summary>
        /// Gets or sets the watchable variable's value.
        /// </summary>
        /// <value>
        /// The value of the watchable variable. Neither the setter nor the getter will ever throw.
        /// </value>
        public T Value
        {
            get { return watchable.Value; }
            set { watchable.Value = value; }
        }
    }

    /// <summary>
    /// Represents a watchable evaluation that can be read from. It can participate in automatic dependency tracking.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <remarks>
    /// The evaluation happens only once initially and each time after a dependency changes,
    /// each time on first read. On other accesses to <see cref="Value"/>, a cached value is returned.
    /// If the evaluation throws the exception is not caught and will fall through to the read of <see cref="Value"/>.
    /// Such an exception is also cached and subsequent reads before dependencies change will receive a
    /// <see cref="RethrowException"/> with the old exception as the <see cref="Exception.InnerException"/>.
    /// From the point of dependency tracking, exceptions are just another "return value".
    /// </remarks>
    public struct Eval<T>
    {
        internal readonly IWatchable<T> watchable;

        internal Eval(IWatchable<T> watchable)
        {
            this.watchable = watchable;
        }

        /// <summary>
        /// Gets the value of the watchable evaluation.
        /// </summary>
        /// <value>
        /// The value that the evaluation represents. This will throw if the evaluation throws.
        /// </value>
        public T Value => watchable.Value;
    }

    /// <summary>
    /// Provides a set of factory methods for watchables.
    /// </summary>
    public static class Watchable
    {
        /// <summary>
        /// Creates a watchable variable.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="def">The initial value.</param>
        /// <returns>The new watchable variable.</returns>
        public static Var<T> Var<T>(T def = default(T))
            => new Var<T>(new WatchableVariable<T>(def));

        internal static IWatchableVariable VarForType(Type type)
            => (WatchableVariable)Activator.CreateInstance(
                typeof(WatchableVariable<>).MakeGenericType(type));

        /// <summary>
        /// Creates a watchable evaluation.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="evaluation">The function to evaluate.</param>
        /// <returns>The new watchable evaluation.</returns>
        public static Eval<T> Eval<T>(Func<T> evaluation)
            => new Eval<T>(new CachedComputedWatchable<T>(evaluation));

        static IDisposable Subscribe<T>(this Eval<T> eval, Action<T> watcher)
            => eval.watchable.Subscribe(() => watcher(eval.Value));
    }
}
