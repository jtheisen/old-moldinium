using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace IronStone.Moldinium
{
    interface IWatchable
    {
        IObservable<Unit> Changed { get; }
    }

    interface IWatchable<Type> : IWatchable
    {
        Type Value { get; }
    }

    abstract class WatchableVariable : IWatchable
    {
        public abstract Object UntypedValue { get; set; }

        public abstract IObservable<Unit> Changed { get; }

        public static WatchableVariable Create(Type type)
        {
            return (WatchableVariable)Activator.CreateInstance(
                typeof(WatchableVariable<>).MakeGenericType(type));
        }
    }

    class WatchableVariable<Type> : WatchableVariable, IWatchable<Type>
    {
        public WatchableVariable()
        {
            value = default(Type);
        }

        public WatchableVariable(Type def)
        {
            value = def;
        }

        public Type Value
        {
            get
            {
                Repository.Instance.NoteEvaluation(this);

                return value;
            }
            set
            {
                this.value = value;

                changed.OnNext(Unit.Default);
            }
        }

        public override IObservable<Unit> Changed {  get { return changed; } }

        public override object UntypedValue
        {
            get
            {
                return Value;
            }

            set
            {
                Value = (Type)value;
            }
        }

        Subject<Unit> changed = new Subject<Unit>();

        Type value;
    }

    class Watcher : IDisposable
    {
        public Watcher(Action action)
        {
            changed.Subscribe(Evaluate);

            this.action = action;

            Evaluate();
        }

        void Evaluate(Unit ignore = default(Unit))
        {
            subscriptions.Disposable = null;

            var dependencies = Repository.Instance.Evaluate(action);

            subscriptions.Disposable = new CompositeDisposable(
                from w in dependencies select w.Changed.Subscribe(changed));
        }

        public void Dispose()
        {
            subscriptions.Dispose();
        }

        Subject<Unit> changed = new Subject<Unit>();

        SerialDisposable subscriptions = new SerialDisposable();

        Action action;
    }

    class Watchable<Type> : IWatchable<Type>
    {
        public Watchable(Func<Type> getter)
        {
            changed.Subscribe(MarkAsDirty);

            this.getter = getter;
        }

        public Type Value
        {
            get
            {
                Repository.Instance.NoteEvaluation(this);

                if (dirty)
                {
                    subscriptions.Disposable = null;

                    IEnumerable<IWatchable> dependencies;

                    var value = Repository.Instance.Evaluate(getter, out dependencies);

                    subscriptions.Disposable = new CompositeDisposable(
                        from w in dependencies select w.Changed.Subscribe(changed));

                    dirty = false;

                    cache = value;
                }

                return cache;
            }
        }

        public IObservable<Unit> Changed { get { return changed; } }

        void MarkAsDirty(Unit u)
        {
            dirty = true;
        }

        Subject<Unit> changed = new Subject<Unit>();

        SerialDisposable subscriptions = new SerialDisposable();

        Boolean dirty = true;

        Type cache;

        Func<Type> getter;
    }

    class Repository
    {
        public static Repository Instance { get { return instance.Value; } }

        static Lazy<Repository> instance = new Lazy<Repository>(() => new Repository());

        Repository()
        {
            evaluationStack.Push(new EvaluationRecord());
        }

        class EvaluationRecord
        {
            internal List<IWatchable> evaluatedWatchables = new List<IWatchable>();
        }

        Stack<EvaluationRecord> evaluationStack = new Stack<EvaluationRecord>();

        public IEnumerable<IWatchable> Evaluate(Action action)
        {
            evaluationStack.Push(new EvaluationRecord());

            try
            {
                action();

                return evaluationStack.Pop().evaluatedWatchables;
            }
            catch (Exception)
            {
                evaluationStack.Pop();

                throw;
            }
        }

        public Type Evaluate<Type>(Func<Type> getter, out IEnumerable<IWatchable> dependencies)
        {
            evaluationStack.Push(new EvaluationRecord());

            try
            {
                var result = getter();

                dependencies = evaluationStack.Pop().evaluatedWatchables;

                return result;
            }
            catch (Exception)
            {
                evaluationStack.Pop();

                throw;
            }
        }

        public Type EvaluateAndSubscribe<Type>(Func<Type> getter, Action onChange)
        {
            if (onChange == null) throw new ArgumentException("onChange");

            IEnumerable<IWatchable> dependencies;

            var result = Evaluate(getter, out dependencies);

            foreach (var dependency in dependencies)
            {
                var subscription = dependency.Changed.Subscribe(u => onChange());
            }

            return result;
        }

        public TResult EvaluateAndSubscribe<TSource, TResult>(Func<TSource, TResult> selector, Action<TSource> onChange, TSource target)
        {
            // FIXME 1: The reference to selector and onChange must be weak!
            // FIXME 2: Don't create lambdas on each call, in particular don't allocate anything if there are no subscriptions

            if (onChange == null) throw new ArgumentException("onChange");

            return EvaluateAndSubscribe(() => selector(target), () => onChange(target));
        }

        internal void NoteEvaluation(IWatchable watchable)
        {
            evaluationStack.Peek().evaluatedWatchables.Add(watchable);
        }
    }

    static class Watchables
    {
        public static WatchableVariable<Type> CreateVariable<Type>(Type def = default(Type))
        {
            return new WatchableVariable<Type>(def);
        }

        public static IWatchable<Type> Create<Type>(Func<Type> getter)
        {
            return new Watchable<Type>(getter);
        }

        public static IDisposable Watch(Action action)
        {
            return new Watcher(action);
        }
    }
}
