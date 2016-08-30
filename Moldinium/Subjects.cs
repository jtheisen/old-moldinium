using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace IronStone.Moldinium
{
    // FIXME: Why isn't this manifested? A late subscription doesn't get the list!
    public class LiveListSubject<TSource> : ILiveList<TSource>, ILiveListObserver<TSource>
    {
        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer)
        {
            var info = new ObserverInfo() { observer = observer };

            info.subscription = LiveListSubscription.Create(
                id => { }, // FIXME: now that can't really work, can it?
                Disposable.Create(() => observers.Remove(info))
                );

            observers.Add(info);

            return info.subscription;
        }

        public void OnNext(ListEventType type, TSource item, Id id, Id? previousId)
        {
            foreach (var info in observers)
            {
                try
                {
                    info.observer(type, item, id, previousId);
                }
                catch (Exception)
                {
                    throw; // FIXME
                }
            }

            switch (type)
            {
                case ListEventType.Add:
                    ++Count;
                    break;
                case ListEventType.Remove:
                    --Count;
                    break;
            }
        }

        public Int32 Count { get; private set; }

        class ObserverInfo
        {
            public DLiveListObserver<TSource> observer;
            public ILiveListSubscription subscription;
        }

        List<ObserverInfo> observers = new List<ObserverInfo>();
    }
}
