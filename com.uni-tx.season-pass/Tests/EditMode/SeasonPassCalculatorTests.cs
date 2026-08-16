using System.Collections.Generic;
using NUnit.Framework;

namespace UniTx.SeasonPass.Tests
{
    /// <summary>
    /// Tier maths, exercised at the boundaries where an off-by-one costs a player a reward.
    /// </summary>
    [TestFixture]
    public sealed class SeasonPassCalculatorTests
    {
        [Test]
        public void GetTier_BelowFirstThreshold_IsZero()
        {
            var season = SeasonJson.Standard();

            Assert.That(SeasonPassCalculator.GetTier(season, 99), Is.Zero);
        }

        [Test]
        public void GetTier_ExactlyOnThreshold_CountsAsReached()
        {
            var season = SeasonJson.Standard();

            // The threshold is "at least this much", not "more than". A player sitting exactly
            // on 100 XP has earned tier 1 and would otherwise be told to grind one more point.
            Assert.That(SeasonPassCalculator.GetTier(season, 100), Is.EqualTo(1));
            Assert.That(SeasonPassCalculator.GetTier(season, 200), Is.EqualTo(2));
            Assert.That(SeasonPassCalculator.GetTier(season, 300), Is.EqualTo(3));
        }

        [Test]
        public void GetTier_PastFinalTierWithoutBonusTiers_StaysAtMax()
        {
            var season = SeasonJson.Standard();

            Assert.That(SeasonPassCalculator.GetTier(season, 10_000), Is.EqualTo(3));
        }

        [Test]
        public void GetTier_UnsortedTierDataInJson_StillResolves()
        {
            // The fixture authors tiers as 2, 1, 3 on purpose: hand-edited content is not
            // ordered, and every lookup downstream binary-searches.
            var season = SeasonJson.Standard();

            Assert.That(SeasonPassCalculator.GetTier(season, 250), Is.EqualTo(2));
        }

        [Test]
        public void GetTier_NegativeXp_ClampsToZero()
        {
            var season = SeasonJson.Standard();

            Assert.That(SeasonPassCalculator.GetTier(season, -500), Is.Zero);
        }

        [Test]
        public void GetProgress_MidTier_ReportsFractionOfThatTierOnly()
        {
            var season = SeasonJson.Standard();

            var progress = SeasonPassCalculator.GetProgress(season, 150);

            Assert.That(progress.Tier, Is.EqualTo(1));
            Assert.That(progress.XpIntoTier, Is.EqualTo(50));
            Assert.That(progress.XpPerTier, Is.EqualTo(100));
            Assert.That(progress.XpToNextTier, Is.EqualTo(50));
            Assert.That(progress.Normalized, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void GetProgress_BelowFirstTier_FillsTowardsTierOne()
        {
            var season = SeasonJson.Standard();

            var progress = SeasonPassCalculator.GetProgress(season, 40);

            Assert.That(progress.Tier, Is.Zero);
            Assert.That(progress.XpIntoTier, Is.EqualTo(40));
            Assert.That(progress.XpPerTier, Is.EqualTo(100));
        }

        [Test]
        public void GetProgress_AtMaxWithoutBonusTiers_ReportsFullAndStops()
        {
            var season = SeasonJson.Standard();

            var progress = SeasonPassCalculator.GetProgress(season, 500);

            Assert.That(progress.IsMaxTier, Is.True);
            Assert.That(progress.XpToNextTier, Is.Zero);
            Assert.That(progress.Normalized, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void GetProgress_PastMaxWithBonusTiers_KeepsCounting()
        {
            var season = SeasonJson.Standard(bonusTierXp: 50);

            var progress = SeasonPassCalculator.GetProgress(season, 425);

            Assert.That(progress.BonusTiers, Is.EqualTo(2));
            Assert.That(progress.Tier, Is.EqualTo(5));
            Assert.That(progress.XpIntoTier, Is.EqualTo(25));
            Assert.That(progress.XpPerTier, Is.EqualTo(50));
            Assert.That(progress.IsMaxTier, Is.True);
        }

        [Test]
        public void GetRequiredXp_BonusTier_ExtrapolatesPastTheLadder()
        {
            var season = SeasonJson.Standard(bonusTierXp: 50);

            Assert.That(SeasonPassCalculator.GetRequiredXp(season, 3), Is.EqualTo(300));
            Assert.That(SeasonPassCalculator.GetRequiredXp(season, 5), Is.EqualTo(400));
        }

        [Test]
        public void GetProgress_SeasonWithNoTiers_DoesNotThrow()
        {
            var season = SeasonJson.Parse(@"{ ""_id"": ""empty"", ""_tiers"": [] }");

            var progress = SeasonPassCalculator.GetProgress(season, 500);

            Assert.That(progress.Tier, Is.Zero);
            Assert.That(progress.IsMaxTier, Is.True);
        }

        [Test]
        public void GetProgress_NullSeason_ReturnsEmptyProgress()
        {
            var progress = SeasonPassCalculator.GetProgress(null, 500);

            Assert.That(progress.Tier, Is.Zero);
            Assert.That(progress.TotalXp, Is.EqualTo(500));
        }

        [Test]
        public void GetProgress_SingleTierSeason_ReachesMaxImmediately()
        {
            var season = SeasonJson.Parse(@"{
              ""_id"": ""single"",
              ""_tiers"": [ { ""_tier"": 1, ""_requiredXp"": 10, ""_rewards"": [] } ]
            }");

            Assert.That(SeasonPassCalculator.GetProgress(season, 10).IsMaxTier, Is.True);
            Assert.That(SeasonPassCalculator.GetProgress(season, 9).Tier, Is.Zero);
        }

        [Test]
        public void CollectUnlockedRewards_ReturnsEveryTrackUpToTheTier()
        {
            var season = SeasonJson.Standard();
            var buffer = new List<SeasonRewardRef>();

            var count = SeasonPassCalculator.CollectUnlockedRewards(season, 2, buffer);

            // Two tiers, two tracks each. Ownership filtering happens in the service, so the
            // premium slots are listed here whether or not the player can claim them.
            Assert.That(count, Is.EqualTo(4));
            Assert.That(buffer, Has.Exactly(1).Matches<SeasonRewardRef>(r => r.RewardId == "p2"));
            Assert.That(buffer, Has.None.Matches<SeasonRewardRef>(r => r.Tier == 3));
        }

        [Test]
        public void CollectUnlockedRewards_BonusTiers_RepeatTheBonusRewardPerTier()
        {
            var season = SeasonJson.Standard(bonusTierXp: 50);
            var buffer = new List<SeasonRewardRef>();

            SeasonPassCalculator.CollectUnlockedRewards(season, 5, buffer);

            Assert.That(buffer.FindAll(r => r.RewardId == "bonus_coins"), Has.Count.EqualTo(2));
            Assert.That(buffer, Has.Exactly(1).Matches<SeasonRewardRef>(r => r.Tier == 4));
            Assert.That(buffer, Has.Exactly(1).Matches<SeasonRewardRef>(r => r.Tier == 5));
        }

        [Test]
        public void CollectUnlockedRewards_ClearsTheBufferFirst()
        {
            var season = SeasonJson.Standard();
            var buffer = new List<SeasonRewardRef> { new("stale", 99, SeasonTrack.Free, "stale") };

            SeasonPassCalculator.CollectUnlockedRewards(season, 1, buffer);

            Assert.That(buffer, Has.None.Matches<SeasonRewardRef>(r => r.RewardId == "stale"));
        }

        [Test]
        public void ClaimKey_RoundTrips()
        {
            var reference = new SeasonRewardRef("s1", 7, SeasonTrack.PremiumPlus, "reward:with:colons");

            Assert.That(SeasonRewardRef.TryParseClaimKey("s1", reference.ToClaimKey(), out var parsed),
                Is.True);
            Assert.That(parsed, Is.EqualTo(reference));
        }

        [Test]
        public void ClaimKey_Malformed_IsRejected()
        {
            Assert.That(SeasonRewardRef.TryParseClaimKey("s1", "nonsense", out _), Is.False);
            Assert.That(SeasonRewardRef.TryParseClaimKey("s1", "t1:0:", out _), Is.False);
            Assert.That(SeasonRewardRef.TryParseClaimKey("s1", string.Empty, out _), Is.False);
        }

        [Test]
        public void StartOfUtcWeek_LandsOnMonday()
        {
            // 2026-06-17 is a Wednesday; the week it belongs to starts Monday the 15th.
            var wednesday = new System.DateTime(2026, 6, 17, 13, 45, 0, System.DateTimeKind.Utc);
            var weekStart = SeasonPassTime.StartOfUtcWeek(
                new System.DateTimeOffset(wednesday).ToUnixTimeSeconds());

            var asDate = SeasonPassTime.FromUnix(weekStart);

            Assert.That(asDate.DayOfWeek, Is.EqualTo(System.DayOfWeek.Monday));
            Assert.That(asDate.Day, Is.EqualTo(15));
            Assert.That(asDate.TimeOfDay, Is.EqualTo(System.TimeSpan.Zero));
        }

        [Test]
        public void StartOfUtcWeek_OnSunday_UsesThePrecedingMonday()
        {
            // Sunday is the trap: DayOfWeek numbers it 0, so a naive subtraction jumps forward
            // a week instead of back six days.
            var sunday = new System.DateTime(2026, 6, 21, 23, 59, 0, System.DateTimeKind.Utc);
            var weekStart = SeasonPassTime.StartOfUtcWeek(
                new System.DateTimeOffset(sunday).ToUnixTimeSeconds());

            Assert.That(SeasonPassTime.FromUnix(weekStart).Day, Is.EqualTo(15));
        }
    }
}
