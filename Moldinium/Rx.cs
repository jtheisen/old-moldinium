using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium
{
    public class ActionDisposable : IDisposable
    {
        public ActionDisposable(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            action?.Invoke();
        }

        Action action;
    }

    public class CompositeDisposable : IDisposable
    {
        public CompositeDisposable(IEnumerable<IDisposable> disposables)
        {
            this.disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable?.Dispose();
            }
        }

        IEnumerable<IDisposable> disposables;
    }

    public class ActionObserver<T> : IObserver<T>
    {
        public ActionObserver(Action<T> onNext)
        {
            this.onNext = onNext;
        }

        public void OnNext(T value)
        {
            onNext(value);
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        Action<T> onNext;
    }

    public interface ISubject<T> : IObservable<T>, IObserver<T>
    {
    }

    public class Subject<T> : ISubject<T>
    {
        public Subject()
        {
        }

        public void OnCompleted()
        {
            foreach (var item in observers)
            {
                item.OnCompleted();
            }
        }

        public void OnError(Exception error)
        {
            foreach (var item in observers)
            {
                item.OnError(error);
            }
        }

        public void OnNext(T value)
        {
            foreach (var item in observers)
            {
                item.OnNext(value);
            }
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            observers.Add(observer);

            return new ActionDisposable(() => observers.Remove(observer));
        }

        List<IObserver<T>> observers = new List<IObserver<T>>();
    }

    public static class RxExtensions
    {
        public static IDisposable Subscribe<T>(this IObservable<T> observable, Action<T> action)
        {
            return observable.Subscribe(new ActionObserver<T>(action));
        }
    }
}
