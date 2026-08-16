using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Runs one season pass: progression, ownership, claiming and rollover.
    /// </summary>
    public interface ISeasonPassService : IInjectable, IInitializableAsync, IResettable
    {
        /// <summary>
        /// Indicates whether content and saved progress are loaded.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the active season definition, or null when none is selected.
        /// </summary>
        SeasonPassData Season { get; }

        /// <summary>
        /// Gets the current lifecycle phase.
        /// </summary>
        SeasonPhase Phase { get; }

        /// <summary>
        /// Gets everything a season pass screen needs in one value.
        /// </summary>
        SeasonPassSnapshot Snapshot { get; }

        /// <summary>
        /// Raised whenever the snapshot changes, for UI that binds rather than polls.
        /// </summary>
        event Action<SeasonPassSnapshot> OnChanged;

        /// <summary>
        /// Installs the component that delivers rewards into the game's economy.
        /// </summary>
        /// <param name="granter">The granter to use.</param>
        void SetRewardGranter(ISeasonPassRewardGranter granter);

        /// <summary>
        /// Installs the wallet used for currency purchases.
        /// </summary>
        /// <param name="wallet">The wallet to charge.</param>
        void SetWallet(ISeasonPassWallet wallet);

        /// <summary>
        /// Indicates whether the player owns a track this season.
        /// </summary>
        /// <param name="track">The track to test.</param>
        bool OwnsTrack(SeasonTrack track);

        /// <summary>
        /// Indicates whether a specific reward can be claimed right now.
        /// </summary>
        /// <param name="reward">The reward slot to test.</param>
        bool IsClaimable(SeasonRewardRef reward);

        /// <summary>
        /// Fills a buffer with every reward the player can claim right now.
        /// </summary>
        /// <param name="buffer">Buffer to fill. Cleared first.</param>
        /// <returns>How many rewards were written.</returns>
        int GetClaimable(List<SeasonRewardRef> buffer);

        /// <summary>
        /// Adds season XP from a whitelisted source.
        /// </summary>
        /// <param name="sourceId">The source id declared in the season definition.</param>
        /// <param name="amount">XP to add, or zero to use the source's configured amount.</param>
        /// <param name="grantId">Idempotency id; a repeat of the same id is ignored.</param>
        /// <param name="cToken">Token to cancel the grant.</param>
        /// <returns>What happened, including whether a daily cap trimmed the amount.</returns>
        UniTask<XpGrantResult> GrantXpAsync(string sourceId, int amount = 0, string grantId = null,
            CancellationToken cToken = default);

        /// <summary>
        /// Claims one reward.
        /// </summary>
        /// <param name="reward">The reward slot to claim.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        UniTask<ClaimResult> ClaimAsync(SeasonRewardRef reward, CancellationToken cToken = default);

        /// <summary>
        /// Claims every reward on one tier of one track.
        /// </summary>
        /// <param name="tier">The 1-based tier number.</param>
        /// <param name="track">The track to claim from.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        /// <returns>How many rewards were delivered.</returns>
        UniTask<int> ClaimTierAsync(int tier, SeasonTrack track, CancellationToken cToken = default);

        /// <summary>
        /// Claims everything currently claimable, including previously failed deliveries.
        /// </summary>
        /// <param name="cToken">Token to cancel the operation.</param>
        /// <returns>How many rewards were delivered.</returns>
        UniTask<int> ClaimAllAsync(CancellationToken cToken = default);

        /// <summary>
        /// Unlocks a paid track and back-grants every tier already passed.
        /// </summary>
        /// <param name="track">The track to unlock.</param>
        /// <param name="payment">Charge the wallet, or record an unlock already paid for.</param>
        /// <param name="cToken">Token to cancel the unlock.</param>
        UniTask<TrackUnlockResult> UnlockTrackAsync(SeasonTrack track,
            SeasonPassPayment payment = SeasonPassPayment.Currency,
            CancellationToken cToken = default);

        /// <summary>
        /// Buys tier skips, banking any that fall past the end of the ladder.
        /// </summary>
        /// <param name="count">How many tiers to skip.</param>
        /// <param name="payment">Charge the wallet, or record skips already paid for.</param>
        /// <param name="cToken">Token to cancel the purchase.</param>
        /// <returns>How many skips were applied or banked.</returns>
        UniTask<int> BuyTierSkipsAsync(int count,
            SeasonPassPayment payment = SeasonPassPayment.Currency,
            CancellationToken cToken = default);

        /// <summary>
        /// Records progress against a season quest, paying its XP on completion.
        /// </summary>
        /// <param name="questId">The quest id.</param>
        /// <param name="amount">How much progress to add.</param>
        /// <param name="cToken">Token to cancel the update.</param>
        UniTask<QuestProgressResult> ReportQuestProgressAsync(string questId, int amount = 1,
            CancellationToken cToken = default);

        /// <summary>
        /// Re-evaluates the clock: rollover, expiry, window resets, sync and retries.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        /// <remarks>
        /// Call on app resume and when the season screen opens. Nothing else drives the
        /// passage of time, so a session left open across a season boundary only notices when
        /// this runs.
        /// </remarks>
        UniTask RefreshAsync(CancellationToken cToken = default);
    }
}
