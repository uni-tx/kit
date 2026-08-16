using System;

namespace UniTx.Events
{
    /// <summary>
    /// Static facade over the kit's event bus.
    /// </summary>
    public static class UniEvents
    {
        private static IEventBus _eventBus;

        /// <summary>
        /// Indicates whether the bus has been initialized.
        /// </summary>
        public static bool IsInitialized => _eventBus != null;

        /// <summary>
        /// Initializes with the default <see cref="PriorityEventBus"/>.
        /// </summary>
        public static void Initialize() => Initialize(new PriorityEventBus());

        /// <summary>
        /// Initializes with a custom bus implementation.
        /// </summary>
        /// <param name="eventBus">The bus to use.</param>
        public static void Initialize(IEventBus eventBus)
        {
            if (_eventBus != null)
            {
                throw new InvalidOperationException(
                    "UniEvents is already initialized. Call Reset() before initializing again.");
            }

            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _eventBus.Initialize();
        }

        /// <summary>
        /// Drops every subscription and releases the bus.
        /// </summary>
        public static void Reset()
        {
            if (_eventBus == null) return;

            _eventBus.Reset();
            _eventBus = null;
        }

        /// <summary>
        /// Subscribes a listener to an event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
        /// <param name="action">The callback invoked when the event is raised.</param>
        /// <param name="priority">Invocation order; defaults to <see cref="Priority.Medium"/>.</param>
        public static void Subscribe<TEvent>(Action<TEvent> action, Priority priority = default)
            where TEvent : struct, IEvent
            => Bus.Subscribe(action, priority);

        /// <summary>
        /// Removes a previously registered listener. Safe to call during dispatch.
        /// </summary>
        /// <typeparam name="TEvent">The event type to unsubscribe from.</typeparam>
        /// <param name="action">The callback to remove.</param>
        /// <remarks>
        /// A no-op when the bus is not initialized, so teardown ordering during shutdown or
        /// a scene unload never throws.
        /// </remarks>
        public static void Unsubscribe<TEvent>(Action<TEvent> action)
            where TEvent : struct, IEvent
            => _eventBus?.Unsubscribe(action);

        /// <summary>
        /// Raises an event, invoking every subscriber in priority order.
        /// </summary>
        /// <typeparam name="TEvent">The event type being raised.</typeparam>
        /// <param name="event">The event instance to dispatch.</param>
        public static void Raise<TEvent>(TEvent @event)
            where TEvent : struct, IEvent
            => Bus.Raise(@event);

        /// <summary>
        /// Gets how many listeners are subscribed to an event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to count.</typeparam>
        public static int SubscriberCount<TEvent>()
            where TEvent : struct, IEvent
            => _eventBus?.SubscriberCount<TEvent>() ?? 0;

        /// <summary>
        /// Removes every listener for an event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to clear.</typeparam>
        public static void Clear<TEvent>()
            where TEvent : struct, IEvent
            => _eventBus?.Clear<TEvent>();

        private static IEventBus Bus => _eventBus
            ?? throw new InvalidOperationException(
                "UniEvents is not initialized. Call UniEvents.Initialize() first — " +
                "UniTxStep does this during bootstrap.");
    }
}
