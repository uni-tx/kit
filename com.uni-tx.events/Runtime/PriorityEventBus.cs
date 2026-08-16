using System;
using System.Collections.Generic;

namespace UniTx.Events
{
    /// <summary>
    /// Default <see cref="IEventBus"/>: priority-ordered, allocation-free on dispatch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Listeners live in a per-event-type <see cref="Channel{TEvent}"/> holding a typed
    /// <c>Action&lt;TEvent&gt;[]</c>. Nothing is boxed on subscribe and nothing is copied on
    /// raise, so a bus raised every frame contributes zero bytes of GC pressure — the point
    /// of using struct events in the first place.
    /// </para>
    /// <para>
    /// Unsubscribing from inside a handler is safe: the entry is tombstoned and the array is
    /// compacted once the outermost dispatch unwinds, so a re-entrant raise never walks a
    /// shifting array. Subscribing from inside a handler is also safe, but the new listener
    /// does not receive the event currently being dispatched.
    /// </para>
    /// </remarks>
    internal sealed class PriorityEventBus : IEventBus
    {
        private readonly Dictionary<Type, IChannel> _channels = new();

        /// <inheritdoc />
        public void Initialize() => _channels.Clear();

        /// <inheritdoc />
        public void Reset()
        {
            foreach (var channel in _channels.Values)
            {
                channel.Clear();
            }

            _channels.Clear();
        }

        /// <inheritdoc />
        public void Subscribe<TEvent>(Action<TEvent> action, Priority priority = default)
            where TEvent : struct, IEvent
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            GetOrCreateChannel<TEvent>().Add(action, priority);
        }

        /// <inheritdoc />
        public void Unsubscribe<TEvent>(Action<TEvent> action)
            where TEvent : struct, IEvent
        {
            if (action == null) return;

            if (_channels.TryGetValue(typeof(TEvent), out var channel))
            {
                ((Channel<TEvent>)channel).Remove(action);
            }
        }

        /// <inheritdoc />
        public void Raise<TEvent>(TEvent @event)
            where TEvent : struct, IEvent
        {
            if (_channels.TryGetValue(typeof(TEvent), out var channel))
            {
                ((Channel<TEvent>)channel).Raise(@event);
            }
        }

        /// <inheritdoc />
        public int SubscriberCount<TEvent>()
            where TEvent : struct, IEvent
            => _channels.TryGetValue(typeof(TEvent), out var channel)
                ? ((Channel<TEvent>)channel).Count
                : 0;

        /// <inheritdoc />
        public void Clear<TEvent>()
            where TEvent : struct, IEvent
        {
            if (_channels.TryGetValue(typeof(TEvent), out var channel))
            {
                channel.Clear();
            }
        }

        private Channel<TEvent> GetOrCreateChannel<TEvent>()
            where TEvent : struct, IEvent
        {
            var key = typeof(TEvent);

            if (_channels.TryGetValue(key, out var existing)) return (Channel<TEvent>)existing;

            var channel = new Channel<TEvent>();
            _channels[key] = channel;
            return channel;
        }

        private interface IChannel
        {
            void Clear();
        }

        private sealed class Channel<TEvent> : IChannel
            where TEvent : struct, IEvent
        {
            private const int InitialCapacity = 4;

            private Action<TEvent>[] _actions = new Action<TEvent>[InitialCapacity];
            private Priority[] _priorities = new Priority[InitialCapacity];
            private int _count;
            private int _dispatchDepth;
            private bool _hasTombstones;
            private bool _isUnsorted;

            public int Count => _count;

            public void Add(Action<TEvent> action, Priority priority)
            {
                if (_count == _actions.Length) Grow();

                if (_dispatchDepth > 0)
                {
                    // Mid-dispatch, appending is the only safe move: sorting into place would
                    // shift already-invoked entries forward and invoke them a second time.
                    // The raise loop is bounded by a snapshot count, so this entry sits out
                    // the current pass and the array is re-sorted once dispatch unwinds.
                    _actions[_count] = action;
                    _priorities[_count] = priority;
                    _count++;
                    _isUnsorted = true;
                    return;
                }

                InsertSorted(action, priority);
            }

            public void Remove(Action<TEvent> action)
            {
                for (var i = 0; i < _count; i++)
                {
                    if (_actions[i] != action) continue;

                    if (_dispatchDepth > 0)
                    {
                        // Mid-dispatch: tombstone rather than shift, so the in-flight loop
                        // keeps its indices valid.
                        _actions[i] = null;
                        _hasTombstones = true;
                    }
                    else
                    {
                        RemoveAt(i);
                    }

                    return;
                }
            }

            public void Raise(TEvent @event)
            {
                // Snapshot the count so a handler that subscribes does not extend this pass.
                var count = _count;

                _dispatchDepth++;

                try
                {
                    for (var i = 0; i < count; i++)
                    {
                        _actions[i]?.Invoke(@event);
                    }
                }
                finally
                {
                    _dispatchDepth--;

                    if (_dispatchDepth == 0)
                    {
                        if (_hasTombstones) Compact();
                        if (_isUnsorted) SortByPriority();
                    }
                }
            }

            public void Clear()
            {
                Array.Clear(_actions, 0, _count);
                Array.Clear(_priorities, 0, _count);
                _count = 0;
                _hasTombstones = false;
                _isUnsorted = false;
            }

            private void InsertSorted(Action<TEvent> action, Priority priority)
            {
                // Insert after the last entry of equal-or-higher priority: keeps the array
                // ordered without re-sorting on every subscribe, and preserves registration
                // order among equal priorities.
                var index = _count;

                while (index > 0 && _priorities[index - 1] > priority)
                {
                    _actions[index] = _actions[index - 1];
                    _priorities[index] = _priorities[index - 1];
                    index--;
                }

                _actions[index] = action;
                _priorities[index] = priority;
                _count++;
            }

            private void SortByPriority()
            {
                // Insertion sort: the array is already ordered apart from the few entries
                // appended during dispatch, so this is effectively linear here.
                for (var i = 1; i < _count; i++)
                {
                    var action = _actions[i];
                    var priority = _priorities[i];
                    var j = i - 1;

                    while (j >= 0 && _priorities[j] > priority)
                    {
                        _actions[j + 1] = _actions[j];
                        _priorities[j + 1] = _priorities[j];
                        j--;
                    }

                    _actions[j + 1] = action;
                    _priorities[j + 1] = priority;
                }

                _isUnsorted = false;
            }

            private void Compact()
            {
                var write = 0;

                for (var read = 0; read < _count; read++)
                {
                    if (_actions[read] == null) continue;

                    _actions[write] = _actions[read];
                    _priorities[write] = _priorities[read];
                    write++;
                }

                Array.Clear(_actions, write, _count - write);
                _count = write;
                _hasTombstones = false;
            }

            private void RemoveAt(int index)
            {
                _count--;
                Array.Copy(_actions, index + 1, _actions, index, _count - index);
                Array.Copy(_priorities, index + 1, _priorities, index, _count - index);
                _actions[_count] = null;
            }

            private void Grow()
            {
                var capacity = _actions.Length * 2;
                Array.Resize(ref _actions, capacity);
                Array.Resize(ref _priorities, capacity);
            }
        }
    }
}
