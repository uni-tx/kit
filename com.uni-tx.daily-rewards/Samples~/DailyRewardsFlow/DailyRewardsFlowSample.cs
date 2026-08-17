using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Events;
using UniTx.Resources;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.DailyRewards.Samples
{
    /// <summary>
    /// Grants rewards into a pretend inventory and logs what arrived.
    /// </summary>
    /// <remarks>
    /// This is the piece every game writes itself. Note that it returns a bool rather than
    /// throwing on refusal: returning false leaves the day's reward claimable and queued for
    /// retry, which is what keeps a full inventory or a dropped connection from eating a reward.
    /// </remarks>
    public sealed class SampleRewardGranter : IDailyRewardsRewardGranter
    {
        private readonly Dictionary<string, int> _inventory = new();

        /// <summary>
        /// Gets how many of an item the player holds.
        /// </summary>
        /// <param name="itemId">The item to read.</param>
        public int CountOf(string itemId) => _inventory.GetValueOrDefault(itemId, 0);

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(DailyRewardSlotData slot, DailyRewardRef reference,
            string grantId, CancellationToken cToken = default)
        {
            _inventory[slot.ItemId] = CountOf(slot.ItemId) + slot.Amount;

            Debug.Log($"[DailyRewards] +{slot.Amount} {slot.ItemId} on day {slot.Day} " +
                      $"({(slot.IsMilestone ? "milestone" : "regular")}). Held: {CountOf(slot.ItemId)}.");

            return UniTask.FromResult(true);
        }
    }

    /// <summary>
    /// A clock the sample drives by hand, so the day rollover is visible in one run.
    /// </summary>
    public sealed class SampleClock : IClock
    {
        /// <summary>
        /// Creates the clock at a fixed moment.
        /// </summary>
        /// <param name="utcNow">The starting UTC time.</param>
        public SampleClock(DateTime utcNow) => UtcNow = utcNow;

        /// <inheritdoc />
        public DateTime UtcNow { get; set; }

        /// <inheritdoc />
        public long UnixTimestampNow => UtcNow.ToUnixTimestamp();

        /// <summary>
        /// Moves the clock forward.
        /// </summary>
        /// <param name="amount">How far to advance.</param>
        public void Advance(TimeSpan amount) => UtcNow += amount;
    }

    /// <summary>
    /// The whole daily rewards lifecycle in one script, with no UI.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> put <c>daily_rewards_default.json</c> (in this folder) somewhere
    /// Addressable with the label below, and make sure its asset name matches the registered
    /// file name. Then press play and read the console.
    /// </remarks>
    public sealed class DailyRewardsFlowSample : MonoBehaviour
    {
        private const string CalendarFile = "daily_rewards_default";

        [Tooltip("Addressables label the calendar definitions are tagged with.")]
        [SerializeField] private string _contentLabel = "content";

        [Tooltip("Policy asset. Leave empty to load Resources/UniDailyRewardsConfig.")]
        [SerializeField] private UniDailyRewardsConfig _config;

        private readonly SampleRewardGranter _granter = new();

        private DailyRewardsService _service;
        private SampleClock _clock;
        private CancellationTokenSource _cts;

        private void Start() => RunAsync().Forget();

        private void OnDestroy()
        {
            UniEvents.Unsubscribe<DailyRewardClaimed>(OnClaimed);
            UniEvents.Unsubscribe<DailyStreakReset>(OnStreakReset);

            UniDailyRewards.Reset();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async UniTaskVoid RunAsync()
        {
            try
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

                if (!UniEvents.IsInitialized) UniEvents.Initialize();

                // Listening on the bus rather than polling: the same events drive a rewards
                // screen, a streak widget and an analytics adapter without any of them
                // knowing each other.
                UniEvents.Subscribe<DailyRewardClaimed>(OnClaimed);
                UniEvents.Subscribe<DailyStreakReset>(OnStreakReset);

                var content = await LoadContentAsync(_contentLabel, _cts.Token);

                // A fixed start time so the day rollover can be demonstrated deterministically.
                _clock = new SampleClock(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));

                _service = new DailyRewardsService(_clock, content,
                    new LocalDailyRewardsBackend(new SerialisationService()), _config);

                _service.SetRewardGranter(_granter);

                await UniDailyRewards.InitializeAsync(_service, _cts.Token);

                var calendar = UniDailyRewards.Calendar;

                if (calendar == null)
                {
                    Debug.LogWarning("[DailyRewards] No calendar is registered — check the JSON and its label.");
                    return;
                }

                LogState("ready");

                // 1. Day one.
                await UniDailyRewards.ClaimAsync(_cts.Token);
                _clock.Advance(TimeSpan.FromDays(1));

                // 2. Day two, on time: the streak grows.
                await UniDailyRewards.ClaimAsync(_cts.Token);
                _clock.Advance(TimeSpan.FromDays(2));

                // 3. Two days missed. In calendar mode the position skips ahead to wherever
                //    the calendar is now — the missed rewards are gone, not retroactively due.
                await UniDailyRewards.ClaimAsync(_cts.Token);
                _clock.Advance(TimeSpan.FromDays(3));

                // 4. Day seven: the milestone chest, on a rebuilt streak.
                await UniDailyRewards.ClaimAsync(_cts.Token);

                LogState("final");

                Debug.Log($"[DailyRewards] Held: {_granter.CountOf("coins")} coins, " +
                          $"{_granter.CountOf("gems")} gems, {_granter.CountOf("chest")} chests.");
            }
            catch (OperationCanceledException)
            {
                // The scene is going away; nothing to do.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[DailyRewards] Sample failed: {exception}");
            }
        }

        private static async UniTask<IContentService> LoadContentAsync(string label,
            CancellationToken cToken)
        {
            if (!UniResources.IsInitialized) await UniResources.InitializeAsync(cToken);

            // Bind the file name to the type before loading; the Addressable asset name must
            // match this string exactly or the loader skips it with a warning.
            ContentRegistry.Register<DailyRewardsData>(CalendarFile);

            var content = new ContentService();
            await content.LoadContentAsync(new[] { label }, cToken);

            return content;
        }

        private void LogState(string label)
        {
            var snapshot = UniDailyRewards.Snapshot;

            Debug.Log($"[DailyRewards] {label}: {snapshot.State}, streak {snapshot.Streak}, " +
                      $"slot {snapshot.CurrentSlotIndex + 1}/{snapshot.TotalDays}" +
                      (snapshot.IsMilestone ? " (milestone!)" : string.Empty) + ", " +
                      $"{snapshot.RemainingSeconds / 3600d:F1}h until the next claim.");
        }

        private void OnClaimed(DailyRewardClaimed @event) =>
            Debug.Log($"[DailyRewards] Collected day {@event.Day}: {@event.Amount}x {@event.ItemId} " +
                      $"(streak now {@event.Streak}).");

        private void OnStreakReset(DailyStreakReset @event) =>
            Debug.Log($"[DailyRewards] Streak of {@event.PreviousStreak} broken — missed a day.");
    }
}
