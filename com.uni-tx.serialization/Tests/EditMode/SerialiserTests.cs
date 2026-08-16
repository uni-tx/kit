using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UniTx.Serialization.Tests.EditMode
{
    /// <summary>
    /// Save data backed by serialized fields — JsonUtility writes fields, not properties.
    /// </summary>
    [Serializable]
    public sealed class TestSavedData : ISavedData
    {
        [SerializeField] private string _id;
        [SerializeField] private int _coins;
        [SerializeField] private long _modifiedTimestamp;

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

        public int Coins => _coins;

        public void AddCoins(int amount) => _coins += amount;
    }

    /// <summary>
    /// Save data whose id is a plain auto-property, i.e. no `_id` serialized field at all.
    /// </summary>
    public sealed class PropertyIdSavedData : ISavedData
    {
        public string Id { get; set; }
        public long ModifiedTimestamp { get; set; }
    }

    public class SerialiserTests
    {
        private Serialiser _serialiser;

        [SetUp]
        public void SetUp() => _serialiser = new Serialiser();

        [TearDown]
        public void TearDown()
        {
            foreach (var id in new[] { "save-1", "atomic", "corrupt", "deleted", "prop-id" })
            {
                _serialiser.Delete(id);
            }

            _serialiser.Reset();
        }

        [Test]
        public void Deserialise_WhenNoFile_ReturnsFreshInstanceWithId()
        {
            var data = _serialiser.Deserialise<TestSavedData>("missing-id");

            Assert.IsNotNull(data);
            Assert.AreEqual("missing-id", data.Id);
        }

        [Test]
        public void Deserialise_AssignsIdWithoutRelyingOnASerializedField()
        {
            // The old implementation synthesized {"_id":"…"} JSON, so a type without a field
            // called exactly `_id` came back with a null Id and could never be saved.
            var data = _serialiser.Deserialise<PropertyIdSavedData>("prop-id");

            Assert.AreEqual("prop-id", data.Id);
        }

        [Test]
        public void MarkDirty_ThenSerialiseDirty_WritesFile()
        {
            var data = _serialiser.Deserialise<TestSavedData>("save-1");
            data.AddCoins(10);

            _serialiser.MarkDirty(data);
            Assert.AreEqual(1, _serialiser.SerialiseDirty());

            _serialiser.Reset();

            var loaded = _serialiser.Deserialise<TestSavedData>("save-1");
            Assert.AreEqual(10, loaded.Coins);
            Assert.Greater(loaded.ModifiedTimestamp, 0L);
        }

        [Test]
        public void SerialiseDirty_LeavesNoTempFileBehind()
        {
            var data = _serialiser.Deserialise<TestSavedData>("atomic");
            data.AddCoins(1);
            _serialiser.MarkDirty(data);
            _serialiser.SerialiseDirty();

            // Writes go to a .tmp then swap atomically; a stray .tmp means the swap did not
            // happen and a crash could leave a truncated save.
            var temp = Path.Combine(Serialiser.SaveDirectoryPath, "atomic.json.tmp");
            FileAssert.DoesNotExist(temp);
            FileAssert.Exists(Path.Combine(Serialiser.SaveDirectoryPath, "atomic.json"));
        }

        [Test]
        public void SerialiseDirty_WithNothingQueued_WritesNothing()
            => Assert.AreEqual(0, _serialiser.SerialiseDirty());

        [Test]
        public void Deserialise_CorruptFile_FallsBackToDefaults()
        {
            Directory.CreateDirectory(Serialiser.SaveDirectoryPath);
            File.WriteAllText(Path.Combine(Serialiser.SaveDirectoryPath, "corrupt.json"), "{ this is not json");

            // A truncated or hand-edited save must not take the game down on launch.
            TestSavedData data = null;
            Assert.DoesNotThrow(() => data = _serialiser.Deserialise<TestSavedData>("corrupt"));
            Assert.AreEqual("corrupt", data.Id);
            Assert.AreEqual(0, data.Coins);
        }

        [Test]
        public void Deserialise_CachesInstanceAcrossCalls()
        {
            var first = _serialiser.Deserialise<TestSavedData>("cached");
            var second = _serialiser.Deserialise<TestSavedData>("cached");

            Assert.AreSame(first, second);
        }

        [Test]
        public void Reset_ClearsCache()
        {
            var first = _serialiser.Deserialise<TestSavedData>("gone");
            _serialiser.Reset();

            var second = _serialiser.Deserialise<TestSavedData>("gone");

            Assert.AreNotSame(first, second);
        }

        [Test]
        public void Delete_RemovesFileAndCache()
        {
            var data = _serialiser.Deserialise<TestSavedData>("deleted");
            data.AddCoins(5);
            _serialiser.MarkDirty(data);
            _serialiser.SerialiseDirty();

            _serialiser.Delete("deleted");

            FileAssert.DoesNotExist(Path.Combine(Serialiser.SaveDirectoryPath, "deleted.json"));
            Assert.AreEqual(0, _serialiser.Deserialise<TestSavedData>("deleted").Coins);
        }

        [Test]
        public void MarkDirty_NullData_DoesNotThrow()
            => Assert.DoesNotThrow(() => _serialiser.MarkDirty(null));

        [Test]
        public void DirtyCount_TracksQueuedEntries()
        {
            Assert.AreEqual(0, _serialiser.DirtyCount);

            _serialiser.MarkDirty(_serialiser.Deserialise<TestSavedData>("save-1"));

            Assert.AreEqual(1, _serialiser.DirtyCount);

            _serialiser.SerialiseDirty();

            Assert.AreEqual(0, _serialiser.DirtyCount);
        }
    }
}
