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

namespace UniTx.DailyRewards.Tests
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
    internal sealed class RecordingGranter : IDailyRewardsRewardGranter
    {
        public List<DailyRewardRef> Granted { get; } = new();

        public List<string> GrantIds { get; } = new();

        public bool ShouldFail { get; set; }

        public bool ShouldThrow { get; set; }

        public UniTask<bool> GrantAsync(DailyRewardSlotData slot, DailyRewardRef reference,
            string grantId, CancellationToken cToken = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("granter blew up");

            if (ShouldFail) return UniTask.FromResult(false);

            Granted.Add(reference);
            GrantIds.Add(grantId);

            return UniTask.FromResult(true);
        }

        public int CountFor(int slotIndex) => Granted.Count(reference => reference.SlotIndex == slotIndex);
    }

    /// <summary>
    /// A backend over the in-memory serialisation service.
    /// </summary>
    internal sealed class FakeBackend : IDailyRewardsBackend
    {
        private readonly FakeSerialisationService _serialisation;

        public FakeBackend(FakeSerialisationService serialisation) => _serialisation = serialisation;

        public bool IsAuthoritative { get; set; } = true;

        public bool IsOnline { get; set; } = true;

        public UniTask<DailyRewardsSavedData> LoadAsync(string saveId,
            CancellationToken cToken = default)
        {
            var data = _serialisation.Load<DailyRewardsSavedData>(saveId);
            data.Migrate();

            return UniTask.FromResult(data);
        }

        public UniTask SaveAsync(DailyRewardsSavedData data, bool immediate,
            CancellationToken cToken = default)
        {
            // Mirrors LocalDailyRewardsBackend: a fake that ignores the token would let
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
        public static UniDailyRewardsConfig Create(int resetHourUtc = 0, bool flushOnCheckpoint = true,
            string forcedCalendarId = null)
        {
            var config = ScriptableObject.CreateInstance<UniDailyRewardsConfig>();

            var json =
                $@"{{ ""_saveId"": ""{DailyRewardsSavedData.DefaultSaveId}"", " +
                $@"""_flushOnCheckpoint"": {(flushOnCheckpoint ? "true" : "false")}, " +
                $@"""_resetHourUtc"": {resetHourUtc}, " +
                $@"""_forcedCalendarId"": ""{forcedCalendarId ?? string.Empty}"", " +
                $@"""_verboseLogging"": false }}";

            JsonUtility.FromJsonOverwrite(json, config);

            return config;
        }
    }

    /// <summary>
    /// Builds calendars the way content does — through JSON.
    /// </summary>
    /// <remarks>
    /// Deliberately not a set of object initializers. The runtime only ever sees a calendar
    /// that came out of <c>JsonUtility</c>, so building one any other way in a test would
    /// skip the field-mapping rules that make content load at all.
    /// </remarks>
    internal static class CalendarJson
    {
        public const string CalendarId = "daily_test";

        /// <summary>
        /// A calendar of <paramref name="days"/> currency slots, optionally looped, in either mode.
        /// </summary>
        public static DailyRewardsData Standard(int days = 7, int mode = 0, bool loop = true,
            string id = CalendarId) =>
            Parse($@"{{
  ""_id"": ""{id}"",
  ""_displayName"": ""Test Calendar"",
  ""_mode"": {mode},
  ""_loop"": {(loop ? "true" : "false")},
  ""_slots"": [{Slots(days)}]
}}");

        /// <summary>
        /// Parses a calendar definition exactly as the content loader would.
        /// </summary>
        public static DailyRewardsData Parse(string json) => JsonUtility.FromJson<DailyRewardsData>(json);

        private static string Slots(int days)
        {
            var builder = new StringBuilder();

            for (var day = 1; day <= days; day++)
            {
                if (day > 1) builder.Append(',');

                builder.Append($@"{{ ""_day"": {day}, ""_rewardId"": ""d{day}"", " +
                               $@"""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": {day * 10}, " +
                               $@"""_isMilestone"": {(day == days ? "true" : "false")} }}");
            }

            return builder.ToString();
        }
    }
}
