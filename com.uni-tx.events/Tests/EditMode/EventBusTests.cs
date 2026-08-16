using System;
using System.Collections.Generic;
using NUnit.Framework;
// AllocatingGCMemory() is an extension on NUnit's ConstraintExpression, so the namespace
// import is required as well as the alias; the alias keeps NUnit's own `Is` unambiguous.
using UnityEngine.TestTools.Constraints;
using UnityIs = UnityEngine.TestTools.Constraints.Is;

namespace UniTx.Events.Tests.EditMode
{
    public struct TestEvent : IEvent
    {
        public int Value;
    }

    public struct OtherEvent : IEvent
    {
        public int Value;
    }

    public class EventBusTests
    {
        private IEventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new PriorityEventBus();
            _bus.Initialize();
        }

        [TearDown]
        public void TearDown() => _bus.Reset();

        [Test]
        public void Subscribe_Raise_InvokesListener()
        {
            var received = 0;
            _bus.Subscribe<TestEvent>(e => received = e.Value);

            _bus.Raise(new TestEvent { Value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Unsubscribe_PreventsFurtherInvocations()
        {
            var count = 0;
            void Handler(TestEvent e) => count++;

            _bus.Subscribe<TestEvent>(Handler);
            _bus.Raise(new TestEvent());
            _bus.Unsubscribe<TestEvent>(Handler);
            _bus.Raise(new TestEvent());

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Priority_SortsHighestFirst()
        {
            var order = new List<int>();
            _bus.Subscribe<TestEvent>(e => order.Add(1), Priority.Low);
            _bus.Subscribe<TestEvent>(e => order.Add(2), Priority.Highest);
            _bus.Subscribe<TestEvent>(e => order.Add(3), Priority.Medium);
            _bus.Subscribe<TestEvent>(e => order.Add(4), Priority.Lowest);
            _bus.Subscribe<TestEvent>(e => order.Add(5), Priority.High);

            _bus.Raise(new TestEvent());

            CollectionAssert.AreEqual(new[] { 2, 5, 3, 1, 4 }, order);
        }

        [Test]
        public void Priority_DefaultIsMedium()
        {
            // Medium is deliberately 0 so `default` keeps meaning "middle", even though the
            // enum is laid out negative-to-positive for direct comparison.
            Assert.AreEqual(Priority.Medium, default(Priority));
        }

        [Test]
        public void Priority_EqualPriorities_KeepRegistrationOrder()
        {
            var order = new List<int>();
            _bus.Subscribe<TestEvent>(e => order.Add(1), Priority.High);
            _bus.Subscribe<TestEvent>(e => order.Add(2), Priority.High);
            _bus.Subscribe<TestEvent>(e => order.Add(3), Priority.High);

            _bus.Raise(new TestEvent());

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void Raise_WithoutSubscribers_DoesNotThrow()
            => Assert.DoesNotThrow(() => _bus.Raise(new TestEvent()));

        [Test]
        public void Unsubscribe_DuringDispatch_DoesNotDisturbCurrentPass()
        {
            var invoked = new List<int>();
            Action<TestEvent> second = null;

            _bus.Subscribe<TestEvent>(e =>
            {
                invoked.Add(1);
                // Removing a later listener mid-dispatch must not shift the array under the
                // running loop.
                _bus.Unsubscribe(second);
            }, Priority.Highest);

            second = e => invoked.Add(2);
            _bus.Subscribe(second, Priority.Low);
            _bus.Subscribe<TestEvent>(e => invoked.Add(3), Priority.Lowest);

            _bus.Raise(new TestEvent());

            // The tombstoned listener is skipped in this pass, the rest still run.
            CollectionAssert.AreEqual(new[] { 1, 3 }, invoked);
            Assert.AreEqual(2, _bus.SubscriberCount<TestEvent>(), "tombstone should be compacted away");
        }

        [Test]
        public void Unsubscribe_Self_DuringDispatch_IsSafe()
        {
            var count = 0;
            Action<TestEvent> handler = null;
            handler = e =>
            {
                count++;
                _bus.Unsubscribe(handler);
            };

            _bus.Subscribe(handler);
            _bus.Raise(new TestEvent());
            _bus.Raise(new TestEvent());

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Subscribe_DuringDispatch_DoesNotReceiveCurrentEvent()
        {
            var lateInvocations = 0;

            _bus.Subscribe<TestEvent>(e =>
                _bus.Subscribe<TestEvent>(_ => lateInvocations++, Priority.Lowest), Priority.Highest);

            _bus.Raise(new TestEvent());
            Assert.AreEqual(0, lateInvocations, "a listener added mid-dispatch must sit out that pass");

            // Second pass runs the listener added during the first one; the listener added
            // during *this* pass again sits it out.
            _bus.Raise(new TestEvent());
            Assert.AreEqual(1, lateInvocations);
        }

        [Test]
        public void Subscribe_DuringDispatch_IsSortedAfterwards()
        {
            var order = new List<int>();

            _bus.Subscribe<TestEvent>(e =>
            {
                if (_bus.SubscriberCount<TestEvent>() == 1)
                {
                    // Appended at the end during dispatch; must be re-sorted to the front.
                    _bus.Subscribe<TestEvent>(_ => order.Add(0), Priority.Highest);
                }

                order.Add(1);
            }, Priority.Low);

            _bus.Raise(new TestEvent());
            order.Clear();
            _bus.Raise(new TestEvent());

            CollectionAssert.AreEqual(new[] { 0, 1 }, order);
        }

        [Test]
        public void Raise_DoesNotAllocate()
        {
            // The old bus copied its listener list into a new List<IListener> on every raise,
            // and boxed each struct listener on subscribe. For a bus raised every frame that
            // is the difference between zero GC pressure and a steady allocation drip.
            //
            // Measured with Unity's own AllocatingGCMemory constraint rather than
            // GC.GetAllocatedBytesForCurrentThread, which returns 0 on Unity's Mono runtime
            // and would make this assertion vacuous.
            for (var i = 0; i < 8; i++)
            {
                _bus.Subscribe<TestEvent>(e => { }, (Priority)(i % 5 - 2));
            }

            _bus.Raise(new TestEvent()); // warm up any lazy paths

            Assert.That(() =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    _bus.Raise(new TestEvent { Value = i });
                }
            }, UnityIs.Not.AllocatingGCMemory());
        }

        [Test]
        public void Raise_AllocationProbe_DetectsRealAllocations()
        {
            // Proves the constraint used above actually observes allocations, so a green
            // Raise_DoesNotAllocate means something.
            Assert.That(() => _ = new byte[1024], UnityIs.AllocatingGCMemory());
        }

        [Test]
        public void Subscribe_NullAction_Throws()
            => Assert.Throws<ArgumentNullException>(() => _bus.Subscribe<TestEvent>(null));

        [Test]
        public void SubscriberCount_IsPerEventType()
        {
            _bus.Subscribe<TestEvent>(e => { });
            _bus.Subscribe<TestEvent>(e => { });
            _bus.Subscribe<OtherEvent>(e => { });

            Assert.AreEqual(2, _bus.SubscriberCount<TestEvent>());
            Assert.AreEqual(1, _bus.SubscriberCount<OtherEvent>());
        }

        [Test]
        public void Clear_RemovesOnlyThatEventType()
        {
            _bus.Subscribe<TestEvent>(e => { });
            _bus.Subscribe<OtherEvent>(e => { });

            _bus.Clear<TestEvent>();

            Assert.AreEqual(0, _bus.SubscriberCount<TestEvent>());
            Assert.AreEqual(1, _bus.SubscriberCount<OtherEvent>());
        }

        [Test]
        public void Reset_DropsEverySubscription()
        {
            _bus.Subscribe<TestEvent>(e => { });
            _bus.Subscribe<OtherEvent>(e => { });

            _bus.Reset();

            Assert.AreEqual(0, _bus.SubscriberCount<TestEvent>());
            Assert.AreEqual(0, _bus.SubscriberCount<OtherEvent>());
        }

        [Test]
        public void Subscribe_ManyListeners_GrowsBackingArray()
        {
            var count = 0;

            for (var i = 0; i < 100; i++)
            {
                _bus.Subscribe<TestEvent>(e => count++);
            }

            _bus.Raise(new TestEvent());

            Assert.AreEqual(100, count);
        }
    }
}
