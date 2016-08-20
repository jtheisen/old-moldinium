using System;
using System.Collections.Generic;

namespace IronStone.Moldinium
{
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
    /// A live list can also be subscribed on to listen for changes, not quite unlike an
    /// <seealso cref="INotifyCollectionChanged" />. Unlike <seealso cref="INotifyCollectionChanged" />,
    /// however, a live list will generate add events for all current items in the list
    /// on subscription, so subscription is the only method required to get all the contents
    /// of the live list.
    /// </summary>
    /// <typeparam name="T">The item type of the list.</typeparam>
    public interface ILiveList<out T>
    {
        IDisposable Subscribe(DLiveListObserver<T> observer, IObservable<Key> refreshRequested);
    }

    public delegate void DLiveListObserver<in T>(ListEventType type, T item, Key key, Key? previousKey);
}
