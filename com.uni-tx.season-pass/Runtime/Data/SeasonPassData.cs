using System;
using System.Collections.Generic;
using System.Text;
using UniTx.Content;
using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// One season's static definition, loaded as JSON content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Static because it is the same for every player: a balance patch replaces this file
    /// without touching a single save. Everything per-player lives in
    /// <see cref="SeasonPassSavedData"/>. Keeping the two apart is what lets a season be
    /// retuned mid-flight, and what stops a rollover from deleting things players earned.
    /// </para>
    /// <para>
    /// Ship one file per season and let the dates decide which is active, rather than
    /// editing a single file in place — an overlapping definition is then a content bug you
    /// can see, not a save-corrupting surprise.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SeasonPassData : IData
    {
        [Tooltip("Unique season id. Changing it is what triggers a rollover on the client.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing season name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("ISO 8601 UTC start, e.g. 2026-09-01T00:00:00Z.")]
        [SerializeField] private string _startUtc;

        [Tooltip("ISO 8601 UTC end. Earning stops here.")]
        [SerializeField] private string _endUtc;

        [Tooltip("Hours after the end date during which unclaimed rewards can still be " +
                 "collected. Only used by the GraceWindow expiry policy.")]
        [SerializeField, Min(0)] private int _claimGraceHours = 48;

        [Tooltip("How many hours before the end the season reports EndingSoon, so the UI can " +
                 "nudge players who still have unclaimed tiers.")]
        [SerializeField, Min(0)] private int _endingSoonHours = 72;

        [Tooltip("The tier ladder. Thresholds are cumulative and are sorted on load, so the " +
                 "authoring order in the file does not matter.")]
        [SerializeField] private SeasonTierData[] _tiers;

        [Tooltip("XP per repeatable bonus tier past the final tier. 0 disables bonus tiers.")]
        [SerializeField, Min(0)] private int _bonusTierXp;

        [Tooltip("Rewards paid by each repeatable bonus tier.")]
        [SerializeField] private SeasonRewardData[] _bonusTierRewards;

        [Tooltip("How the paid tracks are sold this season.")]
        [SerializeField] private SeasonTrackOfferData[] _trackOffers;

        [Tooltip("Every accepted way to earn XP. An unlisted source id is refused.")]
        [SerializeField] private SeasonXpSourceData[] _xpSources;

        [Tooltip("Challenges that pay season XP.")]
        [SerializeField] private SeasonQuestData[] _quests;

        [Tooltip("Store product id for a single tier skip. Blank means skips are not sold for money.")]
        [SerializeField] private string _tierSkipProductId;

        [Tooltip("Currency id charged per tier skip. Blank means skips are not sold for currency.")]
        [SerializeField] private string _tierSkipCurrencyId;

        [Tooltip("Currency cost of one tier skip.")]
        [SerializeField, Min(0)] private int _tierSkipCurrencyCost;

        [Tooltip("Most tier skips one player may buy this season. 0 means unlimited.")]
        [SerializeField, Min(0)] private int _maxTierSkipPurchases;

        [NonSerialized] private bool _isPrepared;
        [NonSerialized] private SeasonTierData[] _sortedTiers;
        [NonSerialized] private DateTime _start;
        [NonSerialized] private DateTime _end;
        [NonSerialized] private Dictionary<string, SeasonXpSourceData> _sourcesById;
        [NonSerialized] private Dictionary<string, SeasonQuestData> _questsById;

        /// <inheritdoc />
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing season name or localization key.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the UTC moment the season starts earning.
        /// </summary>
        public DateTime StartUtc
        {
            get
            {
                Prepare();
                return _start;
            }
        }

        /// <summary>
        /// Gets the UTC moment the season stops earning.
        /// </summary>
        public DateTime EndUtc
        {
            get
            {
                Prepare();
                return _end;
            }
        }

        /// <summary>
        /// Gets the UTC moment the claim window closes under the grace policy.
        /// </summary>
        public DateTime GraceEndUtc => EndUtc.AddHours(_claimGraceHours);

        /// <summary>
        /// Gets the UTC moment the season starts reporting <see cref="SeasonPhase.EndingSoon"/>.
        /// </summary>
        public DateTime EndingSoonUtc => EndUtc.AddHours(-_endingSoonHours);

        /// <summary>
        /// Gets the tier ladder, ordered by required XP.
        /// </summary>
        public IReadOnlyList<SeasonTierData> Tiers
        {
            get
            {
                Prepare();
                return _sortedTiers;
            }
        }

        /// <summary>
        /// Gets the highest configured tier number, or zero when the ladder is empty.
        /// </summary>
        public int MaxTier
        {
            get
            {
                Prepare();
                return _sortedTiers.Length == 0 ? 0 : _sortedTiers[^1].Tier;
            }
        }

        /// <summary>
        /// Gets the XP each repeatable bonus tier costs, or zero when disabled.
        /// </summary>
        public int BonusTierXp => _bonusTierXp;

        /// <summary>
        /// Gets the rewards paid by each bonus tier.
        /// </summary>
        public SeasonRewardData[] BonusTierRewards => _bonusTierRewards ??= Array.Empty<SeasonRewardData>();

        /// <summary>
        /// Gets the XP sources this season accepts.
        /// </summary>
        public SeasonXpSourceData[] XpSources => _xpSources ??= Array.Empty<SeasonXpSourceData>();

        /// <summary>
        /// Gets this season's quests.
        /// </summary>
        public SeasonQuestData[] Quests => _quests ??= Array.Empty<SeasonQuestData>();

        /// <summary>
        /// Gets the store product id for a single tier skip.
        /// </summary>
        public string TierSkipProductId => _tierSkipProductId;

        /// <summary>
        /// Gets the currency id charged per tier skip.
        /// </summary>
        public string TierSkipCurrencyId => _tierSkipCurrencyId;

        /// <summary>
        /// Gets the currency cost of one tier skip.
        /// </summary>
        public int TierSkipCurrencyCost => _tierSkipCurrencyCost;

        /// <summary>
        /// Gets the per-player tier skip purchase limit, or zero when unlimited.
        /// </summary>
        public int MaxTierSkipPurchases => _maxTierSkipPurchases;

        /// <summary>
        /// Indicates whether tier skips can be bought with in-game currency.
        /// </summary>
        public bool SellsTierSkipsForCurrency =>
            !string.IsNullOrWhiteSpace(_tierSkipCurrencyId) && _tierSkipCurrencyCost > 0;

        /// <summary>
        /// Returns the tier with the given number, or null.
        /// </summary>
        /// <param name="tier">The 1-based tier number.</param>
        public SeasonTierData GetTier(int tier)
        {
            Prepare();

            foreach (var candidate in _sortedTiers)
            {
                if (candidate.Tier == tier) return candidate;
            }

            return null;
        }

        /// <summary>
        /// Returns the rewards a tier pays, falling back to the repeatable bonus rewards.
        /// </summary>
        /// <param name="tier">The 1-based tier number.</param>
        /// <remarks>
        /// A tier past the ladder's end is a bonus tier, so it hands out the bonus rewards
        /// rather than nothing — otherwise every reward past max silently disappears.
        /// </remarks>
        public SeasonRewardData[] GetRewards(int tier)
        {
            var match = GetTier(tier);

            if (match != null) return match.Rewards;

            return tier > MaxTier && _bonusTierXp > 0 ? BonusTierRewards : Array.Empty<SeasonRewardData>();
        }

        /// <summary>
        /// Looks up a whitelisted XP source.
        /// </summary>
        /// <param name="sourceId">The source id supplied by the caller.</param>
        /// <param name="source">The matching source, or null.</param>
        /// <returns><c>true</c> when the source is whitelisted.</returns>
        public bool TryGetXpSource(string sourceId, out SeasonXpSourceData source)
        {
            Prepare();

            if (!string.IsNullOrEmpty(sourceId)) return _sourcesById.TryGetValue(sourceId, out source);

            source = null;
            return false;
        }

        /// <summary>
        /// Looks up a quest by id.
        /// </summary>
        /// <param name="questId">The quest id.</param>
        /// <param name="quest">The matching quest, or null.</param>
        /// <returns><c>true</c> when the quest belongs to this season.</returns>
        public bool TryGetQuest(string questId, out SeasonQuestData quest)
        {
            Prepare();

            if (!string.IsNullOrEmpty(questId)) return _questsById.TryGetValue(questId, out quest);

            quest = null;
            return false;
        }

        /// <summary>
        /// Returns how a track is sold this season, or null when it is not sold at all.
        /// </summary>
        /// <param name="track">The track to look up.</param>
        public SeasonTrackOfferData GetOffer(SeasonTrack track)
        {
            if (_trackOffers == null) return null;

            foreach (var offer in _trackOffers)
            {
                if (offer != null && offer.Track == track) return offer;
            }

            return null;
        }

        /// <summary>
        /// Returns the track sold under a store product id, or null.
        /// </summary>
        /// <param name="productId">The store product id from a purchase or restore.</param>
        public SeasonTrackOfferData GetOfferByProductId(string productId)
        {
            if (_trackOffers == null || string.IsNullOrWhiteSpace(productId)) return null;

            foreach (var offer in _trackOffers)
            {
                if (offer != null && string.Equals(offer.ProductId, productId, StringComparison.Ordinal))
                {
                    return offer;
                }
            }

            return null;
        }

        /// <summary>
        /// Reports authoring mistakes that would misbehave at runtime rather than fail loudly.
        /// </summary>
        /// <returns>A human-readable summary, or an empty string when the season is sound.</returns>
        /// <remarks>
        /// Content arrives as JSON a designer edits, so it is validated rather than trusted.
        /// These are the failures that would otherwise show up as a reward nobody can claim.
        /// </remarks>
        public string DescribeProblems()
        {
            Prepare();

            var problems = new StringBuilder();

            if (string.IsNullOrWhiteSpace(_id)) Append(problems, "season id is blank");
            if (_start == DateTime.MinValue) Append(problems, $"start '{_startUtc}' is not a valid ISO 8601 UTC date");
            if (_end == DateTime.MinValue) Append(problems, $"end '{_endUtc}' is not a valid ISO 8601 UTC date");
            if (_end <= _start && _end != DateTime.MinValue) Append(problems, "the season ends before it starts");
            if (_sortedTiers.Length == 0) Append(problems, "no tiers are defined");
            if (_bonusTierXp > 0 && BonusTierRewards.Length == 0) Append(problems, "bonus tiers are enabled but pay nothing");

            var seenTiers = new HashSet<int>();

            foreach (var tier in _sortedTiers)
            {
                if (!seenTiers.Add(tier.Tier)) Append(problems, $"tier {tier.Tier} is defined more than once");

                var seenRewards = new HashSet<string>();

                foreach (var reward in tier.Rewards)
                {
                    if (reward == null) continue;

                    if (!reward.IsValid) Append(problems, $"tier {tier.Tier} has an incomplete reward");

                    var key = $"{(int)reward.Track}:{reward.RewardId}";

                    if (!seenRewards.Add(key))
                    {
                        // Claims are recorded per reward id, so a duplicate is unclaimable:
                        // collecting one marks both, and the second silently disappears.
                        Append(problems, $"tier {tier.Tier} repeats reward id '{reward.RewardId}' on the same track");
                    }
                }
            }

            return problems.ToString();
        }

        private static void Append(StringBuilder builder, string problem)
        {
            if (builder.Length > 0) builder.Append("; ");

            builder.Append(problem);
        }

        private void Prepare()
        {
            if (_isPrepared) return;

            _isPrepared = true;

            _start = SeasonPassTime.ParseUtc(_startUtc) ?? DateTime.MinValue;
            _end = SeasonPassTime.ParseUtc(_endUtc) ?? DateTime.MinValue;

            // Sorted once here rather than assumed: JSON is hand-edited, and every tier lookup
            // downstream binary-searches on ascending required XP.
            _sortedTiers = _tiers == null
                ? Array.Empty<SeasonTierData>()
                : (SeasonTierData[])_tiers.Clone();

            Array.Sort(_sortedTiers, static (left, right) =>
            {
                var byXp = left.RequiredXp.CompareTo(right.RequiredXp);
                return byXp != 0 ? byXp : left.Tier.CompareTo(right.Tier);
            });

            _sourcesById = new Dictionary<string, SeasonXpSourceData>(StringComparer.Ordinal);

            foreach (var source in XpSources)
            {
                if (source != null && !string.IsNullOrWhiteSpace(source.SourceId))
                {
                    _sourcesById[source.SourceId] = source;
                }
            }

            _questsById = new Dictionary<string, SeasonQuestData>(StringComparer.Ordinal);

            foreach (var quest in Quests)
            {
                if (quest != null && !string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    _questsById[quest.QuestId] = quest;
                }
            }
        }
    }
}
