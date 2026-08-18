using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Events;
using UniTx.Resources;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Ladder.Samples
{
    /// <summary>
    /// Grants rewards into a pretend inventory and logs what arrived.
    /// </summary>
    /// <remarks>
    /// This is the piece every game writes itself. Note that it returns a bool rather than
    /// throwing on refusal: returning false leaves the rung claimable and queued for
    /// retry, which is what keeps a full inventory or a dropped connection from eating a
    /// reward.
    /// </remarks>
    public sealed class SampleLadderRewardGranter : ILadderRewardGranter
    {
        private readonly Dictionary<string, int> _inventory = new();

        /// <summary>
        /// Gets how many of an item the player holds.
        /// </summary>
        /// <param name="itemId">The item to read.</param>
        public int CountOf(string itemId) => _inventory.GetValueOrDefault(itemId, 0);

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(LadderRungData rung, LadderRewardData reward,
            LadderRungRef reference, string grantId, CancellationToken cToken = default)
        {
            _inventory[reward.ItemId] = CountOf(reward.ItemId) + reward.Amount;

            Debug.Log($"[Ladder] +{reward.Amount} {reward.ItemId} from rung " +
                      $"'{reference.RungId}'. Held: {CountOf(reward.ItemId)}.");

            return UniTask.FromResult(true);
        }
    }

    /// <summary>
    /// The whole ladder lifecycle in one script, with no UI.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> put <c>ladder_default.json</c> (in this folder) somewhere Addressable
    /// with the label below, and make sure its asset name matches the registered file name.
    /// Then press play and read the console.
    /// <para>
    /// Steps come from <see cref="UniLadder.ReportStepsAsync"/> directly — in a real game
    /// the <c>QuestsLadderBridge</c> integration does this automatically for every claimed
    /// quest.
    /// </para>
    /// </remarks>
    public sealed class LadderFlowSample : MonoBehaviour
    {
        private const string LadderFile = "ladder_default";

        [Tooltip("Addressables label the ladder definitions are tagged with.")]
        [SerializeField] private string _contentLabel = "content";

        [Tooltip("Policy asset. Leave empty to load Resources/UniLadderConfig.")]
        [SerializeField] private UniLadderConfig _config;

        private readonly SampleLadderRewardGranter _granter = new();

        private LadderService _service;
        private CancellationTokenSource _cts;

        private void Start() => RunAsync().Forget();

        private void OnDestroy()
        {
            UniEvents.Unsubscribe<LadderStepsAdded>(OnStepsAdded);
            UniEvents.Unsubscribe<LadderRungReached>(OnRungReached);
            UniEvents.Unsubscribe<LadderRungClaimed>(OnRungClaimed);
            UniEvents.Unsubscribe<LadderCompleted>(OnCompleted);

            UniLadder.Reset();

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

                // Listening on the bus rather than polling: the same events drive a ladder
                // screen, a toast and an analytics adapter without any of them knowing each
                // other.
                UniEvents.Subscribe<LadderStepsAdded>(OnStepsAdded);
                UniEvents.Subscribe<LadderRungReached>(OnRungReached);
                UniEvents.Subscribe<LadderRungClaimed>(OnRungClaimed);
                UniEvents.Subscribe<LadderCompleted>(OnCompleted);

                var content = await LoadContentAsync(_contentLabel, _cts.Token);

                _service = new LadderService(content,
                    new LocalLadderBackend(new SerialisationService()), _config);

                _service.SetRewardGranter(_granter);

                await UniLadder.InitializeAsync(_service, _cts.Token);

                if (UniLadder.Ladder == null)
                {
                    Debug.LogWarning("[Ladder] No ladder is registered — check the JSON and its label.");
                    return;
                }

                LogState("ready");

                // 1. A quest was claimed: the climb gains one step. (In a real game the
                // QuestsLadderBridge integration raises this from the QuestClaimed event.)
                await UniLadder.ReportStepsAsync(1, _cts.Token);
                await UniLadder.ReportStepsAsync(1, _cts.Token);

                // 2. The first rung is reached at 1 step; claim it.
                var first = await UniLadder.ClaimAsync("first_claim", _cts.Token);

                Debug.Log($"[Ladder] Claiming 'first_claim': {first}.");

                LogState("first rung claimed");

                // 3. More quests: the climb crosses the second threshold.
                await UniLadder.ReportStepsAsync(1, _cts.Token);
                await UniLadder.ReportStepsAsync(1, _cts.Token);

                var second = await UniLadder.ClaimAsync("three_claims", _cts.Token);

                Debug.Log($"[Ladder] Claiming 'three_claims': {second}.");

                LogState("second rung claimed");

                // 4. The grand prize: the top rung completes the ladder.
                await UniLadder.ReportStepsAsync(1, _cts.Token);

                var top = await UniLadder.ClaimAsync("grand_prize", _cts.Token);

                Debug.Log($"[Ladder] Claiming 'grand_prize': {top}.");

                LogState("complete");

                Debug.Log($"[Ladder] Held: {_granter.CountOf("coins")} coins, " +
                          $"{_granter.CountOf("gems")} gems.");
            }
            catch (OperationCanceledException)
            {
                // The scene is going away; nothing to do.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Ladder] Sample failed: {exception}");
            }
        }

        private static async UniTask<IContentService> LoadContentAsync(string label,
            CancellationToken cToken)
        {
            if (!UniResources.IsInitialized) await UniResources.InitializeAsync(cToken);

            // Bind the file name to the type before loading; the Addressable asset name must
            // match this string exactly or the loader skips it with a warning.
            ContentRegistry.Register<LadderData>(LadderFile);

            var content = new ContentService();
            await content.LoadContentAsync(new[] { label }, cToken);

            return content;
        }

        private void LogState(string label)
        {
            var snapshot = UniLadder.Snapshot;

            Debug.Log($"[Ladder] {label}: {snapshot.TotalSteps} steps, " +
                      $"next rung at {snapshot.NextRungSteps}, " +
                      $"progress {snapshot.Progress:P0}.");

            foreach (var rung in snapshot.Rungs)
            {
                Debug.Log($"[Ladder]   '{rung.RungId}': {rung.State} (at {rung.Steps}).");
            }
        }

        private void OnStepsAdded(LadderStepsAdded @event) =>
            Debug.Log($"[Ladder] +{@event.Steps} steps, now {@event.TotalSteps}.");

        private void OnRungReached(LadderRungReached @event) =>
            Debug.Log($"[Ladder] Rung '{@event.RungId}' reached at {@event.Steps} steps!");

        private void OnRungClaimed(LadderRungClaimed @event) =>
            Debug.Log($"[Ladder] Collected {@event.Rewards?.Count ?? 0} rewards from " +
                      $"'{@event.RungId}'.");

        private void OnCompleted(LadderCompleted @event) =>
            Debug.Log($"[Ladder] Ladder '{@event.LadderId}' complete — grand prize " +
                      $"'{@event.RungId}' claimed!");
    }
}
