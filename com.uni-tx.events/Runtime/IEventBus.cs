using System;
using UniTx.Core;

namespace UniTx.Events
{
    /// <summary>
    /// Subscribes, unsubscribes and raises value-type events.
    /// </summary>
    public interface IEventBus : IInitializable, IResettable
    {
        /// <summary>
        /// Subscribes a listener to an event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
        /// <param name="action">The callback invoked when the event is raised.</param>
        /// <param name="priority">Invocation order; defaults to <see cref="Priority.Medium"/>.</param>
        void Subscribe<TEvent>(Action<TEvent> action, Priority priority = default)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Removes a previously registered listener. Safe to call during dispatch.
        /// </summary>
        /// <typeparam name="TEvent">The event type to unsubscribe from.</typeparam>
        /// <param name="action">The callback to remove.</param>
        void Unsubscribe<TEvent>(Action<TEvent> action)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Raises an event, invoking every subscriber in priority order.
        /// </summary>
        /// <typeparam name="TEvent">The event type being raised.</typeparam>
        /// <param name="event">The event instance to dispatch.</param>
        void Raise<TEvent>(TEvent @event)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Gets how many listeners are subscribed to an event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to count.</typeparam>
        /// <remarks>
        /// Useful as a leak check: a count that climbs across scene loads means something is
        /// subscribing without unsubscribing.
        /// </remarks>
        int SubscriberCount<TEvent>()
            where TEvent : struct, IEvent;

        /// <summary>
        /// Removes every listener for an event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to clear.</typeparam>
        void Clear<TEvent>()
            where TEvent : struct, IEvent;
    }
}
