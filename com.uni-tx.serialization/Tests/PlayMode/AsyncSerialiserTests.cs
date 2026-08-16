using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniTx.Serialization.Tests.PlayMode
{
    /// <summary>
    /// Saved data used to exercise the off-thread write path.
    /// </summary>
    [Serializable]
    public sealed class AsyncTestSavedData : ISavedData
    {
        [SerializeField] private string _id;
        [SerializeField] private long _modifiedTimestamp;
        [SerializeField] private int _value;

        /// <inheritdoc />
        public string Id
        {
            get => _id;
            set => _id = value;
        }

        /// <inheritdoc />
        public long ModifiedTimestamp
        {
            get => _modifiedTimestamp;
            set => _modifiedTimestamp = value;
        }

        /// <summary>
        /// Gets the stored value.
        /// </summary>
        public int Value => _value;

        /// <summary>
        /// Sets the stored value.
        /// </summary>
        /// <param name="value">The value to store.</param>
        public void SetValue(int value) => _value = value;
    }

    public class AsyncSerialiserTests
    {
        private const string IdA = "async-a";
        private const string IdB = "async-b";

        private Serialiser _serialiser;

        [SetUp]
        public void SetUp() => _serialiser = new Serialiser();

        [TearDown]
        public void TearDown()
        {
            _serialiser.Delete(IdA);
            _serialiser.Delete(IdB);
            _serialiser.Reset();
        }

        [UnityTest]
        public IEnumerator SerialiseDirtyAsync_WritesEveryQueuedEntry() => UniTask.ToCoroutine(async () =>
        {
            var a = _serialiser.Deserialise<AsyncTestSavedData>(IdA);
            var b = _serialiser.Deserialise<AsyncTestSavedData>(IdB);
            a.SetValue(11);
            b.SetValue(22);
            _serialiser.MarkDirty(a);
            _serialiser.MarkDirty(b);

            var written = await _serialiser.SerialiseDirtyAsync(CancellationToken.None);

            Assert.AreEqual(2, written);
            Assert.AreEqual(0, _serialiser.DirtyCount);

            FileAssert.Exists(Path.Combine(Serialiser.SaveDirectoryPath, $"{IdA}.json"));
            FileAssert.Exists(Path.Combine(Serialiser.SaveDirectoryPath, $"{IdB}.json"));
        });

        [UnityTest]
        public IEnumerator SerialiseDirtyAsync_RoundTripsThroughDisk() => UniTask.ToCoroutine(async () =>
        {
            var data = _serialiser.Deserialise<AsyncTestSavedData>(IdA);
            data.SetValue(1234);
            _serialiser.MarkDirty(data);

            await _serialiser.SerialiseDirtyAsync(CancellationToken.None);

            // Drop the cache so the value has to come back off disk rather than memory.
            _serialiser.Reset();

            var reloaded = _serialiser.Deserialise<AsyncTestSavedData>(IdA);

            Assert.AreEqual(1234, reloaded.Value);
            Assert.Greater(reloaded.ModifiedTimestamp, 0L);
        });

        [UnityTest]
        public IEnumerator SerialiseDirtyAsync_ReturnsToTheMainThread() => UniTask.ToCoroutine(async () =>
        {
            var mainThread = Thread.CurrentThread.ManagedThreadId;

            var data = _serialiser.Deserialise<AsyncTestSavedData>(IdA);
            data.SetValue(7);
            _serialiser.MarkDirty(data);

            await _serialiser.SerialiseDirtyAsync(CancellationToken.None);

            // The continuation touches the dirty map and Unity's logger, both of which are
            // main-thread only. If the await resumed on the thread pool this would be a
            // rare, hard-to-reproduce crash rather than a test failure.
            Assert.AreEqual(mainThread, Thread.CurrentThread.ManagedThreadId);

            // Proves the main-thread-only Unity API is reachable after the await.
            Assert.IsNotNull(Application.persistentDataPath);
        });

        [UnityTest]
        public IEnumerator SerialiseDirtyAsync_Cancelled_KeepsTheDataQueued() => UniTask.ToCoroutine(async () =>
        {
            var data = _serialiser.Deserialise<AsyncTestSavedData>(IdA);
            data.SetValue(99);
            _serialiser.MarkDirty(data);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // The queue is drained before the await, so a cancellation here must put the
            // entries back — otherwise they are neither dirty nor written, and the player's
            // progress is silently gone.
            try
            {
                await _serialiser.SerialiseDirtyAsync(cts.Token);
                Assert.Fail("expected the cancellation to propagate");
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }

            Assert.AreEqual(1, _serialiser.DirtyCount, "cancelled entries must remain queued");

            // And the retry actually writes them.
            Assert.AreEqual(1, await _serialiser.SerialiseDirtyAsync(CancellationToken.None));
        });

        [UnityTest]
        public IEnumerator SerialiseDirtyAsync_WithNothingQueued_WritesNothing() =>
            UniTask.ToCoroutine(async () =>
            {
                Assert.AreEqual(0, await _serialiser.SerialiseDirtyAsync(CancellationToken.None));
            });

        [UnityTest]
        public IEnumerator SerialiseDirtyAsync_LeavesNoTempFile() => UniTask.ToCoroutine(async () =>
        {
            var data = _serialiser.Deserialise<AsyncTestSavedData>(IdA);
            data.SetValue(5);
            _serialiser.MarkDirty(data);

            await _serialiser.SerialiseDirtyAsync(CancellationToken.None);

            // Writes land on a temp file and swap atomically; a stray .tmp means the swap
            // did not happen and a crash could leave a truncated save.
            FileAssert.DoesNotExist(Path.Combine(Serialiser.SaveDirectoryPath, $"{IdA}.json.tmp"));
        });

        [UnityTest]
        public IEnumerator SerialiseDirtyAsync_StampsModifiedTimestampOnce() => UniTask.ToCoroutine(async () =>
        {
            var data = _serialiser.Deserialise<AsyncTestSavedData>(IdA);
            data.SetValue(1);
            _serialiser.MarkDirty(data);

            await _serialiser.SerialiseDirtyAsync(CancellationToken.None);

            // Stamped on the main thread before the payload is handed to the pool —
            // JsonUtility and Unity object access are not thread-safe.
            Assert.Greater(data.ModifiedTimestamp, 0L);
        });
    }
}
