using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace IronStone.Moldinium
{
    [DebuggerDisplay("{Id}")]
    public struct Key : IEquatable<Key>, IComparable<Key>
    {
        internal Guid Id;

        public static Boolean operator ==(Key lhs, Key rhs)
        {
            return lhs.Id == rhs.Id;
        }

        public static Boolean operator !=(Key lhs, Key rhs)
        {
            return lhs.Id != rhs.Id;
        }

        public override Boolean Equals(Object obj)
        {
            return Id.Equals(obj);
        }

        public override Int32 GetHashCode()
        {
            return Id.GetHashCode();
        }

        public Boolean Equals(Key other)
        {
            return Id.Equals(other.Id);
        }

        public Int32 CompareTo(Key other)
        {
            return Id.CompareTo(other.Id);
        }
    }

    public static class KeyHelper
    {
        public static Key Create()
        {
            return new Key() { Id = Guid.NewGuid() };
        }
    }

    //public static class KeyExtensions
    //{
    //    public static Boolean Equals(this Key? lhs, Key? rhs)
    //    {
    //        return lhs.Value == rhs.Value;
    //    }
    //}


    /// <summary>
    /// Specifies whether an item is to be added or removed.
    /// </summary>
    public enum ListEventType
    {
        /// <summary>
        /// The item is to be inserted into the list.
        /// </summary>
        Add,
        /// <summary>
        /// The item is to be removed from the list.
        /// </summary>
        Remove
    }

    /// <summary>
    /// Provides factory methods for the creation of list events.
    /// </summary>
    public static class ListEvent
    {
        /// <summary>
        /// Creates a list event.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type">Whether to add or remove the item.</param>
        /// <param name="item">The item to add or remove.</param>
        /// <param name="key">The key for the item.</param>
        /// <param name="previousKey">The key of the previous item.</param>
        /// <returns>
        /// The list event.
        /// </returns>
        public static ListEvent<T> Make<T>(ListEventType type, T item, Key key, Key? previousKey)
        {
            return new ListEvent<T>(type, item, key, previousKey);
        }
    }

    /// <summary>
    /// Represents a change in a live list - either the addition or the removal of
    /// a single element.
    /// </summary>
    /// <typeparam name="T">The item type of the list.</typeparam>
    public struct ListEvent<T>
    {
        /// <summary>
        /// Gets the item type of the list.
        /// </summary>
        public ListEventType Type { get; private set; }
        /// <summary>
        /// Gets the item to add or remove.
        /// </summary>
        public T Item { get; private set; }
        /// <summary>
        /// Gets the key of the item.
        /// </summary>
        public Key Key { get; private set; }
        /// <summary>
        /// Gets the key of the previous item.
        /// </summary>
        public Key? PreviousKey { get; private set; }

        //public static readonly Func<ListEvent<T>, T> PreviousProjection = v => v.Previous;

        //public static readonly Func<ListEvent<T>, T> TargetProjection = v => v.Target;

        /// <summary>
        /// Initializes a new list event.
        /// </summary>
        /// <param name="type">Whether to add or remove the item.</param>
        /// <param name="target">The item to add or remove.</param>
        /// <param name="previous">The previous item.</param>
        public ListEvent(ListEventType type, T target, Key key, Key? previousKey)
        {
            Type = type;
            Item = target;
            Key = key;
            PreviousKey = previousKey;
        }
    }

    /// <summary>
    /// A live list is an <seealso cref="System.Collections.Generic.IEnumerable{T}" /> that
    /// can also be subscribed on to listen for changes, not quite unlike an
    /// <seealso cref="INotifyCollectionChanged" />. Unlike <seealso cref="INotifyCollectionChanged" />,
    /// however, a live list will generate add events for all current items in the list
    /// on subscription.
    /// </summary>
    /// <typeparam name="T">The item type of the list.</typeparam>
    /// <seealso cref="System.Collections.Generic.IEnumerable{T}" />
    public interface ILiveList<out T> : IEnumerable<T>
    {
        IDisposable Subscribe(DLiveListObserver<T> observer, IObservable<Key> refreshRequested);
    }

    // This interface is the key to efficient windowing, will have to build on this
    public interface IOrderedLiveList<out T> : ILiveList<T>
    {
        IDisposable Subscribe(DLiveListObserver<T> observer, IObservable<Key> refreshRequested, Int32 skip, Int32 take);
    }

    public delegate void DLiveListObserver<in T>(ListEventType type, T item, Key key, Key? previousKey);

    class ConcreteLiveList<T> : ILiveList<T>, INotifyCollectionChanged
    {
        public ConcreteLiveList(Func<DLiveListObserver<T>, IObservable<Key>, IDisposable> subscribe)
        {
            this.subscribe = subscribe;
        }

        public IDisposable Subscribe(DLiveListObserver<T> observer, IObservable<Key> refresh)
        {
            return subscribe(observer, refresh);
        }

        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
            add {
                if (null == collectionChanged)
                {
                    if (null != selfSubscription) throw new Exception(
                        "Unexpected state in ConcreteLiveList on collection change subscription.");
                    selfSubscription = subscribe(ProcessEvent, null);
                }

                collectionChanged += value;
            }

            remove {
                collectionChanged -= value;

                if (null == collectionChanged)
                {
                    if (null == selfSubscription) throw new Exception(
                        "Unexpected state in ConcreteLiveList on collection change unsubscription.");
                    selfSubscription.Dispose();
                    selfSubscription = null;
                }
            }
        }

        IEnumerator<T> GetEnumerator()
        {
            var lst = new List<T>();

            using (subscribe((type, item, key, previousKey) => lst.Add(item), null)) { }

            return lst.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        void ProcessEvent(ListEventType type, T item, Key key, Key? previousKey)
        {
            switch (type)
            {
                case ListEventType.Add:
                    collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Add, item));
                    break;
                case ListEventType.Remove:
                    collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Remove, item));
                    break;
                default:
                    break;
            }
        }

        Func<DLiveListObserver<T>, IObservable<Key>, IDisposable> subscribe;

        IDisposable selfSubscription;

        NotifyCollectionChangedEventHandler collectionChanged;
    }

    // Needed for tests, let's see what else we need it for.
    public class ManifestedLiveList<T> : ICollection, IEnumerable<T>, IEnumerable
    {
        public ManifestedLiveList(ILiveList<T> nested)
        {
            this.nested = nested;

            nested.Subscribe(OnNext, null);
        }

        void OnNext(ListEventType type, T item, Key key, Key? previousKey)
        {
            var index = previousKey.HasValue ? keys.IndexOf(previousKey.Value) : -1;

            switch (type)
            {
                case ListEventType.Add:
                    items.Insert(index + 1, item);
                    keys.Insert(index + 1, key);
                    break;
                case ListEventType.Remove:
                    items.RemoveAt(index + 1);
                    keys.RemoveAt(index + 1);
                    break;
                default:
                    break;
            }
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return items.GetEnumerator();
        }

        void ICollection.CopyTo(Array array, int index)
        {
            ((ICollection)items).CopyTo(array, index);
        }

        Int32 ICollection.Count { get { return items.Count; } }

        Object ICollection.SyncRoot { get { return this; } }

        Boolean ICollection.IsSynchronized { get { return false; } }

        List<T> items = new List<T>();
        List<Key> keys = new List<Key>();

        ILiveList<T> nested;
    }

    /// <summary>
    /// Provides various factories, helpers and extension methods for live lists.
    /// </summary>
    public static partial class LiveList
    {
        /// <summary>
        /// Converts an observable collection to a live list. This has currently O(n) complexity,
        /// but could potentially be implemented with O(1).
        /// </summary>
        /// <typeparam name="T">The item type of the list.</typeparam>
        /// <param name="collection">The observable collection.</param>
        /// <returns>The live list.</returns>
        public static ILiveList<T> ToLiveList<T>(this ObservableCollection<T> collection)
        {
            // This could also be implemented without making a copy, but it's more difficult to get right.

            LiveList<T> lst = new LiveList<T>(collection);

            collection.CollectionChanged += (s, a) =>
            {
                switch (a.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        lst.InsertRange(a.NewStartingIndex, a.NewItems.Cast<T>());
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        for (int i = 0; i < a.OldItems.Count; ++i)
                            lst.RemoveAt(a.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        lst.Clear();
                        break;
                    case NotifyCollectionChangedAction.Replace:
                    case NotifyCollectionChangedAction.Move:
                    default:
                        throw new NotImplementedException();
                }
            };

            return lst;
        }

        public static ILiveList<T> Create<T>(Func<DLiveListObserver<T>, IObservable<Key>, IDisposable> subscribe)
        {
            return new ConcreteLiveList<T>(subscribe);
        }

        //public static IObservable<T> While<T>(this IObservable<T> source, IObservable<Unit> control)
        //{
        //    return Observable.Create<T>(o =>
        //    {
        //        var subscription = source.Subscribe(o);

        //        var disposable = new SingleAssignmentDisposable() { Disposable = subscription };

        //        return Extensions.CreateCompositeDisposable(
        //            control.Subscribe(u => { }, e => { o.OnError(e); disposable.Dispose(); }, () => { o.OnCompleted(); disposable.Dispose(); }),
        //            disposable
        //        );
        //    });
        //}

        //public static IObservable<ListEvent<T>> ToObservable<T>(this ILiveList<T> list)
        //{
        //    return Observable.Create<ListEvent<T>>(o => list.Subscribe(o.OnNext, null));
        //}

        /// <summary>
        /// Concatenates two lists. This has an O(1) complexity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lhs">The first list.</param>
        /// <param name="rhs">The second list.</param>
        /// <returns>The concatenated list.</returns>
        public static ILiveList<T> Concat<T>(this ILiveList<T> lhs, ILiveList<T> rhs)
        {
            LiveList<ILiveList<T>> lists = new LiveList<ILiveList<T>>();
            lists.Add(lhs);
            lists.Add(rhs);
            return lists.Flatten();
        }

        class FlattenOuterListAttachment<T>
        {
            public IDisposable Subscription { get; set; }
            public Key? LastItemKey { get; set; }
            public Key? PreviousKey { get; set; }
            public Subject<Key> InboundRefreshRequest { get; set; }
            public Dictionary<Key, Key> IncomingToOutgoingKeyLookup { get; set; }
        }

        /// <summary>
        /// Flattens the specified list of lists. This has O(1) complexity.
        /// </summary>
        /// <typeparam name="T">The item type of the list.</typeparam>
        /// <param name="listOfLists">The list of lists.</param>
        /// <returns>The flattened list.</returns>
        public static ILiveList<T> Flatten<T>(this ILiveList<ILiveList<T>> listOfLists)
        {
            return LiveList.Create<T>((onNext, inboundRefreshRequest) =>
            {
                var outerAttachments = new Dictionary<Key, FlattenOuterListAttachment<T>>();

                // inner incoming key -> outer incoming key it belongs to
                var innerToOuterKeyLookup = new Dictionary<Key, Key>();

                var inboundRefreshRequestSubscription = inboundRefreshRequest?.Subscribe(key =>
                {
                    var listKey = innerToOuterKeyLookup[key];

                    var attachment = outerAttachments[listKey];

                    // We delegate the refresh request to the correct inner live list.
                    attachment.InboundRefreshRequest.OnNext(key);
                });

                var listOfListsSubscription = listOfLists.Subscribe((type, item, key, previousKey) =>
                {
                    switch (type)
                    {
                        case ListEventType.Add:
                            var attachment = new FlattenOuterListAttachment<T>();

                            outerAttachments.Add(key, attachment);

                            attachment.PreviousKey = previousKey;

                            attachment.InboundRefreshRequest = new Subject<Key>();

                            // We're translating keys as all incoming keys of all lists may not be unique.
                            attachment.IncomingToOutgoingKeyLookup = new Dictionary<Key, Key>();

                            var outboundRefreshRequests = new Subject<Key>();

                            attachment.InboundRefreshRequest.Subscribe(t => outboundRefreshRequests.OnNext(t));

                            attachment.Subscription = item.Subscribe((type2, item2, key2, previousKey2) =>
                            {
                                Key nkey2;

                                switch (type2)
                                {
                                    case ListEventType.Add:
                                        nkey2 = KeyHelper.Create();

                                        attachment.IncomingToOutgoingKeyLookup[key2] = nkey2;

                                        innerToOuterKeyLookup[key2] = key;

                                        if (previousKey2 == attachment.LastItemKey)
                                        {
                                            attachment.LastItemKey = key2;
                                        }
                                        break;
                                    case ListEventType.Remove:
                                        nkey2 = attachment.IncomingToOutgoingKeyLookup[key2];

                                        innerToOuterKeyLookup.Remove(key2);

                                        if (key2 == attachment.LastItemKey)
                                        {
                                            attachment.LastItemKey = previousKey2;
                                        }
                                        break;
                                    default:
                                        throw new Exception("Unexpected event type.");
                                }

                                if (previousKey2 == null)
                                {
                                    if (attachment.PreviousKey.HasValue)
                                    {
                                        var previousAttachment = outerAttachments[attachment.PreviousKey.Value];

                                        var npreviousKey2 = previousAttachment.LastItemKey
                                            .ApplyTo(attachment.IncomingToOutgoingKeyLookup);

                                        onNext(type2, item2, nkey2, npreviousKey2);
                                    }
                                    else
                                    {
                                        onNext(type2, item2, nkey2, null);
                                    }
                                }
                                else
                                {
                                    var npreviousKey2 = previousKey2.ApplyTo(attachment.IncomingToOutgoingKeyLookup);

                                    onNext(type2, item2, nkey2, npreviousKey2);
                                }

                            }, outboundRefreshRequests);
                            break;

                        case ListEventType.Remove:
                            var oldAttachment = outerAttachments[key];

                            oldAttachment.Subscription.Dispose();

                            outerAttachments.Remove(key);
                            break;
                    }
                }, null);

                return new CompositeDisposable(
                    inboundRefreshRequestSubscription ?? Disposable.Empty,
                    listOfListsSubscription
                    );
            });
        }
    }

    /// <summary>
    /// Represents a mutable list of objects that can be accessed by index, is also a live list and also
    /// implements <seealso cref="System.Collections.Specialized.INotifyCollectionChanged" />. It
    /// has many of the methods of the popular <seealso cref="System.Collections.Generic.List{T}" /> class.
    /// </summary>
    /// <typeparam name="T">The item type of the list.</typeparam>
    /// <seealso cref="MagicModels.ILiveList{T}" />
    /// <seealso cref="System.Collections.Specialized.INotifyCollectionChanged" />
    /// <seealso cref="System.Collections.Generic.IList{T}" />
    /// <seealso cref="System.Collections.Generic.ICollection{T}" />
    /// <seealso cref="System.Collections.Generic.IReadOnlyList{T}" />
    /// <seealso cref="System.Collections.Generic.IReadOnlyCollection{T}" />
    /// <seealso cref="System.Collections.Generic.IEnumerable{T}" />
    /// <seealso cref="System.Collections.IEnumerable" />
    public class LiveList<T> : ILiveList<T>, INotifyCollectionChanged, IList<T>, ICollection<T>, ICollection, IReadOnlyList<T>, IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LiveList{T}"/> class that
        /// is empty and has the default initial capacity.
        /// </summary>
        public LiveList()
        {
            items = new List<T>();
            keys = new List<Key>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LiveList{T}"/> class that
        /// is empty and has the specified initial capacity.
        /// </summary>
        /// <param name="capacity">The number of elements that the new list can initially store.</param>
        public LiveList(Int32 capacity)
        {
            items = new List<T>(capacity);
            keys = new List<Key>(capacity);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LiveList{T}"/> class that
        /// contains elements copied from the specified collection and has sufficient capacity
        /// to accommodate the number of elements copied.
        /// </summary>
        /// <param name="collection">The collection whose elements are copied to the new list.</param>
        public LiveList(IEnumerable<T> collection)
        {
            items = new List<T>(collection.Count());
            keys = new List<Key>(collection.Count());
            InsertRange(0, collection);
        }

        /// <summary>
        /// Gets or sets the total number of elements the internal data structure can hold
        /// without resizing.
        /// </summary>
        public int Capacity { get { return items.Capacity; } set { items.Capacity = value; keys.Capacity = value; } }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="System.Collections.Generic.ICollection{T}" />.
        /// </summary>
        public Int32 Count { get { OnEvaluated(); return items.Count; } }

        /// <summary>
        /// Adds an object to the end of the <see cref="System.Collections.Generic.ICollection{T}" />.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="System.Collections.Generic.ICollection{T}" />.
        /// The value must not be null or equal to any item already in the list.</param>
        public void Add(T item)
        {
            AssertItemNotNull(item);
            Insert(items.Count, item);
        }

        /// <summary>
        /// Adds the elements of the specified collection to the end of the <see cref="LiveList{T}"/>.
        /// </summary>
        /// <param name="collection">The collection whose elements should be added to the end of the
        /// <see cref="LiveList{T}"/>. The collection must not be null and must
        /// not contain null elements, duplicates, or elements that are already in the list.</param>
        public void AddRange(IEnumerable<T> collection)
        {
            InsertRange(items.Count, collection);
        }

        /// <summary>
        /// Returns a read-only <see cref="System.Collections.Generic.IList{T}" /> wrapper for the current collection.
        /// </summary>
        /// <returns>A <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}" /> that acts as a read-only wrapper. </returns>
        public ReadOnlyCollection<T> AsReadOnly()
        {
            return items.AsReadOnly();
        }

        /// <summary>
        /// Searches the entire sorted <see cref="LiveList{T}" /> for an element using
        /// the default comparer and returns the zero-based index of the element.
        /// </summary>
        /// <param name="item">The object to locate.</param>
        /// <returns>The zero-based index of item in the sorted <see cref="LiveList{T}" />,
        /// if item is found; otherwise, a negative number that is the bitwise complement
        /// of the index of the next element that is larger than item or, if there is no
        /// larger element, the bitwise complement of <see cref="LiveList{T}.Count"/>.</returns>
        public int BinarySearch(T item)
        {
            AssertItemNotNull(item);
            OnEvaluated();
            return items.BinarySearch(item);
        }

        /// <summary>
        /// Searches the entire sorted <see cref="LiveList{T}" /> for an element using
        /// the specified comparer and returns the zero-based index of the element.
        /// </summary>
        /// <param name="item">The object to locate.</param>
        /// <param name="comparer">The <see cref="System.Collections.Generic.IComparer{T}" /> implementation to use when comparing
        /// elements, or null to use the default comparer <see cref="System.Collections.Generic.Comparer{T}" />.Default.</param>
        /// <returns>
        /// The zero-based index of item in the sorted <see cref="LiveList{T}" />,
        /// if item is found; otherwise, a negative number that is the bitwise complement
        /// of the index of the next element that is larger than item or, if there is no
        /// larger element, the bitwise complement of <see cref="LiveList{T}.Count" />.
        /// </returns>
        public int BinarySearch(T item, IComparer<T> comparer)
        {
            AssertItemNotNull(item);
            OnEvaluated();
            return items.BinarySearch(item, comparer);
        }

        /// <summary>
        /// Searches the entire sorted <see cref="LiveList{T}" /> for an element using
        /// the specified comparer and returns the zero-based index of the element.
        /// </summary>
        /// <param name="index">The zero-based starting index of the range to search.</param>
        /// <param name="count">The length of the range to search.</param>
        /// <param name="item">The object to locate.</param>
        /// <param name="comparer">The <see cref="System.Collections.Generic.IComparer{T}" /> implementation to use when comparing
        /// elements, or null to use the default comparer <see cref="System.Collections.Generic.Comparer{T}" />.Default.</param>
        /// <returns>
        /// The zero-based index of item in the sorted <see cref="LiveList{T}" />,
        /// if item is found; otherwise, a negative number that is the bitwise complement
        /// of the index of the next element that is larger than item or, if there is no
        /// larger element, the bitwise complement of <see cref="LiveList{T}.Count" />.
        /// </returns>
        public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
        {
            AssertItemNotNull(item);
            OnEvaluated();
            return items.BinarySearch(index, count, item, comparer);
        }

        /// <summary>
        /// Removes all items from the <see cref="System.Collections.Generic.ICollection{T}" />.
        /// </summary>
        public void Clear()
        {
            while (items.Count > 0)
            {
                RemoveLast();
            }
        }

        /// <summary>
        /// Determines whether the <see cref="System.Collections.Generic.ICollection{T}" /> contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in the <see cref="System.Collections.Generic.ICollection{T}" />.</param>
        /// <returns>
        /// true if <paramref name="item" /> is found in the <see cref="System.Collections.Generic.ICollection{T}" />; otherwise, false.
        /// </returns>
        public bool Contains(T item) { AssertItemNotNull(item); OnEvaluated(); return items.Contains(item); }

        /// <summary>
        /// Copies the entire <see cref="LiveList{T}"/> to a compatible one-dimensional
        /// array, starting at the beginning of the target array.
        /// </summary>
        /// <param name="array">The one-dimensional System.Array that is the destination of the elements copied
        /// from <see cref="LiveList{T}"/>. The System.Array must have zero-based indexing.</param>
        public void CopyTo(T[] array) { OnEvaluated(); items.CopyTo(array); }

        /// <summary>
        /// Copies the entire <see cref="LiveList{T}" /> to a compatible one-dimensional
        /// array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">The one-dimensional System.Array that is the destination of the elements copied
        /// from <see cref="LiveList{T}" />. The System.Array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex) { OnEvaluated(); items.CopyTo(array, arrayIndex); }

        /// <summary>
        /// Copies the entire <see cref="LiveList{T}" /> to a compatible one-dimensional
        /// array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="index">The zero-based index in the source <see cref="LiveList{T}" /> at which
        /// copying begins.</param>
        /// <param name="array">The one-dimensional System.Array that is the destination of the elements copied
        /// from <see cref="LiveList{T}" />. The System.Array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <param name="count">The number of elements to copy.</param>
        public void CopyTo(int index, T[] array, int arrayIndex, int count) { OnEvaluated(); items.CopyTo(index, array, arrayIndex, count); }

        /// <summary>
        /// Determines whether the <see cref="LiveList{T}" /> contains elements that
        /// match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the elements to
        /// search for.</param>
        /// <returns>true if the <see cref="LiveList{T}" /> contains one or more elements that
        /// match the conditions defined by the specified predicate; otherwise, false.</returns>
        public bool Exists(Predicate<T> match) { OnEvaluated(); return items.Exists(match); }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the first occurrence within the entire <see cref="LiveList{T}" />.
        /// </summary>
        /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
        /// search for.</param>
        /// <returns>The first element that matches the conditions defined by the specified predicate,
        ///  if found; otherwise, the default value for type T.</returns>
        public T Find(Predicate<T> match) { OnEvaluated(); return items.Find(match); }

        /// <summary>
        /// Retrieves all the elements that match the conditions defined by the specified
        /// predicate.
        /// </summary>
        /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
        /// search for.</param>
        /// <returns>A <see cref="System.Collections.Generic.List{T}" /> containing all the elements that match the
        /// conditions defined by the specified predicate, if found; otherwise, an empty
        /// <see cref="System.Collections.Generic.List{T}" />.</returns>
        public List<T> FindAll(Predicate<T> match) { OnEvaluated(); return items.FindAll(match); }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the first occurrence within the
        /// entire <see cref="LiveList{T}" />.
        /// </summary>
        /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
        /// search for.</param>
        /// <returns>The zero-based index of the first occurrence of an element that matches the conditions
        /// defined by match, if found; otherwise, –1.</returns>
        public int FindIndex(Predicate<T> match) { OnEvaluated(); return items.FindIndex(match); }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the first occurrence within the
        /// range of elements in the <see cref="LiveList{T}" /> that extends from
        /// the specified index to the last element.
        /// </summary>
        /// <param name="startIndex">The zero-based starting index of the search.</param>
        /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
        /// search for.</param>
        /// <returns>
        /// The zero-based index of the first occurrence of an element that matches the conditions
        /// defined by match, if found; otherwise, –1.
        /// </returns>
        public int FindIndex(int startIndex, Predicate<T> match) { OnEvaluated(); return items.FindIndex(startIndex, match); }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the first occurrence within the
        /// range of elements in the <see cref="LiveList{T}" /> that extends from
        /// the specified index and contains the specified number of elements.
        /// </summary>
        /// <param name="startIndex">The zero-based starting index of the search.</param>
        /// <param name="count">The number of elements in the section to search.</param>
        /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
        /// search for.</param>
        /// <returns>
        /// The zero-based index of the first occurrence of an element that matches the conditions
        /// defined by match, if found; otherwise, –1.
        /// </returns>
        public int FindIndex(int startIndex, int count, Predicate<T> match) { OnEvaluated(); return items.FindIndex(startIndex, count, match); }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="System.Collections.Generic.ICollection{T}" />.
        /// </summary>
        /// <param name="item">The object to remove from the <see cref="System.Collections.Generic.ICollection{T}" />.</param>
        /// <returns>
        /// true if <paramref name="item" /> was successfully removed from the <see <see cref="cref="System.Collections.Generic.ICollection{T}" />" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection{T}" />.
        /// </returns>
        public bool Remove(T item)
        {
            AssertItemNotNull(item);
            OnEvaluated();
            var index = items.IndexOf(item);
            if (index < 0) return false;
            RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="System.Collections.Generic.IList{T}" />.
        /// </summary>
        /// <param name="item">The object to locate in the <see cref="System.Collections.Generic.IList{T}" />.</param>
        /// <returns>
        /// The index of <paramref name="item" /> if found in the list; otherwise, -1.
        /// </returns>
        public int IndexOf(T item) { AssertItemNotNull(item); OnEvaluated(); return items.IndexOf(item); }

        // ** Core method **
        /// <summary>
        /// Removes the <see cref="System.Collections.Generic.IList{T}" /> item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        public void RemoveAt(int index)
        {
            var removed = items[index];
            var key = keys[index];
            items.RemoveAt(index);
            keys.RemoveAt(index);
            events.OnNext(ListEvent.Make(ListEventType.Remove, removed, key, GetPreviousKey(index)));
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, index));
        }

        /// <summary>
        /// Removes the last item in the list.
        /// </summary>
        public void RemoveLast()
        {
            RemoveAt(items.Count - 1);
        }

        /// <summary>
        /// Gets or sets the <see cref="T"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="T"/>.
        /// </value>
        /// <param name="index">The index.</param>
        public T this[int index] {
            get { OnEvaluated(); return items[index]; }
            set {
                AssertItemNotNull(value, "value");
                RemoveAt(index);
                Insert(index, value);
            }
        }

        // ** Core method **        
        /// <summary>
        /// Inserts an item to the <see cref="System.Collections.Generic.IList{T}" /> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
        /// <param name="item">The object to insert into the <see cref="System.Collections.Generic.IList{T}" />.</param>
        public void Insert(int index, T item)
        {
            AssertItemNotNull(item);
            var key = KeyHelper.Create();
            items.Insert(index, item);
            keys.Insert(index, key);
            events.OnNext(ListEvent.Make(ListEventType.Add, item, key, GetPreviousKey(index)));
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        /// <summary>
        /// Inserts the elements of a collection into the <see cref="LiveList{T}" />
        /// </summary>
        /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
        /// <param name="collection">The collection whose elements should be inserted into the <see cref="LiveList{T}" />.
        /// The collection itself cannot be null, but it can contain elements that are null,
        /// if type T is a reference type.</param>
        public void InsertRange(int index, IEnumerable<T> collection)
        {
            if (collection.Any(v => null == v))
                throw new ArgumentException("Argument range contains null items.");

            var i = index;
            foreach (var item in collection)
                Insert(i++, item);
        }

        /// <summary>
        /// Occurs when the collection changes.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        Key? GetPreviousKey(Int32 index)
        {
            return index > 0 ? keys[index - 1] : (Key?)null;
        }

        void AssertItemNotNull(T item, String paramName = "item")
        {
            if (null == item) throw new ArgumentNullException(paramName);
        }

        protected virtual void OnEvaluated() { }

        public IDisposable Subscribe(DLiveListObserver<T> onNext, IObservable<Key> refreshRequested = null)
        {
            for (var i = 0; i < items.Count; ++i)
                onNext(ListEventType.Add, items[i], keys[i], i > 0 ? keys[i - 1] : (Key?)null);

            return new CompositeDisposable(
                refreshRequested?.Subscribe(key =>
                {
                    var index = keys.IndexOf(key);

                    if (index < 0) throw new Exception("Item not found.");

                    var item = items[index];

                    var previousKey = index == 0 ? (Key?)null : keys[index - 1];

                    onNext(ListEventType.Remove, item, key, previousKey);
                    onNext(ListEventType.Add, item, key, previousKey);
                }) ?? Disposable.Empty,
                events.Subscribe(v => onNext(v.Type, v.Item, v.Key, v.PreviousKey))
            );
        }

        Boolean ICollection<T>.IsReadOnly { get { return true; } }

        Int32 ICollection.Count { get { return Count; } }

        Object ICollection.SyncRoot { get { return this; } }

        bool ICollection.IsSynchronized { get { return false; } }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() { OnEvaluated(); return items.GetEnumerator(); }

        IEnumerator IEnumerable.GetEnumerator() { OnEvaluated(); return items.GetEnumerator(); }

        void ICollection.CopyTo(Array array, int index)
        {
            OnEvaluated();
            (items as ICollection).CopyTo(array, index);
        }


        ISubject<ListEvent<T>> events = new Subject<ListEvent<T>>();

        List<T> items;
        List<Key> keys;

        // Missing List<T> methods:

        // ConvertAll
        // FindLast
        // FindLastIndex (3x)
        // ForEach
        // GetRange
        // RemoveAll
        // RemoveRange
        // Reverse (2x)
        // Sort (4x)
        // TrimExcess
        // TrueForAll

        // LastIndexOf: makes no sense here

        // To correct ghost-doc-screwups: Replace ...
        // To correct copy-pasted documentation: Replace \b([^ ]*?)`1\b with <see cref="$1{T}" />
    }
}
