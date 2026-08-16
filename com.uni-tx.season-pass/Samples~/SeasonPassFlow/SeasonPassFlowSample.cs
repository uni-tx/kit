using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Events;
using UniTx.Resources;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.SeasonPass.Samples
{
    /// <summary>
    /// Grants rewards into a pretend inventory and logs what arrived.
    /// </summary>
    /// <remarks>
    /// This is the piece every game writes itself. Note that it returns a bool rather than
    /// throwing on refusal: returning false leaves the reward claimable and queued for retry,
    /// which is what keeps a full inventory or a dropped connection from eating a reward.
    /// </remarks>
    public sealed class SampleRewardGranter : ISeasonPassRewardGranter
    {
        private readonly Dictionary<string, int> _inventory = new();

        /// <summary>
        /// Gets how many of an item the player holds.
        /// </summary>
        /// <param name="itemId">The item to read.</param>
        public int CountOf(string itemId) => _inventory.GetValueOrDefault(itemId, 0);

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(SeasonRewardData reward, SeasonRewardRef reference,
            CancellationToken cToken = default)
        {
            _inventory[reward.ItemId] = CountOf(reward.ItemId) + reward.Amount;

            Debug.Log($"[SeasonPass] +{reward.Amount} {reward.ItemId} from tier {reference.Tier} " +
                      $"({reference.Track}). Held: {CountOf(reward.ItemId)}.");

            return UniTask.FromResult(true);
        }
    }

    /// <summary>
    /// A wallet over a plain dictionary, so the currency purchase path is exercised.
    /// </summary>
    public sealed class SampleWallet : ISeasonPassWallet
    {
        private readonly Dictionary<string, int> _balances = new() { ["gems"] = 800 };

        /// <inheritdoc />
        public int GetBalance(string currencyId) => _balances.GetValueOrDefault(currencyId, 0);

        /// <inheritdoc />
        public bool TrySpend(string currencyId, int amount)
        {
            if (GetBalance(currencyId) < amount) return false;

            // Check and deduct together. A wallet that reports success without deducting hands
            // out a free pass; one that deducts and reports failure charges for nothing.
            _balances[currencyId] = GetBalance(currencyId) - amount;

            return true;
        }
    }

    /// <summary>
    /// The whole season pass lifecycle in one script, with no UI.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> put <c>season_summer.json</c> (in this folder) somewhere Addressable with
    /// the label below, and make sure its asset name matches the registered file name. Then
    /// press play and read the console.
    /// </remarks>
    public sealed class SeasonPassFlowSample : MonoBehaviour
    {
        private const string SeasonsFile = "season_summer";

        [Tooltip("Addressables label the season definitions are tagged with.")]
        [SerializeField] private string _contentLabel = "content";

        [Tooltip("Policy asset. Leave empty to load Resources/UniSeasonPassConfig.")]
        [SerializeField] private UniSeasonPassConfig _config;

        private readonly SampleRewardGranter _granter = new();
        private readonly SampleWallet _wallet = new();
        private readonly List<SeasonRewardRef> _claimable = new();

        private SeasonPassService _service;
        private CancellationTokenSource _cts;

        private async void Start()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            if (!UniEvents.IsInitialized) UniEvents.Initialize();

            // Listening on the bus rather than polling: the same events drive a season screen,
            // a level-up toast and an analytics adapter without any of them knowing each other.
            UniEvents.Subscribe<SeasonTierUnlocked>(OnTierUnlocked);
            UniEvents.Subscribe<SeasonRewardClaimed>(OnRewardClaimed);
            UniEvents.Subscribe<SeasonChanged>(OnSeasonChanged);

            await RunAsync(_cts.Token);
        }

        private void OnDestroy()
        {
            UniEvents.Unsubscribe<SeasonTierUnlocked>(OnTierUnlocked);
            UniEvents.Unsubscribe<SeasonRewardClaimed>(OnRewardClaimed);
            UniEvents.Unsubscribe<SeasonChanged>(OnSeasonChanged);

            UniSeasonPass.Reset();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async UniTask RunAsync(CancellationToken cToken)
        {
            var content = await LoadContentAsync(_contentLabel, cToken);

            _service = new SeasonPassService(new LocalClock(), content,
                new LocalSeasonPassBackend(new SerialisationService()), _config);

            _service.SetRewardGranter(_granter);
            _service.SetWallet(_wallet);

            await UniSeasonPass.InitializeAsync(_service, cToken);

            var season = UniSeasonPass.Season;

            if (season == null)
            {
                Debug.LogWarning("[SeasonPass] No season is active — check the dates in the JSON.");
                return;
            }

            Debug.Log($"[SeasonPass] '{season.DisplayName}' is {UniSeasonPass.Phase}, " +
                      $"{UniSeasonPass.Snapshot.TimeRemaining.TotalDays:F1} days left.");

            // 1. Earn. The source id has to be one the season whitelists, and the grant id makes
            //    a retry after a dropped connection free rather than double-paying.
            await UniSeasonPass.GrantXpAsync("match_complete", grantId: "match-0001", cToken: cToken);
            await UniSeasonPass.GrantXpAsync("match_complete", 250, "match-0002", cToken);

            Debug.Log($"[SeasonPass] Tier {UniSeasonPass.Snapshot.Progress.Tier}, " +
                      $"{UniSeasonPass.Snapshot.Progress.XpToNextTier} XP to the next one.");

            // 2. Claim the free track. Premium slots are refused until the track is owned, which
            //    is what makes them visible-but-locked in a UI.
            UniSeasonPass.GetClaimable(_claimable);
            Debug.Log($"[SeasonPass] {_claimable.Count} rewards waiting to be collected.");

            await UniSeasonPass.ClaimAllAsync(cToken);

            // 3. Buy the paid track mid-season. Everything already passed on it pays out
            //    immediately — without that, the player has bought rewards they cannot reach.
            var unlock = await UniSeasonPass.UnlockTrackAsync(SeasonTrack.Premium,
                SeasonPassPayment.Currency, cToken);

            Debug.Log($"[SeasonPass] Premium unlock: {unlock}. Gems left: {_wallet.GetBalance("gems")}.");

            // 4. Skip a tier. The skip converts to exactly the XP the next tier needs, so total
            //    XP stays the only number that decides tier standing.
            await UniSeasonPass.BuyTierSkipsAsync(1, SeasonPassPayment.Currency, cToken);

            Debug.Log($"[SeasonPass] After one skip: tier {UniSeasonPass.Snapshot.Progress.Tier}.");

            // 5. Quests feed the same XP pool, but pay out once and ignore the daily caps.
            await UniSeasonPass.ReportQuestProgressAsync("daily_win", 1, cToken);
            await UniSeasonPass.ReportQuestProgressAsync("daily_win", 1, cToken);

            // 6. Refresh is what notices the passage of time — rollover, expiry, window resets
            //    and retries all happen here. Call it on resume and when the screen opens.
            await UniSeasonPass.RefreshAsync(cToken);

            Debug.Log($"[SeasonPass] Final: tier {UniSeasonPass.Snapshot.Progress.Tier}, " +
                      $"{UniSeasonPass.Snapshot.ClaimableCount} unclaimed, " +
                      $"{_granter.CountOf("coins")} coins held.");
        }

        private static async UniTask<IContentService> LoadContentAsync(string label,
            CancellationToken cToken)
        {
            if (!UniResources.IsInitialized) await UniResources.InitializeAsync(cToken);

            // Bind the file name to the type before loading; the Addressable asset name must
            // match this string exactly or the loader skips it with a warning.
            ContentRegistry.Register<SeasonPassData>(SeasonsFile);

            var content = new ContentService();
            await content.LoadContentAsync(new[] { label }, cToken);

            return content;
        }

        private void OnTierUnlocked(SeasonTierUnlocked @event) =>
            Debug.Log($"[SeasonPass] Reached tier {@event.Tier}" +
                      (@event.IsBonusTier ? " (bonus)." : "."));

        private void OnRewardClaimed(SeasonRewardClaimed @event) =>
            Debug.Log($"[SeasonPass] Collected {@event.Reward}" +
                      (@event.WasAutomatic ? " automatically." : "."));

        private void OnSeasonChanged(SeasonChanged @event) =>
            Debug.Log($"[SeasonPass] Season rolled from '{@event.PreviousSeasonId}' to " +
                      $"'{@event.SeasonId}'. {@event.ForfeitedRewards} rewards expired unclaimed.");
    }
}
