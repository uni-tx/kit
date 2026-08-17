using System;
using System.Threading;
using NUnit.Framework;

namespace UniTx.DailyRewards.Tests
{
    public sealed class DailyRewardsServiceTests
    {
        private static readonly DateTime Noon = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Initialize_LoadsCalendarAndSave_ClaimsAreAvailable()
        {
            var harness = Harness.Create(Noon);

            Assert.IsTrue(harness.Service.IsReady);
            Assert.IsNotNull(harness.Service.Calendar);
            Assert.AreEqual(CalendarJson.CalendarId, harness.Service.Calendar.Id);
            Assert.AreEqual(DailyRewardsState.Claimable, harness.Service.Snapshot.State);
            Assert.AreEqual(0, harness.Service.Snapshot.Streak);
            Assert.AreEqual(0, harness.Service.Snapshot.CurrentSlotIndex);
            Assert.IsTrue(harness.Service.IsClaimable);
        }

        [Test]
        public void Claim_FirstDay_GrantsSlotZeroAndStreakOne()
        {
            var harness = Harness.Create(Noon);

            var result = harness.Service.ClaimAsync().GetAwaiter().GetResult();

            Assert.AreEqual(DailyClaimResult.Claimed, result);
            Assert.AreEqual(1, harness.Granter.Granted.Count);
            Assert.AreEqual(0, harness.Granter.Granted[0].SlotIndex);
            Assert.AreEqual(1, harness.Granter.Granted[0].Day);
            Assert.AreEqual(1, harness.Service.Snapshot.Streak);
            Assert.AreEqual(DailyRewardsState.Claimed, harness.Service.Snapshot.State);
            Assert.IsFalse(harness.Service.IsClaimable);
        }

        [Test]
        public void Claim_TwiceSameDay_SecondIsAlreadyClaimed()
        {
            var harness = Harness.Create(Noon);

            Assert.AreEqual(DailyClaimResult.Claimed, harness.Service.ClaimAsync().GetAwaiter().GetResult());
            Assert.AreEqual(DailyClaimResult.AlreadyClaimed, harness.Service.ClaimAsync().GetAwaiter().GetResult());

            Assert.AreEqual(1, harness.Granter.Granted.Count);
        }

        [Test]
        public void Claim_NextDay_AdvancesOneSlotAndTheStreak()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ClaimAsync().GetAwaiter().GetResult();

            harness.Clock.Advance(TimeSpan.FromDays(1));

            var result = harness.Service.ClaimAsync().GetAwaiter().GetResult();

            Assert.AreEqual(DailyClaimResult.Claimed, result);
            Assert.AreEqual(1, harness.Granter.Granted[^1].SlotIndex);
            Assert.AreEqual(2, harness.Granter.Granted[^1].Day);
            Assert.AreEqual(2, harness.Service.Snapshot.Streak);
        }

        [Test]
        public void Claim_AfterMissedDays_CalendarMode_SkipsAheadAndResetsStreak()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ClaimAsync().GetAwaiter().GetResult();

            // Three days pass without logging in.
            harness.Clock.Advance(TimeSpan.FromDays(3));

            var result = harness.Service.ClaimAsync().GetAwaiter().GetResult();

            Assert.AreEqual(DailyClaimResult.Claimed, result);
            Assert.AreEqual(3, harness.Granter.Granted[^1].SlotIndex);
            Assert.AreEqual(4, harness.Granter.Granted[^1].Day);
            Assert.AreEqual(1, harness.Service.Snapshot.Streak);
        }

        [Test]
        public void Claim_AfterMissedDays_StreakMode_ResetsToDayOne()
        {
            var calendar = CalendarJson.Standard(days: 7, mode: (int)DailyRewardsMode.Streak);
            var harness = Harness.Create(Noon, calendar);

            harness.Service.ClaimAsync().GetAwaiter().GetResult();

            harness.Clock.Advance(TimeSpan.FromDays(3));

            var result = harness.Service.ClaimAsync().GetAwaiter().GetResult();

            Assert.AreEqual(DailyClaimResult.Claimed, result);
            Assert.AreEqual(0, harness.Granter.Granted[^1].SlotIndex);
            Assert.AreEqual(1, harness.Granter.Granted[^1].Day);
            Assert.AreEqual(1, harness.Service.Snapshot.Streak);
        }

        [Test]
        public void Claim_GranterRefuses_ThenTheSameSlotIsRetried()
        {
            var harness = Harness.Create(Noon);
            harness.Granter.ShouldFail = true;

            var first = harness.Service.ClaimAsync().GetAwaiter().GetResult();

            Assert.AreEqual(DailyClaimResult.GrantFailed, first);
            Assert.AreEqual(0, harness.Granter.Granted.Count);
            Assert.AreEqual(DailyRewardsState.Claimable, harness.Service.Snapshot.State);

            // The failure was recorded for retry, and the retry delivers the same slot.
            harness.Granter.ShouldFail = false;

            var second = harness.Service.ClaimAsync().GetAwaiter().GetResult();

            Assert.AreEqual(DailyClaimResult.Claimed, second);
            Assert.AreEqual(0, harness.Granter.Granted[0].SlotIndex);
            Assert.AreEqual(DailyRewardsState.Claimed, harness.Service.Snapshot.State);
        }

        [Test]
        public void Refresh_RetriesAFailedClaimFromThePreviousSession()
        {
            var harness = Harness.Create(Noon);
            harness.Granter.ShouldFail = true;

            Assert.AreEqual(DailyClaimResult.GrantFailed,
                harness.Service.ClaimAsync().GetAwaiter().GetResult());

            // The player closes the app. Next launch: same day, granter healthy again.
            harness.Granter.ShouldFail = false;

            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, harness.Granter.Granted.Count);
            Assert.AreEqual(DailyRewardsState.Claimed, harness.Service.Snapshot.State);
        }

        [Test]
        public void LoopedCalendar_AfterDaySeven_WrapsAndKeepsTheStreak()
        {
            var harness = Harness.Create(Noon);

            for (var day = 1; day <= 7; day++)
            {
                Assert.AreEqual(DailyClaimResult.Claimed,
                    harness.Service.ClaimAsync().GetAwaiter().GetResult());

                harness.Clock.Advance(TimeSpan.FromDays(1));
            }

            // Day 7 claimed slot 6.
            Assert.AreEqual(6, harness.Granter.Granted[6].SlotIndex);

            // Next day wraps back to slot 0; the streak keeps climbing.
            var result = harness.Service.ClaimAsync().GetAwaiter().GetResult();

            Assert.AreEqual(DailyClaimResult.Claimed, result);
            Assert.AreEqual(0, harness.Granter.Granted[7].SlotIndex);
            Assert.AreEqual(8, harness.Service.Snapshot.Streak);
        }

        [Test]
        public void FiniteCalendar_FinishesAfterTheLastSlot()
        {
            var calendar = CalendarJson.Standard(days: 3, loop: false);
            var harness = Harness.Create(Noon, calendar);

            for (var day = 1; day <= 3; day++)
            {
                Assert.AreEqual(DailyClaimResult.Claimed,
                    harness.Service.ClaimAsync().GetAwaiter().GetResult());

                if (day < 3) harness.Clock.Advance(TimeSpan.FromDays(1));
            }

            // All three slots claimed, the clock still on the last day: claimed, not finished.
            Assert.AreEqual(DailyRewardsState.Claimed, harness.Service.Snapshot.State);

            harness.Clock.Advance(TimeSpan.FromDays(1));

            var result = harness.Service.ClaimAsync().GetAwaiter().GetResult();

            Assert.AreEqual(DailyClaimResult.Finished, result);
            Assert.AreEqual(DailyRewardsState.Finished, harness.Service.Snapshot.State);
            Assert.IsFalse(harness.Service.IsClaimable);
        }

        [Test]
        public void ResetHour_ShiftsWhichCalendarDayAClaimBelongsTo()
        {
            var config = ConfigFactory.Create(resetHourUtc: 9);
            var harness = Harness.Create(new DateTime(2026, 6, 15, 5, 0, 0, DateTimeKind.Utc),
                CalendarJson.Standard(), config);

            // 05:00 UTC is before the 09:00 reset, so this claim belongs to June 14's day.
            Assert.AreEqual(DailyClaimResult.Claimed, harness.Service.ClaimAsync().GetAwaiter().GetResult());

            // Still before the reset: the same calendar day, so nothing new to claim.
            harness.Clock.UtcNow = new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(DailyClaimResult.AlreadyClaimed,
                harness.Service.ClaimAsync().GetAwaiter().GetResult());

            // Past the reset: a new calendar day, and the calendar advances.
            harness.Clock.UtcNow = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(DailyClaimResult.Claimed, harness.Service.ClaimAsync().GetAwaiter().GetResult());
            Assert.AreEqual(1, harness.Granter.Granted[^1].SlotIndex);
        }

        [Test]
        public void ClockWoundBack_CannotReclaimADay()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ClaimAsync().GetAwaiter().GetResult();

            // The device clock is wound back a day; the high-water mark keeps time forward.
            harness.Clock.UtcNow = Noon.AddDays(-1);

            Assert.AreEqual(DailyClaimResult.AlreadyClaimed,
                harness.Service.ClaimAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void CalendarReplacement_ResetsPosition_KeepsHistory()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ClaimAsync().GetAwaiter().GetResult();
            Assert.AreEqual(1, harness.Service.Snapshot.Streak);

            // A new calendar ships under a different id.
            harness.Content.Add(CalendarJson.Standard(days: 5, id: "daily_v2"));
            harness.Content.Remove(CalendarJson.CalendarId);

            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual("daily_v2", harness.Service.Calendar.Id);
            Assert.AreEqual(0, harness.Service.Snapshot.CurrentSlotIndex);
            Assert.AreEqual(0, harness.Service.Snapshot.Streak);
            Assert.AreEqual(1, harness.Service.SavedData.History.Count);
        }

        [Test]
        public void GrantId_IsScopedToTheCalendarDay()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ClaimAsync().GetAwaiter().GetResult();

            Assert.IsNotEmpty(harness.Granter.GrantIds);
            StringAssert.StartsWith($"daily:{CalendarJson.CalendarId}:", harness.Granter.GrantIds[0]);
            Assert.IsTrue(harness.Service.SavedData.HasAppliedGrant(harness.Granter.GrantIds[0]));
        }

        [Test]
        public void Snapshot_CountsDownToTheNextClaim()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ClaimAsync().GetAwaiter().GetResult();

            var snapshot = harness.Service.Snapshot;

            Assert.AreEqual(DailyRewardsState.Claimed, snapshot.State);
            Assert.Greater(snapshot.RemainingSeconds, 0);
            Assert.LessOrEqual(snapshot.RemainingSeconds, DailyRewardsTime.SecondsPerDay);
            Assert.AreEqual(0, snapshot.CurrentSlotIndex); // day one claimed, slot zero highlighted
        }

        /// <summary>
        /// A fully wired service over in-memory dependencies.
        /// </summary>
        private sealed class Harness
        {
            public FakeClock Clock;
            public FakeContentService Content;
            public FakeSerialisationService Serialisation;
            public FakeBackend Backend;
            public RecordingGranter Granter;
            public DailyRewardsService Service;

            public static Harness Create(DateTime now, DailyRewardsData calendar = null,
                UniDailyRewardsConfig config = null)
            {
                var harness = new Harness
                {
                    Clock = new FakeClock(now),
                    Content = new FakeContentService(),
                    Serialisation = new FakeSerialisationService(),
                };

                harness.Content.Add(calendar ?? CalendarJson.Standard());
                harness.Backend = new FakeBackend(harness.Serialisation);
                harness.Granter = new RecordingGranter();
                harness.Service = new DailyRewardsService(harness.Clock, harness.Content,
                    harness.Backend, config ?? ConfigFactory.Create());
                harness.Service.SetRewardGranter(harness.Granter);

                harness.Service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

                return harness;
            }
        }
    }
}
