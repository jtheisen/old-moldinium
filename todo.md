# What do we need for this library to have enough use to gain traction?

- The factory
- Watchables
- Live lists: Select, Where, Concat, OrderBy - but not grouping, paging and joins. 
- The advisory fairy.

This must be implemented, properly tested, documented and equipped with samples. 

# Todo

## Factory

## Watchables

- Blinkered()


## Live lists

- Implement a *checker*
- GroupBy
- Sort
- Join?
- the exception fairy to warn you about complexity problems
- aggregate syntax
- implement take/skip efficiently via SortedLiveList<T>
- make a generic refresh request test somehow
- implement everything from enumerable/queryable albeit not efficiently

optimization:
- make evaluations allocation-free in the absence of any watchables

## ILiveListSubscription

    public interface ILiveList<out T>
    {
        ILiveListSubscription Subscribe(DLiveListObserver<T> observer);
    }

and

    public interface ILiveListSubscription : IDisposable
    {
        void Refresh(Id id);
    }

is the better interface. It reduces the number of allocations needed and, more importantly, is easier to read in the implementations.

One would use this concrete implementation:

    public class LiveListSubscription<T> : ILiveListSubscription
    {
        Action<Id> refresh;
        IDisposable[] disposables;

        public LiveListSubscription(Action<Id> refresh, params IDisposable[] disposables)
        {
            this.disposables = disposables;
        }

        public void Dispose()
        {
            for (int i = 0; i < disposables.Length; ++i)
                InternalExtensions.DisposeSafely(ref disposables[i]);
        }

        public void Refresh(Id id)
        {
            refresh?.Invoke(id);
        }
    }



# Random thoughts

- tie live lists and manifestations:

TieLiveList *must* be a manifestation. That's because a second subscriber also needs all of the initial insertion elements, and getting them from upstream wouldn't be a tie.

It may make sense to have the tie implemented at the observer level, not the live list level.

Things like LiveLookup or LiveIndex are also sort of manifestations because only then they can expose their respective apis properly. So these two are really siblings of ManifestedLiveList.


- MVVM subscriptions on INotifyPropertyChange needs revisiting:

I don't believe it's correct.  INotifyPropertyChange should deliver all events on subscription, obviously. 


- The factory should manifest:

The factory could call .ManifastLazily or some such on live lists that are returned from getters.

- Guarding against various forms of reentrance

## The select with watchables problems

Consider this:

```
    from x in source
    select new Model
    {
        Foo = x.Something,
        Bar = x.Somethingelse
    }
```

We want the produced model change on having `Something` and `Somethingelse` change, rather than it being re-created.

### Solution

The `MergeReplaced` primitive caches the last removed element and re-inserts it if

- has the same identity according to some `IIdentity` interface to be defined or
- has the same identity according to some defintion provided as a parameter.

The re-inserted object gets its properties set from the new one it is identical with.

The only problem with this is

- Consumers still get a remove and subsequent reinsertion.
- The model needs setters.
- The whole select is reevaluated, not only the assignment that changed.








