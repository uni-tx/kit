using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Quests.Tests
{
    /// <summary>
    /// A clock the test drives by hand.
    /// </summary>
    internal sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; set; }

        public long UnixTimestampNow => UtcNow.ToUnixTimestamp();

        public void Advance(TimeSpan amount) => UtcNow += amount;
    }

    /// <summary>
    /// An in-memory serialisation service, so tests never touch the disk.
    /// </summary>
    internal sealed class FakeSerialisationService : ISerialisationService
    {
        private readonly Dictionary<string, ISavedData> _store = new();

        public int SaveCount { get; private set; }

        public int FlushCount { get; private set; }

        public void Save(ISavedData data)
        {
            SaveCount++;

            if (data?.Id != null) _store[data.Id] = data;
        }

        public T Load<T>(string id)
            where T : ISavedData, new()
        {
            if (_store.TryGetValue(id, out var existing) && existing is T typed) return typed;

            var created = new T { Id = id };
            _store[id] = created;

            return created;
        }

        public int Flush()
        {
            FlushCount++;
            return _store.Count;
        }

        public void Delete(string id) => _store.Remove(id);
    }

    /// <summary>
    /// A content service backed by a dictionary the test fills.
    /// </summary>
    internal sealed class FakeContentService : IContentService
    {
        private readonly Dictionary<string, IData> _data = new();

        public void Add(IData data) => _data[data.Id] = data;

        public void Remove(string key) => _data.Remove(key);

        public T GetData<T>(string key)
            where T : IData =>
            _data.TryGetValue(key, out var data) && data is T typed
                ? typed
                : throw new KeyNotFoundException(key);

        public bool TryGetData<T>(string key, out T data)
            where T : IData
        {
            if (key != null && _data.TryGetValue(key, out var found) && found is T typed)
            {
                data = typed;
                return true;
            }

            data = default;
            return false;
        }

        public IEnumerable<T> GetData<T>(IEnumerable<string> keys)
            where T : IData =>
            keys.Select(key => TryGetData<T>(key, out var data) ? data : default)
                .Where(data => data != null);

        public IEnumerable<T> GetAllData<T>()
            where T : IData => _data.Values.OfType<T>();
    }

    /// <summary>
    /// A granter that records what it was asked to deliver, and can be told to refuse.
    /// </summary>
    internal sealed class RecordingGranter : IQuestRewardGranter
    {
        public List<QuestRef> Granted { get; } = new();

        public List<string> GrantIds { get; } = new();

        public bool ShouldFail { get; set; }

        public bool ShouldThrow { get; set; }

        public UniTask<bool> GrantAsync(QuestData quest, QuestRewardData reward,
            QuestRef reference, string grantId, CancellationToken cToken = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("granter blew up");

            if (ShouldFail) return UniTask.FromResult(false);

            Granted.Add(reference);
            GrantIds.Add(grantId);

            return UniTask.FromResult(true);
        }

        public int CountFor(string questId) => Granted.Count(reference => reference.QuestId == questId);
    }

    /// <summary>
    /// A backend over the in-memory serialisation service.
    /// </summary>
    internal sealed class FakeBackend : IQuestsBackend
    {
        private readonly FakeSerialisationService _serialisation;

        public FakeBackend(FakeSerialisationService serialisation) => _serialisation = serialisation;

        public bool IsAuthoritative { get; set; } = true;

        public bool IsOnline { get; set; } = true;

        public UniTask<QuestsSavedData> LoadAsync(string saveId,
            CancellationToken cToken = default)
        {
            var data = _serialisation.Load<QuestsSavedData>(saveId);
            data.Migrate();

            return UniTask.FromResult(data);
        }

        public UniTask SaveAsync(QuestsSavedData data, bool immediate,
            CancellationToken cToken = default)
        {
            // Mirrors LocalQuestsBackend: a fake that ignores the token would let
            // cancellation bugs pass here and surface only against the real one.
            cToken.ThrowIfCancellationRequested();

            _serialisation.Save(data);

            if (immediate) _serialisation.Flush();

            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Builds a config the way a designer's asset would deserialize — through JSON — because
    /// the private serialized fields have no public setters.
    /// </summary>
    internal static class ConfigFactory
    {
        public static UniQuestsConfig Create(int resetHourUtc = 0, int weekStartDay = 1,
            bool flushOnCheckpoint = true, string forcedSetId = null)
        {
            var config = ScriptableObject.CreateInstance<UniQuestsConfig>();

            var json =
                $@"{{ ""_saveId"": ""{QuestsSavedData.DefaultSaveId}"", " +
                $@"""_flushOnCheckpoint"": {(flushOnCheckpoint ? "true" : "false")}, " +
                $@"""_resetHourUtc"": {resetHourUtc}, " +
                $@"""_weekStartDay"": {weekStartDay}, " +
                $@"""_forcedSetId"": ""{forcedSetId ?? string.Empty}"", " +
                $@"""_verboseLogging"": false }}";

            JsonUtility.FromJsonOverwrite(json, config);

            return config;
        }
    }

    /// <summary>
    /// Builds quest sets the way content does — through JSON.
    /// </summary>
    /// <remarks>
    /// Deliberately not a set of object initializers. The runtime only ever sees a set that
    /// came out of <c>JsonUtility</c>, so building one any other way in a test would skip
    /// the field-mapping rules that make content load at all.
    /// </remarks>
    internal static class QuestSetJson
    {
        public const string SetId = "quests_test";

        /// <summary>
        /// A set with a single daily quest whose objective key is <paramref name="objectiveKey"/>.
        /// </summary>
        public static QuestSetData Single(string objectiveKey = "win_match", int target = 3,
            int reset = 1, string questId = "q1", string requiredQuestId = null,
            string setId = SetId) =>
            Parse($@"{{
  ""_id"": ""{setId}"",
  ""_displayName"": ""Test Board"",
  ""_quests"": [{{
    ""_id"": ""{questId}"",
    ""_displayName"": ""Win Matches"",
    ""_description"": ""Win matches today"",
    ""_reset"": {reset},
    ""_order"": 0,
    ""_requiredQuestId"": ""{requiredQuestId ?? string.Empty}"",
    ""_objectives"": [{{ ""_key"": ""{objectiveKey}"", ""_displayName"": ""Win"" , ""_target"": {target} }}],
    ""_rewards"": [{{ ""_rewardId"": ""r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 100 }}]
  }}]
}}");

        /// <summary>
        /// A set with a two-quest prerequisite chain: <paramref name="questId"/> requires
        /// <paramref name="requiredQuestId"/>.
        /// </summary>
        public static QuestSetData Chain(string questId = "q2", string requiredQuestId = "q1",
            string setId = SetId) =>
            Parse($@"{{
  ""_id"": ""{setId}"",
  ""_displayName"": ""Test Board"",
  ""_quests"": [
    {{
      ""_id"": ""{requiredQuestId}"",
      ""_displayName"": ""Tutorial"",
      ""_description"": ""Finish the tutorial"",
      ""_reset"": 0,
      ""_order"": 0,
      ""_objectives"": [{{ ""_key"": ""tutorial"", ""_target"": 1 }}],
      ""_rewards"": [{{ ""_rewardId"": ""r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 10 }}]
    }},
    {{
      ""_id"": ""{questId}"",
      ""_displayName"": ""First Win"",
      ""_description"": ""Win after the tutorial"",
      ""_reset"": 1,
      ""_order"": 1,
      ""_requiredQuestId"": ""{requiredQuestId}"",
      ""_objectives"": [{{ ""_key"": ""win_match"", ""_target"": 1 }}],
      ""_rewards"": [{{ ""_rewardId"": ""r2"", ""_kind"": 0, ""_itemId"": ""gems"", ""_amount"": 5 }}]
    }}
  ]
}}");

        /// <summary>
        /// A set holding a daily and a weekly quest, for cadence tests.
        /// </summary>
        public static QuestSetData Mixed(string setId = SetId) =>
            Parse($@"{{
  ""_id"": ""{setId}"",
  ""_displayName"": ""Test Board"",
  ""_quests"": [
    {{
      ""_id"": ""daily"",
      ""_displayName"": ""Daily"",
      ""_description"": ""Daily quest"",
      ""_reset"": 1,
      ""_order"": 0,
      ""_objectives"": [{{ ""_key"": ""play"", ""_target"": 1 }}],
      ""_rewards"": [{{ ""_rewardId"": ""r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 10 }}]
    }},
    {{
      ""_id"": ""weekly"",
      ""_displayName"": ""Weekly"",
      ""_description"": ""Weekly quest"",
      ""_reset"": 2,
      ""_order"": 1,
      ""_objectives"": [{{ ""_key"": ""play"", ""_target"": 3 }}],
      ""_rewards"": [{{ ""_rewardId"": ""r2"", ""_kind"": 0, ""_itemId"": ""gems"", ""_amount"": 20 }}]
    }}
  ]
}}");

        /// <summary>
        /// Parses a set definition exactly as the content loader would.
        /// </summary>
        public static QuestSetData Parse(string json) => JsonUtility.FromJson<QuestSetData>(json);
    }
}
