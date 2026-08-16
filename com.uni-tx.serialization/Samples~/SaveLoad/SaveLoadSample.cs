using System;
using UniTx.IoC;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Serialization.Samples
{
    /// <summary>
    /// A player's persisted progress.
    /// </summary>
    /// <remarks>
    /// JsonUtility serializes <b>fields</b>, not properties, so anything that must survive a
    /// restart needs a <c>[SerializeField]</c> backing field. Auto-properties are silently
    /// dropped — a common cause of "my save keeps resetting".
    /// </remarks>
    [Serializable]
    public sealed class PlayerProgress : ISavedData
    {
        [SerializeField] private string _id;
        [SerializeField] private long _modifiedTimestamp;
        [SerializeField] private int _version = CurrentVersion;

        [SerializeField] private int _coins;
        [SerializeField] private int _highestLevel = 1;
        [SerializeField] private string[] _unlockedSkins = Array.Empty<string>();

        /// <summary>
        /// Bump when the shape of this type changes, then handle it in Migrate.
        /// </summary>
        public const int CurrentVersion = 2;

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
        /// Gets the schema version this instance was written with.
        /// </summary>
        public int Version => _version;

        /// <summary>
        /// Gets the player's coin balance.
        /// </summary>
        public int Coins => _coins;

        /// <summary>
        /// Gets the highest level reached.
        /// </summary>
        public int HighestLevel => _highestLevel;

        /// <summary>
        /// Gets the unlocked skin ids.
        /// </summary>
        public string[] UnlockedSkins => _unlockedSkins;

        /// <summary>
        /// Adds coins.
        /// </summary>
        public void AddCoins(int amount) => _coins = Mathf.Max(0, _coins + amount);

        /// <summary>
        /// Records a completed level.
        /// </summary>
        public void CompleteLevel(int level) => _highestLevel = Mathf.Max(_highestLevel, level);

        /// <summary>
        /// Brings an older save up to the current schema.
        /// </summary>
        /// <remarks>
        /// Migration has to be explicit. A shipped game will read saves written by every
        /// version you ever released, and a field that changed meaning between them is a
        /// silent corruption unless it is converted here.
        /// </remarks>
        public void Migrate()
        {
            if (_version >= CurrentVersion) return;

            if (_version < 2)
            {
                // v1 had no skins array; JsonUtility leaves it null rather than empty.
                _unlockedSkins ??= Array.Empty<string>();
                Debug.Log("[Save] migrated v1 -> v2");
            }

            _version = CurrentVersion;
        }
    }

    /// <summary>
    /// Loading, mutating, batching and force-flushing saved data.
    /// </summary>
    public sealed class SaveLoadSample : MonoBehaviour
    {
        private const string SaveId = "player-progress";

        private ISerialisationService _saves;
        private PlayerProgress _progress;

        private void Start()
        {
            // Bootstrap normally binds this; done inline so the sample runs standalone.
            if (!IoCStatics.Resolver.TryResolve(out _saves))
            {
                var service = new SerialisationService();
                IoCStatics.Binder.BindInstance(service).AsSingleton().Conclude();
                service.Inject(IoCStatics.Resolver);
                service.Initialize();
                _saves = service;
            }

            // Load returns the persisted instance, or a fresh one on first run. Either way
            // its Id is set, so the first Save always has somewhere to write.
            _progress = _saves.Load<PlayerProgress>(SaveId);
            _progress.Migrate();

            Debug.Log($"Loaded: coins={_progress.Coins} level={_progress.HighestLevel} " +
                      $"v{_progress.Version} modified={_progress.ModifiedTimestamp}");
        }

        /// <summary>
        /// Marks progress dirty. The write happens on the next autosave batch.
        /// </summary>
        [ContextMenu("Collect Coins")]
        public void CollectCoins()
        {
            _progress.AddCoins(25);

            // Save() queues rather than writing. A value changed every frame therefore costs
            // one disk write per interval instead of one per frame.
            _saves.Save(_progress);

            Debug.Log($"Coins: {_progress.Coins} (queued)");
        }

        /// <summary>
        /// Writes immediately, for a moment that must not be lost.
        /// </summary>
        [ContextMenu("Complete Level")]
        public void CompleteLevel()
        {
            _progress.CompleteLevel(_progress.HighestLevel + 1);
            _saves.Save(_progress);

            // Flush before anything the player would file a bug about losing: a purchase, a
            // level completion, a granted ad reward. The service also flushes automatically
            // on pause and quit.
            var written = _saves.Flush();

            Debug.Log($"Level {_progress.HighestLevel} completed, {written} file(s) written.");
        }

        /// <summary>
        /// Deletes the save file and starts over.
        /// </summary>
        [ContextMenu("Reset Progress")]
        public void ResetProgress()
        {
            _saves.Delete(SaveId);
            _progress = _saves.Load<PlayerProgress>(SaveId);

            Debug.Log($"Reset. Coins: {_progress.Coins}");
        }
    }
}
