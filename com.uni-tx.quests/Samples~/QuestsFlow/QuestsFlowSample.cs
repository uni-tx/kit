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

namespace UniTx.Quests.Samples
{
    /// <summary>
    /// Grants rewards into a pretend inventory and logs what arrived.
    /// </summary>
    /// <remarks>
    /// This is the piece every game writes itself. Note that it returns a bool rather than
    /// throwing on refusal: returning false leaves the quest claimable and queued for
    /// retry, which is what keeps a full inventory or a dropped connection from eating a
    /// reward.
    /// </remarks>
    public sealed class SampleQuestRewardGranter : IQuestRewardGranter
    {
        private readonly Dictionary<string, int> _inventory = new();

        /// <summary>
        /// Gets how many of an item the player holds.
        /// </summary>
        /// <param name="itemId">The item to read.</param>
        public int CountOf(string itemId) => _inventory.GetValueOrDefault(itemId, 0);

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(QuestData quest, QuestRewardData reward,
            QuestRef reference, string grantId, CancellationToken cToken = default)
        {
            _inventory[reward.ItemId] = CountOf(reward.ItemId) + reward.Amount;

            Debug.Log($"[Quests] +{reward.Amount} {reward.ItemId} from '{reference.QuestId}'. " +
                      $"Held: {CountOf(reward.ItemId)}.");

            return UniTask.FromResult(true);
        }
    }

    /// <summary>
    /// A clock the sample drives by hand, so the daily rollover is visible in one run.
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
    /// The whole quest lifecycle in one script, with no UI.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> put <c>quests_default.json</c> (in this folder) somewhere Addressable
    /// with the label below, and make sure its asset name matches the registered file name.
    /// Then press play and read the console.
    /// </remarks>
    public sealed class QuestsFlowSample : MonoBehaviour
    {
        private const string SetFile = "quests_default";

        [Tooltip("Addressables label the quest set definitions are tagged with.")]
        [SerializeField] private string _contentLabel = "content";

        [Tooltip("Policy asset. Leave empty to load Resources/UniQuestsConfig.")]
        [SerializeField] private UniQuestsConfig _config;

        private readonly SampleQuestRewardGranter _granter = new();

        private QuestsService _service;
        private SampleClock _clock;
        private CancellationTokenSource _cts;

        private void Start() => RunAsync().Forget();

        private void OnDestroy()
        {
            UniEvents.Unsubscribe<QuestStarted>(OnStarted);
            UniEvents.Unsubscribe<QuestProgressed>(OnProgressed);
            UniEvents.Unsubscribe<QuestCompleted>(OnCompleted);
            UniEvents.Unsubscribe<QuestClaimed>(OnClaimed);
            UniEvents.Unsubscribe<QuestPeriodReset>(OnPeriodReset);

            UniQuests.Reset();

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

                // Listening on the bus rather than polling: the same events drive a quests
                // screen, a toast and an analytics adapter without any of them knowing each
                // other.
                UniEvents.Subscribe<QuestStarted>(OnStarted);
                UniEvents.Subscribe<QuestProgressed>(OnProgressed);
                UniEvents.Subscribe<QuestCompleted>(OnCompleted);
                UniEvents.Subscribe<QuestClaimed>(OnClaimed);
                UniEvents.Subscribe<QuestPeriodReset>(OnPeriodReset);

                var content = await LoadContentAsync(_contentLabel, _cts.Token);

                // A fixed start time so the daily rollover can be demonstrated deterministically.
                _clock = new SampleClock(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));

                _service = new QuestsService(_clock, content,
                    new LocalQuestsBackend(new SerialisationService()), _config);

                _service.SetRewardGranter(_granter);

                await UniQuests.InitializeAsync(_service, _cts.Token);

                if (UniQuests.Set == null)
                {
                    Debug.LogWarning("[Quests] No quest set is registered — check the JSON and its label.");
                    return;
                }

                LogState("ready");

                // 1. Progress reported from gameplay: a match was won.
                await UniQuests.ReportProgressAsync("win_match", 1, _cts.Token);
                await UniQuests.ReportProgressAsync("win_match", 1, _cts.Token);

                // 2. A second objective key the set knows about.
                await UniQuests.ReportProgressAsync("play_session", 1, _cts.Token);

                // 3. The daily quest is complete; claim it.
                var result = await UniQuests.ClaimAsync("daily_win", _cts.Token);

                Debug.Log($"[Quests] Claiming 'daily_win': {result}.");

                LogState("claimed");

                // 4. Midnight: the daily quest rolls over and is fresh again.
                _clock.Advance(TimeSpan.FromDays(1));
                await UniQuests.RefreshAsync(_cts.Token);

                LogState("next day");

                Debug.Log($"[Quests] Held: {_granter.CountOf("coins")} coins, " +
                          $"{_granter.CountOf("gems")} gems.");
            }
            catch (OperationCanceledException)
            {
                // The scene is going away; nothing to do.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Quests] Sample failed: {exception}");
            }
        }

        private static async UniTask<IContentService> LoadContentAsync(string label,
            CancellationToken cToken)
        {
            if (!UniResources.IsInitialized) await UniResources.InitializeAsync(cToken);

            // Bind the file name to the type before loading; the Addressable asset name must
            // match this string exactly or the loader skips it with a warning.
            ContentRegistry.Register<QuestSetData>(SetFile);

            var content = new ContentService();
            await content.LoadContentAsync(new[] { label }, cToken);

            return content;
        }

        private void LogState(string label)
        {
            var snapshot = UniQuests.Snapshot;

            Debug.Log($"[Quests] {label}: {snapshot.Quests.Count} quests, " +
                      $"{snapshot.RemainingSeconds / 3600d:F1}h until the next reset.");

            foreach (var quest in snapshot.Quests)
            {
                Debug.Log($"[Quests]   '{quest.QuestId}': {quest.State} " +
                          $"({quest.CompletedObjectives}/{quest.TotalObjectives}).");
            }
        }

        private void OnStarted(QuestStarted @event) =>
            Debug.Log($"[Quests] Started '{@event.QuestId}'.");

        private void OnProgressed(QuestProgressed @event) =>
            Debug.Log($"[Quests] '{@event.QuestId}' {@event.ObjectiveKey}: " +
                      $"{@event.Current}/{@event.Target}.");

        private void OnCompleted(QuestCompleted @event) =>
            Debug.Log($"[Quests] Completed '{@event.QuestId}'!");

        private void OnClaimed(QuestClaimed @event) =>
            Debug.Log($"[Quests] Collected {@event.Rewards?.Count ?? 0} rewards from " +
                      $"'{@event.QuestId}'.");

        private void OnPeriodReset(QuestPeriodReset @event) =>
            Debug.Log($"[Quests] '{@event.QuestId}' rolled over ({@event.Reset}).");
    }
}
