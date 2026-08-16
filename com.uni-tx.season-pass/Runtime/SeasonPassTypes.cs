using System;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// A reward track within a season.
    /// </summary>
    /// <remarks>
    /// Ordered by entitlement: owning a higher track implies owning every lower one, which is
    /// what lets a single comparison answer "can this player claim this reward".
    /// </remarks>
    public enum SeasonTrack
    {
        /// <summary>
        /// Available to every player.
        /// </summary>
        Free = 0,

        /// <summary>
        /// Unlocked by purchase, with money or in-game currency.
        /// </summary>
        Premium = 1,

        /// <summary>
        /// A second paid tier above <see cref="Premium"/>, usually bundled with tier skips.
        /// </summary>
        PremiumPlus = 2,
    }

    /// <summary>
    /// Where the current season sits in its lifecycle.
    /// </summary>
    public enum SeasonPhase
    {
        /// <summary>
        /// No season is loaded.
        /// </summary>
        None = 0,

        /// <summary>
        /// Announced, but not yet earning.
        /// </summary>
        NotStarted = 1,

        /// <summary>
        /// Running normally.
        /// </summary>
        Active = 2,

        /// <summary>
        /// Running, inside the final stretch the UI should warn about.
        /// </summary>
        EndingSoon = 3,

        /// <summary>
        /// Earning is closed; claiming is still open.
        /// </summary>
        Grace = 4,

        /// <summary>
        /// Closed. Nothing can be earned or claimed.
        /// </summary>
        Ended = 5,
    }

    /// <summary>
    /// What a reward actually is, so the granter can route it.
    /// </summary>
    public enum SeasonRewardKind
    {
        /// <summary>
        /// Soft or hard currency, granted by amount.
        /// </summary>
        Currency = 0,

        /// <summary>
        /// A consumable or inventory item.
        /// </summary>
        Item = 1,

        /// <summary>
        /// A permanent unlock — skin, emote, avatar.
        /// </summary>
        Cosmetic = 2,

        /// <summary>
        /// A timed multiplier or boost.
        /// </summary>
        Booster = 3,

        /// <summary>
        /// Anything game-specific; the granter interprets the item id.
        /// </summary>
        Custom = 4,
    }

    /// <summary>
    /// How often a quest resets.
    /// </summary>
    public enum SeasonQuestScope
    {
        /// <summary>
        /// Resets at UTC midnight.
        /// </summary>
        Daily = 0,

        /// <summary>
        /// Resets at UTC midnight on Monday.
        /// </summary>
        Weekly = 1,

        /// <summary>
        /// Lives for the whole season.
        /// </summary>
        Seasonal = 2,
    }

    /// <summary>
    /// What happens to rewards a player unlocked but never claimed when a season closes.
    /// </summary>
    public enum SeasonExpiryPolicy
    {
        /// <summary>
        /// Grant everything unclaimed automatically at rollover.
        /// </summary>
        /// <remarks>
        /// The forgiving option, and what Fortnite does. Costs nothing in goodwill and removes
        /// a whole class of support tickets from players who earned a reward and never tapped it.
        /// </remarks>
        AutoGrant = 0,

        /// <summary>
        /// Keep claiming open for a fixed window after the end date, then forfeit.
        /// </summary>
        GraceWindow = 1,

        /// <summary>
        /// Unclaimed rewards are lost the moment the season ends.
        /// </summary>
        Forfeit = 2,
    }

    /// <summary>
    /// How a purchase was paid for.
    /// </summary>
    public enum SeasonPassPayment
    {
        /// <summary>
        /// Deduct the configured in-game currency through <see cref="ISeasonPassWallet"/>.
        /// </summary>
        Currency = 0,

        /// <summary>
        /// Already paid elsewhere — a store purchase, a promotion, or a server grant.
        /// </summary>
        /// <remarks>
        /// The season pass never talks to a store itself. Real-money unlocks arrive here from
        /// the IAP entitlement event, which is the only place restores and deferred orders show up.
        /// </remarks>
        External = 1,
    }

    /// <summary>
    /// Outcome of an XP grant.
    /// </summary>
    public enum XpGrantResult
    {
        /// <summary>
        /// The XP was added.
        /// </summary>
        Granted = 0,

        /// <summary>
        /// The same grant id was already applied; nothing changed.
        /// </summary>
        Duplicate = 1,

        /// <summary>
        /// Some or all of the XP was dropped by the source's daily cap.
        /// </summary>
        Capped = 2,

        /// <summary>
        /// The source id is not in the season's whitelist.
        /// </summary>
        UnknownSource = 3,

        /// <summary>
        /// The source is premium-only and the player does not own a paid track.
        /// </summary>
        TrackNotOwned = 4,

        /// <summary>
        /// The season is not currently earning.
        /// </summary>
        SeasonInactive = 5,

        /// <summary>
        /// The amount was zero or negative, or no season is loaded.
        /// </summary>
        Rejected = 6,

        /// <summary>
        /// The backend is unreachable and offline earning is switched off.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="SeasonInactive"/> on purpose: the season is running fine
        /// and the player has done nothing wrong, so the UI should say "reconnect to earn"
        /// rather than "the season is over".
        /// </remarks>
        Offline = 7,
    }

    /// <summary>
    /// Outcome of a claim attempt.
    /// </summary>
    public enum ClaimResult
    {
        /// <summary>
        /// The reward was delivered and recorded.
        /// </summary>
        Claimed = 0,

        /// <summary>
        /// Already claimed; nothing changed.
        /// </summary>
        AlreadyClaimed = 1,

        /// <summary>
        /// The player has not reached the tier yet.
        /// </summary>
        TierNotReached = 2,

        /// <summary>
        /// The reward belongs to a track the player does not own.
        /// </summary>
        TrackNotOwned = 3,

        /// <summary>
        /// The claim window has closed.
        /// </summary>
        SeasonExpired = 4,

        /// <summary>
        /// No reward exists at that tier on that track.
        /// </summary>
        NothingToClaim = 5,

        /// <summary>
        /// The granter refused or failed; the reward stays unclaimed and is retried.
        /// </summary>
        GrantFailed = 6,

        /// <summary>
        /// No season is loaded.
        /// </summary>
        NoSeason = 7,
    }

    /// <summary>
    /// Outcome of a track unlock.
    /// </summary>
    public enum TrackUnlockResult
    {
        /// <summary>
        /// The track is now owned, and passed tiers were back-granted.
        /// </summary>
        Unlocked = 0,

        /// <summary>
        /// The player already owned it; nothing was charged.
        /// </summary>
        AlreadyOwned = 1,

        /// <summary>
        /// The wallet refused the charge.
        /// </summary>
        InsufficientFunds = 2,

        /// <summary>
        /// The season does not sell this track by the requested payment method.
        /// </summary>
        NotPurchasable = 3,

        /// <summary>
        /// The season is closed.
        /// </summary>
        SeasonInactive = 4,

        /// <summary>
        /// No season is loaded, or <see cref="SeasonTrack.Free"/> was requested.
        /// </summary>
        Rejected = 5,
    }

    /// <summary>
    /// Outcome of reporting quest progress.
    /// </summary>
    public enum QuestProgressResult
    {
        /// <summary>
        /// Progress was recorded, and the quest is still open.
        /// </summary>
        Advanced = 0,

        /// <summary>
        /// Progress completed the quest and its XP was granted.
        /// </summary>
        Completed = 1,

        /// <summary>
        /// The quest was already complete.
        /// </summary>
        AlreadyComplete = 2,

        /// <summary>
        /// No quest with that id exists in this season.
        /// </summary>
        UnknownQuest = 3,

        /// <summary>
        /// The quest is outside its availability window, or premium-locked.
        /// </summary>
        Unavailable = 4,

        /// <summary>
        /// The season is not currently earning, or the amount was not positive.
        /// </summary>
        Rejected = 5,
    }

    /// <summary>
    /// Identifies one reward slot within a season.
    /// </summary>
    /// <remarks>
    /// A readonly struct so enumerating claimable rewards every UI frame costs no allocation.
    /// </remarks>
    public readonly struct SeasonRewardRef : IEquatable<SeasonRewardRef>
    {
        /// <summary>
        /// The season the reward belongs to.
        /// </summary>
        public readonly string SeasonId;

        /// <summary>
        /// The tier number, 1-based.
        /// </summary>
        public readonly int Tier;

        /// <summary>
        /// The track the reward sits on.
        /// </summary>
        public readonly SeasonTrack Track;

        /// <summary>
        /// The reward id within the tier.
        /// </summary>
        public readonly string RewardId;

        /// <summary>
        /// Creates a reference to one reward slot.
        /// </summary>
        /// <param name="seasonId">The owning season id.</param>
        /// <param name="tier">The 1-based tier number.</param>
        /// <param name="track">The track the reward sits on.</param>
        /// <param name="rewardId">The reward id within the tier.</param>
        public SeasonRewardRef(string seasonId, int tier, SeasonTrack track, string rewardId)
        {
            SeasonId = seasonId;
            Tier = tier;
            Track = track;
            RewardId = rewardId;
        }

        /// <summary>
        /// Builds the stable key under which this claim is recorded in the save.
        /// </summary>
        /// <remarks>
        /// Deliberately excludes the season id — the save already scopes to one season, and
        /// including it would break every recorded claim the moment a season id is corrected.
        /// </remarks>
        public string ToClaimKey() => $"t{Tier}:{(int)Track}:{RewardId}";

        /// <summary>
        /// Rebuilds a reference from a recorded claim key.
        /// </summary>
        /// <param name="seasonId">The season the key belongs to.</param>
        /// <param name="claimKey">A key produced by <see cref="ToClaimKey"/>.</param>
        /// <param name="reference">The parsed reference.</param>
        /// <returns><c>true</c> when the key was well formed.</returns>
        /// <remarks>
        /// The retry queue stores keys rather than a parallel list of references, so a failed
        /// claim cannot drift out of sync with the claim it represents.
        /// </remarks>
        public static bool TryParseClaimKey(string seasonId, string claimKey, out SeasonRewardRef reference)
        {
            reference = default;

            if (string.IsNullOrEmpty(claimKey) || claimKey[0] != 't') return false;

            var trackSeparator = claimKey.IndexOf(':');
            if (trackSeparator < 2) return false;

            var rewardSeparator = claimKey.IndexOf(':', trackSeparator + 1);
            if (rewardSeparator < 0 || rewardSeparator == claimKey.Length - 1) return false;

            var tierText = claimKey.Substring(1, trackSeparator - 1);
            var trackText = claimKey.Substring(trackSeparator + 1, rewardSeparator - trackSeparator - 1);

            if (!int.TryParse(tierText, out var tier) || !int.TryParse(trackText, out var track)) return false;

            // The reward id may itself contain a colon, so it is everything after the second one.
            reference = new SeasonRewardRef(seasonId, tier, (SeasonTrack)track, claimKey[(rewardSeparator + 1)..]);

            return true;
        }

        /// <inheritdoc />
        public bool Equals(SeasonRewardRef other) =>
            Tier == other.Tier && Track == other.Track &&
            string.Equals(RewardId, other.RewardId, StringComparison.Ordinal) &&
            string.Equals(SeasonId, other.SeasonId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SeasonRewardRef other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(SeasonId, Tier, (int)Track, RewardId);

        /// <inheritdoc />
        public override string ToString() => $"{SeasonId}/{ToClaimKey()}";
    }

    /// <summary>
    /// Where the player stands within the tier ladder.
    /// </summary>
    public readonly struct TierProgress
    {
        /// <summary>
        /// The highest tier reached, 1-based. Zero means below tier 1.
        /// </summary>
        public readonly int Tier;

        /// <summary>
        /// Total season XP earned.
        /// </summary>
        public readonly int TotalXp;

        /// <summary>
        /// XP earned since the current tier's threshold.
        /// </summary>
        public readonly int XpIntoTier;

        /// <summary>
        /// XP the current tier spans, or the bonus-tier cost past the ladder's end.
        /// </summary>
        public readonly int XpPerTier;

        /// <summary>
        /// How many repeatable bonus tiers were earned past the final tier.
        /// </summary>
        public readonly int BonusTiers;

        /// <summary>
        /// Indicates whether the final configured tier has been reached.
        /// </summary>
        public readonly bool IsMaxTier;

        /// <summary>
        /// Creates a progress snapshot.
        /// </summary>
        /// <param name="tier">The highest tier reached.</param>
        /// <param name="totalXp">Total season XP.</param>
        /// <param name="xpIntoTier">XP past the current tier's threshold.</param>
        /// <param name="xpPerTier">XP the current tier spans.</param>
        /// <param name="bonusTiers">Repeatable tiers earned past the ladder.</param>
        /// <param name="isMaxTier">Whether the final tier is reached.</param>
        public TierProgress(int tier, int totalXp, int xpIntoTier, int xpPerTier, int bonusTiers,
            bool isMaxTier)
        {
            Tier = tier;
            TotalXp = totalXp;
            XpIntoTier = xpIntoTier;
            XpPerTier = xpPerTier;
            BonusTiers = bonusTiers;
            IsMaxTier = isMaxTier;
        }

        /// <summary>
        /// Gets progress through the current tier, 0 to 1, for a progress bar.
        /// </summary>
        public float Normalized => XpPerTier <= 0 ? 1f : Math.Min(1f, XpIntoTier / (float)XpPerTier);

        /// <summary>
        /// Gets how much XP remains before the next tier.
        /// </summary>
        public int XpToNextTier => XpPerTier <= 0 ? 0 : Math.Max(0, XpPerTier - XpIntoTier);
    }

    /// <summary>
    /// Everything a season pass screen needs, captured in one value.
    /// </summary>
    public readonly struct SeasonPassSnapshot
    {
        /// <summary>
        /// The active season id, or null when none is loaded.
        /// </summary>
        public readonly string SeasonId;

        /// <summary>
        /// The season's lifecycle phase.
        /// </summary>
        public readonly SeasonPhase Phase;

        /// <summary>
        /// The player's tier standing.
        /// </summary>
        public readonly TierProgress Progress;

        /// <summary>
        /// The highest track the player owns.
        /// </summary>
        public readonly SeasonTrack HighestOwnedTrack;

        /// <summary>
        /// How many rewards are unlocked, owned and unclaimed.
        /// </summary>
        public readonly int ClaimableCount;

        /// <summary>
        /// Time until the season stops earning. Zero once it has ended.
        /// </summary>
        public readonly TimeSpan TimeRemaining;

        /// <summary>
        /// Tier skips bought past the final tier, waiting for the next season.
        /// </summary>
        public readonly int BankedTierSkips;

        /// <summary>
        /// Creates a snapshot.
        /// </summary>
        /// <param name="seasonId">The active season id.</param>
        /// <param name="phase">The lifecycle phase.</param>
        /// <param name="progress">The tier standing.</param>
        /// <param name="highestOwnedTrack">The highest owned track.</param>
        /// <param name="claimableCount">How many rewards are claimable now.</param>
        /// <param name="timeRemaining">Time until earning closes.</param>
        /// <param name="bankedTierSkips">Skips carried to the next season.</param>
        public SeasonPassSnapshot(string seasonId, SeasonPhase phase, TierProgress progress,
            SeasonTrack highestOwnedTrack, int claimableCount, TimeSpan timeRemaining,
            int bankedTierSkips)
        {
            SeasonId = seasonId;
            Phase = phase;
            Progress = progress;
            HighestOwnedTrack = highestOwnedTrack;
            ClaimableCount = claimableCount;
            TimeRemaining = timeRemaining;
            BankedTierSkips = bankedTierSkips;
        }

        /// <summary>
        /// Indicates whether the season is currently earning XP.
        /// </summary>
        public bool IsEarning => Phase is SeasonPhase.Active or SeasonPhase.EndingSoon;
    }
}
