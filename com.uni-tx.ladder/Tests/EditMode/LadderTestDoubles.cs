using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Ladder.Tests
{
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
    internal sealed class RecordingGranter : ILadderRewardGranter
    {
        public List<LadderRungRef> Granted { get; } = new();

        public List<string> GrantIds { get; } = new();

        public bool ShouldFail { get; set; }

        public bool ShouldThrow { get; set; }

        public UniTask<bool> GrantAsync(LadderRungData rung, LadderRewardData reward,
            LadderRungRef reference, string grantId, CancellationToken cToken = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("granter blew up");

            if (ShouldFail) return UniTask.FromResult(false);

            Granted.Add(reference);
            GrantIds.Add(grantId);

            return UniTask.FromResult(true);
        }

        public int CountFor(string rungId) => Granted.Count(reference => reference.RungId == rungId);
    }

    /// <summary>
    /// A backend over the in-memory serialisation service.
    /// </summary>
    internal sealed class FakeBackend : ILadderBackend
    {
        private readonly FakeSerialisationService _serialisation;

        public FakeBackend(FakeSerialisationService serialisation) => _serialisation = serialisation;

        public bool IsAuthoritative { get; set; } = true;

        public bool IsOnline { get; set; } = true;

        public UniTask<LadderSavedData> LoadAsync(string saveId,
            CancellationToken cToken = default)
        {
            var data = _serialisation.Load<LadderSavedData>(saveId);
            data.Migrate();

            return UniTask.FromResult(data);
        }

        public UniTask SaveAsync(LadderSavedData data, bool immediate,
            CancellationToken cToken = default)
        {
            // Mirrors LocalLadderBackend: a fake that ignores the token would let
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
        public static UniLadderConfig Create(bool flushOnCheckpoint = true,
            string forcedLadderId = null)
        {
            var config = ScriptableObject.CreateInstance<UniLadderConfig>();

            var json =
                $@"{{ ""_saveId"": ""{LadderSavedData.DefaultSaveId}"", " +
                $@"""_flushOnCheckpoint"": {(flushOnCheckpoint ? "true" : "false")}, " +
                $@"""_forcedLadderId"": ""{forcedLadderId ?? string.Empty}"", " +
                $@"""_verboseLogging"": false }}";

            JsonUtility.FromJsonOverwrite(json, config);

            return config;
        }
    }

    /// <summary>
    /// Builds ladders the way content does — through JSON.
    /// </summary>
    /// <remarks>
    /// Deliberately not a set of object initializers. The runtime only ever sees a ladder
    /// that came out of <c>JsonUtility</c>, so building one any other way in a test would
    /// skip the field-mapping rules that make content load at all.
    /// </remarks>
    internal static class LadderJson
    {
        public const string LadderId = "ladder_test";

        /// <summary>
        /// A three-rung ladder: 1, 3 and 5 cumulative steps, the last being the grand prize.
        /// </summary>
        public static LadderData ThreeRungs(string ladderId = LadderId) =>
            Parse($@"{{
  ""_id"": ""{ladderId}"",
  ""_displayName"": ""Test Ladder"",
  ""_rungs"": [
    {{
      ""_id"": ""r1"",
      ""_displayName"": ""Rung One"",
      ""_steps"": 1,
      ""_rewards"": [{{ ""_rewardId"": ""r1r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 10 }}]
    }},
    {{
      ""_id"": ""r2"",
      ""_displayName"": ""Rung Two"",
      ""_steps"": 3,
      ""_rewards"": [{{ ""_rewardId"": ""r2r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 25 }}]
    }},
    {{
      ""_id"": ""r3"",
      ""_displayName"": ""Grand Prize"",
      ""_steps"": 5,
      ""_rewards"": [{{ ""_rewardId"": ""r3r1"", ""_kind"": 0, ""_itemId"": ""gems"", ""_amount"": 50 }}]
    }}
  ]
}}");

        /// <summary>
        /// A ladder with two rungs that share a step threshold — a misconfigured board.
        /// </summary>
        public static LadderData DuplicateThresholds(string ladderId = LadderId) =>
            Parse($@"{{
  ""_id"": ""{ladderId}"",
  ""_displayName"": ""Test Ladder"",
  ""_rungs"": [
    {{
      ""_id"": ""r1"",
      ""_displayName"": ""Rung One"",
      ""_steps"": 3,
      ""_rewards"": [{{ ""_rewardId"": ""r1r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 10 }}]
    }},
    {{
      ""_id"": ""r2"",
      ""_displayName"": ""Rung Two"",
      ""_steps"": 3,
      ""_rewards"": [{{ ""_rewardId"": ""r2r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 25 }}]
    }}
  ]
}}");

        /// <summary>
        /// A ladder with no rungs — a misconfigured board.
        /// </summary>
        public static LadderData Empty(string ladderId = LadderId) =>
            Parse($@"{{ ""_id"": ""{ladderId}"", ""_displayName"": ""Empty"", ""_rungs"": [] }}");

        /// <summary>
        /// Parses a ladder definition exactly as the content loader would.
        /// </summary>
        public static LadderData Parse(string json) => JsonUtility.FromJson<LadderData>(json);
    }
}
