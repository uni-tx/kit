using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniTx.SeasonPass.Tests
{
    /// <summary>
    /// The rules that decide whether a player keeps what they earned.
    /// </summary>
    /// <remarks>
    /// Every operation here completes synchronously — fakes for the clock, the store and the
    /// granter — so the UniTasks can be driven straight from EditMode without a PlayMode
    /// round trip.
    /// </remarks>
    [TestFixture]
    public sealed class SeasonPassServiceTests
    {
        private const string SecondSeasonId = "season_test_2";

        private static readonly DateTime MidSeason = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        private FakeClock _clock;
        private FakeContentService _content;
        private FakeSerialisationService _serialisation;
        private FakeBackend _backend;
        private RecordingGranter _granter;
        private FakeWallet _wallet;
        private UniSeasonPassConfig _config;
        private SeasonPassService _service;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock(MidSeason);
            _content = new FakeContentService();
            _serialisation = new FakeSerialisationService();
            _backend = new FakeBackend(_serialisation);
            _granter = new RecordingGranter();
            _wallet = new FakeWallet();
            _config = BuildConfig();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Reset();

            LogAssert.ignoreFailingMessages = false;

            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Swaps the config before the service is started, without leaking the previous asset.
        /// </summary>
        private void UseConfig(SeasonExpiryPolicy policy, bool autoClaim = false,
            bool allowOfflineGrants = true)
        {
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);

            _config = BuildConfig(policy, autoClaim, allowOfflineGrants);
        }

        // ── Selection and phases ────────────────────────────────────────────────────────

        [Test]
        public void Initialize_WithinTheSeasonWindow_StartsActiveAtTierZero()
        {
            Start();

            Assert.That(_service.Phase, Is.EqualTo(SeasonPhase.Active));
            Assert.That(_service.Snapshot.Progress.Tier, Is.Zero);
            Assert.That(_service.Snapshot.SeasonId, Is.EqualTo(SeasonJson.SeasonId));
        }

        [Test]
        public void Initialize_BeforeTheStartDate_ReportsNotStartedAndRefusesXp()
        {
            _clock.UtcNow = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

            Start();

            Assert.That(_service.Phase, Is.EqualTo(SeasonPhase.NotStarted));
            Assert.That(Grant("match_complete"), Is.EqualTo(XpGrantResult.SeasonInactive));
        }

        [Test]
        public void Initialize_InsideTheFinalStretch_ReportsEndingSoon()
        {
            _clock.UtcNow = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);

            Start();

            Assert.That(_service.Phase, Is.EqualTo(SeasonPhase.EndingSoon));

            // Earning has not stopped yet — the phase exists to warn, not to close the door.
            Assert.That(Grant("match_complete"), Is.EqualTo(XpGrantResult.Granted));
        }

        // ── Earning ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GrantXp_FromAWhitelistedSource_AddsTheSourcesConfiguredAmount()
        {
            Start();

            Assert.That(Grant("match_complete"), Is.EqualTo(XpGrantResult.Granted));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(50));
        }

        [Test]
        public void GrantXp_FromAnUnlistedSource_IsRefused()
        {
            Start();

            Assert.That(Grant("free_xp_please"), Is.EqualTo(XpGrantResult.UnknownSource));
            Assert.That(_service.SavedData.TotalXp, Is.Zero);
        }

        [Test]
        public void GrantXp_FromAPremiumOnlySource_NeedsThePaidTrack()
        {
            Start();

            Assert.That(Grant("premium_bonus"), Is.EqualTo(XpGrantResult.TrackNotOwned));

            Unlock(SeasonTrack.Premium, SeasonPassPayment.External);

            Assert.That(Grant("premium_bonus"), Is.EqualTo(XpGrantResult.Granted));
        }

        [Test]
        public void GrantXp_CrossingAThreshold_RaisesTheTier()
        {
            Start();

            Grant("match_complete", 250);

            Assert.That(_service.Snapshot.Progress.Tier, Is.EqualTo(2));
        }

        [Test]
        public void GrantXp_BeyondTheDailyCap_IsTrimmedThenRefused()
        {
            Start(SeasonJson.Standard(dailyCap: 120));

            Assert.That(Grant("match_complete", 100), Is.EqualTo(XpGrantResult.Granted));

            // 20 of the requested 100 fit under the cap, so the grant is partial rather than
            // silently full — a caller that cares can tell the difference.
            Assert.That(Grant("match_complete", 100), Is.EqualTo(XpGrantResult.Capped));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(120));

            Assert.That(Grant("match_complete", 100), Is.EqualTo(XpGrantResult.Capped));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(120));
        }

        [Test]
        public void GrantXp_AfterUtcMidnight_RefillsTheDailyCap()
        {
            Start(SeasonJson.Standard(dailyCap: 100));

            Grant("match_complete", 100);
            _clock.Advance(TimeSpan.FromDays(1));

            Assert.That(Grant("match_complete", 100), Is.EqualTo(XpGrantResult.Granted));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(200));
        }

        [Test]
        public void GrantXp_WithTheClockWoundBack_DoesNotRefillTheDailyCap()
        {
            Start(SeasonJson.Standard(dailyCap: 100));

            Grant("match_complete", 100);

            // The classic cheat: set the device back a day to farm the cap again. The save
            // keeps a high-water mark, so the window cannot reopen.
            _clock.Advance(TimeSpan.FromDays(-2));

            Assert.That(Grant("match_complete", 100), Is.EqualTo(XpGrantResult.Capped));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(100));
        }

        [Test]
        public void GrantXp_WithARepeatedGrantId_AppliesOnce()
        {
            Start();

            Assert.That(Grant("match_complete", 50, "match-42"), Is.EqualTo(XpGrantResult.Granted));
            Assert.That(Grant("match_complete", 50, "match-42"), Is.EqualTo(XpGrantResult.Duplicate));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(50));
        }

        // ── Claiming ────────────────────────────────────────────────────────────────────

        [Test]
        public void Claim_BeforeReachingTheTier_IsRefused()
        {
            Start();

            Assert.That(Claim(1, SeasonTrack.Free, "f1"), Is.EqualTo(ClaimResult.TierNotReached));
        }

        [Test]
        public void Claim_OnAnUnownedTrack_IsRefused()
        {
            Start();
            Grant("match_complete", 100);

            Assert.That(Claim(1, SeasonTrack.Premium, "p1"), Is.EqualTo(ClaimResult.TrackNotOwned));
            Assert.That(_granter.Granted, Is.Empty);
        }

        [Test]
        public void Claim_Twice_IsRefusedTheSecondTime()
        {
            Start();
            Grant("match_complete", 100);

            Assert.That(Claim(1, SeasonTrack.Free, "f1"), Is.EqualTo(ClaimResult.Claimed));
            Assert.That(Claim(1, SeasonTrack.Free, "f1"), Is.EqualTo(ClaimResult.AlreadyClaimed));
            Assert.That(_granter.CountFor(1, SeasonTrack.Free), Is.EqualTo(1));
        }

        [Test]
        public void Claim_WhenTheGranterRefuses_LeavesTheRewardClaimable()
        {
            Start();
            Grant("match_complete", 100);

            _granter.ShouldFail = true;

            Assert.That(Claim(1, SeasonTrack.Free, "f1"), Is.EqualTo(ClaimResult.GrantFailed));

            // The failure mode that matters: the reward must not be marked collected when it
            // never reached the player.
            Assert.That(_service.SavedData.HasClaimed(Key(1, SeasonTrack.Free, "f1")), Is.False);
            Assert.That(_service.SavedData.PendingClaimKeys, Has.Count.EqualTo(1));
            Assert.That(_service.IsClaimable(Ref(1, SeasonTrack.Free, "f1")), Is.True);
        }

        [Test]
        public void Claim_WhenTheGranterThrows_IsTreatedAsAFailureNotACollection()
        {
            Start();
            Grant("match_complete", 100);

            _granter.ShouldThrow = true;

            LogAssert.ignoreFailingMessages = true;

            Assert.That(Claim(1, SeasonTrack.Free, "f1"), Is.EqualTo(ClaimResult.GrantFailed));
            Assert.That(_service.SavedData.HasClaimed(Key(1, SeasonTrack.Free, "f1")), Is.False);
        }

        [Test]
        public void Refresh_AfterTheGranterRecovers_DeliversTheQueuedClaim()
        {
            Start();
            Grant("match_complete", 100);

            _granter.ShouldFail = true;
            Claim(1, SeasonTrack.Free, "f1");

            _granter.ShouldFail = false;
            Run(_service.RefreshAsync(CancellationToken.None));

            Assert.That(_service.SavedData.HasClaimed(Key(1, SeasonTrack.Free, "f1")), Is.True);
            Assert.That(_service.SavedData.PendingClaimKeys, Is.Empty);
        }

        [Test]
        public void ClaimAll_CollectsEveryUnlockedOwnedReward()
        {
            Start();
            Grant("match_complete", 200);

            Assert.That(RunFor(_service.ClaimAllAsync(CancellationToken.None)), Is.EqualTo(2));
            Assert.That(_granter.Granted, Has.Count.EqualTo(2));
            Assert.That(_granter.Granted, Has.None.Matches<SeasonRewardRef>(
                r => r.Track == SeasonTrack.Premium));
        }

        // ── Buying ──────────────────────────────────────────────────────────────────────

        [Test]
        public void UnlockTrack_WithCurrency_ChargesTheWalletAndBackGrantsPassedTiers()
        {
            Start();
            _wallet.SetBalance("gems", 500);
            Grant("match_complete", 200);

            Assert.That(Unlock(SeasonTrack.Premium), Is.EqualTo(TrackUnlockResult.Unlocked));
            Assert.That(_wallet.GetBalance("gems"), Is.Zero);

            // Paying mid-season has to pay out what was already earned, or the player buys
            // access to rewards they can no longer reach.
            Assert.That(_granter.CountFor(1, SeasonTrack.Premium), Is.EqualTo(1));
            Assert.That(_granter.CountFor(2, SeasonTrack.Premium), Is.EqualTo(1));
        }

        [Test]
        public void UnlockTrack_DoesNotSpendUnclaimedFreeRewardsOnThePlayersBehalf()
        {
            Start();
            _wallet.SetBalance("gems", 500);
            Grant("match_complete", 200);

            Unlock(SeasonTrack.Premium);

            Assert.That(_granter.Granted, Has.None.Matches<SeasonRewardRef>(
                r => r.Track == SeasonTrack.Free));
            Assert.That(_service.IsClaimable(Ref(1, SeasonTrack.Free, "f1")), Is.True);
        }

        [Test]
        public void UnlockTrack_WithoutEnoughCurrency_ChangesNothing()
        {
            Start();
            _wallet.SetBalance("gems", 100);

            Assert.That(Unlock(SeasonTrack.Premium), Is.EqualTo(TrackUnlockResult.InsufficientFunds));
            Assert.That(_service.OwnsTrack(SeasonTrack.Premium), Is.False);
            Assert.That(_wallet.GetBalance("gems"), Is.EqualTo(100));
        }

        [Test]
        public void UnlockTrack_PaidElsewhere_DoesNotTouchTheWallet()
        {
            Start();
            _wallet.SetBalance("gems", 500);

            Assert.That(Unlock(SeasonTrack.Premium, SeasonPassPayment.External),
                Is.EqualTo(TrackUnlockResult.Unlocked));
            Assert.That(_wallet.GetBalance("gems"), Is.EqualTo(500));
        }

        [Test]
        public void UnlockTrack_Twice_IsIdempotentAndFree()
        {
            Start();
            _wallet.SetBalance("gems", 1000);

            Unlock(SeasonTrack.Premium);

            // A restore re-delivers the same entitlement on every launch, so a second unlock
            // must not charge again.
            Assert.That(Unlock(SeasonTrack.Premium), Is.EqualTo(TrackUnlockResult.AlreadyOwned));
            Assert.That(_wallet.GetBalance("gems"), Is.EqualTo(500));
        }

        [Test]
        public void BuyTierSkips_AdvanceOneTierEach()
        {
            Start();
            _wallet.SetBalance("gems", 1000);

            Assert.That(RunFor(_service.BuyTierSkipsAsync(2, SeasonPassPayment.Currency,
                CancellationToken.None)), Is.EqualTo(2));

            Assert.That(_service.Snapshot.Progress.Tier, Is.EqualTo(2));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(200));
            Assert.That(_wallet.GetBalance("gems"), Is.EqualTo(800));
        }

        [Test]
        public void BuyTierSkips_PastTheFinalTier_AreBankedRatherThanLost()
        {
            Start();
            _wallet.SetBalance("gems", 1000);
            Grant("match_complete", 300);

            RunFor(_service.BuyTierSkipsAsync(2, SeasonPassPayment.Currency, CancellationToken.None));

            Assert.That(_service.SavedData.BankedTierSkips, Is.EqualTo(2));
        }

        [Test]
        public void BuyTierSkips_BeyondTheSeasonLimit_AreTrimmed()
        {
            Start(SeasonJson.Standard(maxTierSkips: 1));
            _wallet.SetBalance("gems", 1000);

            Assert.That(RunFor(_service.BuyTierSkipsAsync(5, SeasonPassPayment.Currency,
                CancellationToken.None)), Is.EqualTo(1));
            Assert.That(RunFor(_service.BuyTierSkipsAsync(1, SeasonPassPayment.Currency,
                CancellationToken.None)), Is.Zero);
        }

        // ── Rollover ────────────────────────────────────────────────────────────────────

        [Test]
        public void Rollover_ResetsEphemeralStateAndArchivesTheOutgoingSeason()
        {
            Start();
            _wallet.SetBalance("gems", 500);
            Grant("match_complete", 200);
            Unlock(SeasonTrack.Premium);
            Claim(1, SeasonTrack.Free, "f1");

            AdvanceToSecondSeason();

            var saved = _service.SavedData;

            Assert.That(saved.SeasonId, Is.EqualTo(SecondSeasonId));
            Assert.That(saved.TotalXp, Is.Zero);
            Assert.That(saved.Owns(SeasonTrack.Premium), Is.False);
            Assert.That(saved.ClaimedKeys, Is.Empty);

            Assert.That(saved.Archive, Has.Count.EqualTo(1));
            Assert.That(saved.Archive[0].SeasonId, Is.EqualTo(SeasonJson.SeasonId));
            Assert.That(saved.Archive[0].FinalTier, Is.EqualTo(2));
        }

        [Test]
        public void Rollover_UnderAutoGrant_PaysOutWhatWasEarnedButNeverCollected()
        {
            Start();
            Grant("match_complete", 200);

            AdvanceToSecondSeason();

            // Two free rewards were unlocked and never tapped. The forgiving policy delivers
            // them rather than letting a forgotten tap cost the player the season.
            Assert.That(_granter.CountFor(1, SeasonTrack.Free), Is.EqualTo(1));
            Assert.That(_granter.CountFor(2, SeasonTrack.Free), Is.EqualTo(1));
        }

        [Test]
        public void Rollover_UnderForfeit_LosesUnclaimedRewardsAndRecordsHowMany()
        {
            UseConfig(SeasonExpiryPolicy.Forfeit);
            Start();
            Grant("match_complete", 200);

            AdvanceToSecondSeason();

            Assert.That(_granter.Granted, Is.Empty);
            Assert.That(_service.SavedData.Archive[0].ForfeitedCount, Is.EqualTo(2));
        }

        [Test]
        public void Rollover_AppliesTierSkipsBankedLastSeason()
        {
            Start();
            _wallet.SetBalance("gems", 1000);
            Grant("match_complete", 300);
            RunFor(_service.BuyTierSkipsAsync(2, SeasonPassPayment.Currency, CancellationToken.None));

            AdvanceToSecondSeason();

            // Banked skips were paid for, so they survive the reset that wipes XP.
            Assert.That(_service.SavedData.BankedTierSkips, Is.Zero);
            Assert.That(_service.Snapshot.Progress.Tier, Is.EqualTo(2));
        }

        [Test]
        public void Expiry_UnderGraceWindow_KeepsClaimingOpenAfterTheEndDateThenCloses()
        {
            UseConfig(SeasonExpiryPolicy.GraceWindow);
            Start();
            Grant("match_complete", 100);

            _clock.UtcNow = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
            Run(_service.RefreshAsync(CancellationToken.None));

            Assert.That(_service.Phase, Is.EqualTo(SeasonPhase.Grace));
            Assert.That(Claim(1, SeasonTrack.Free, "f1"), Is.EqualTo(ClaimResult.Claimed));

            // Earning is closed even while claiming is open — the two windows are not the same.
            Assert.That(Grant("match_complete"), Is.EqualTo(XpGrantResult.SeasonInactive));

            _clock.UtcNow = new DateTime(2026, 7, 4, 0, 0, 0, DateTimeKind.Utc);
            Run(_service.RefreshAsync(CancellationToken.None));

            Assert.That(_service.Phase, Is.EqualTo(SeasonPhase.Ended));
            Assert.That(Claim(2, SeasonTrack.Free, "f2"), Is.EqualTo(ClaimResult.SeasonExpired));
        }

        // ── Quests ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Quest_PaysItsXpOnceOnCompletion()
        {
            Start();

            Assert.That(Report("daily_win"), Is.EqualTo(QuestProgressResult.Advanced));
            Assert.That(_service.SavedData.TotalXp, Is.Zero);

            Assert.That(Report("daily_win"), Is.EqualTo(QuestProgressResult.Completed));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(60));

            Assert.That(Report("daily_win"), Is.EqualTo(QuestProgressResult.AlreadyComplete));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(60));
        }

        [Test]
        public void Quest_UnknownId_IsRejected()
        {
            Start();

            Assert.That(Report("no_such_quest"), Is.EqualTo(QuestProgressResult.UnknownQuest));
        }

        [Test]
        public void Quest_Daily_ResetsAtUtcMidnightWhileWeeklyKeepsRunning()
        {
            Start();

            Report("daily_win", 2);
            Report("weekly_grind", 3);

            _clock.Advance(TimeSpan.FromDays(1));
            Run(_service.RefreshAsync(CancellationToken.None));

            Assert.That(Report("daily_win"), Is.EqualTo(QuestProgressResult.Advanced));
            Assert.That(FindQuest("weekly_grind").Amount, Is.EqualTo(3));
        }

        // ── Regressions ─────────────────────────────────────────────────────────────────

        [Test]
        public void Refresh_WithOnlyAnUpcomingSeason_DoesNotRollOverBeforeItStarts()
        {
            Start();
            Grant("match_complete", 200);
            Claim(1, SeasonTrack.Free, "f1");

            // The live season leaves content — a mis-tagged Addressables group is enough — and
            // only a teaser for next season remains. Rolling over here would wipe the player's
            // standing weeks before the season it belongs to has begun.
            _content.Remove(SeasonJson.SeasonId);
            _content.Add(SeasonJson.Standard(SecondSeasonId, "2026-12-01T00:00:00Z",
                "2027-01-01T00:00:00Z"));

            Run(_service.RefreshAsync(CancellationToken.None));

            Assert.That(_service.Phase, Is.EqualTo(SeasonPhase.NotStarted));
            Assert.That(_service.SavedData.SeasonId, Is.EqualTo(SeasonJson.SeasonId));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(200));
            Assert.That(_service.SavedData.Archive, Is.Empty);
        }

        [Test]
        public void Snapshot_WhileShowingAnUnstartedSeason_ReportsNoProgressFromTheOldSave()
        {
            Start();
            Grant("match_complete", 200);

            _content.Remove(SeasonJson.SeasonId);
            _content.Add(SeasonJson.Standard(SecondSeasonId, "2026-12-01T00:00:00Z",
                "2027-01-01T00:00:00Z"));

            Run(_service.RefreshAsync(CancellationToken.None));

            // Last season's XP read against next season's ladder would show a tier the player
            // has not earned, on a season that has not started.
            Assert.That(_service.Snapshot.SeasonId, Is.EqualTo(SecondSeasonId));
            Assert.That(_service.Snapshot.Progress.Tier, Is.Zero);
            Assert.That(_service.Snapshot.Progress.TotalXp, Is.Zero);
        }

        [Test]
        public void GrantXp_OfflineWithOfflineEarningDisabled_ReportsOfflineNotSeasonInactive()
        {
            UseConfig(SeasonExpiryPolicy.AutoGrant, allowOfflineGrants: false);
            Start();

            _backend.IsOnline = false;

            // "The season is over" is the wrong thing to tell a player whose season is running
            // fine and who simply lost signal.
            Assert.That(Grant("match_complete"), Is.EqualTo(XpGrantResult.Offline));
            Assert.That(_service.Phase, Is.EqualTo(SeasonPhase.Active));
        }

        [Test]
        public void GrantXp_Cancelled_DoesNotConsumeTheDailyAllowance()
        {
            Start(SeasonJson.Standard(dailyCap: 100));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<System.OperationCanceledException>(() =>
                RunFor(_service.GrantXpAsync("match_complete", 100, null, cts.Token)));

            // Charging the cap before the XP landed meant a cancelled grant cost the player
            // their allowance and gave them nothing back.
            Assert.That(Grant("match_complete", 100), Is.EqualTo(XpGrantResult.Granted));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(100));
        }

        [Test]
        public void ClaimTier_WritesOnceForTheWholeTier()
        {
            Start();
            _wallet.SetBalance("gems", 500);
            Grant("match_complete", 100);
            Unlock(SeasonTrack.Premium);

            var flushesBefore = _serialisation.FlushCount;

            Assert.That(RunFor(_service.ClaimTierAsync(1, SeasonTrack.Free, CancellationToken.None)),
                Is.EqualTo(1));

            // One synchronous disk write per tier, not one per reward on it.
            Assert.That(_serialisation.FlushCount - flushesBefore, Is.EqualTo(1));
        }

        [Test]
        public void ClaimTier_OnAnUnownedTrack_DeliversNothing()
        {
            Start();
            Grant("match_complete", 100);

            Assert.That(RunFor(_service.ClaimTierAsync(1, SeasonTrack.Premium, CancellationToken.None)),
                Is.Zero);
            Assert.That(_granter.Granted, Is.Empty);
        }

        // ── Backend ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GrantXp_WhileOffline_IsAppliedLocallyAndQueuedForReplay()
        {
            Start();
            _backend.IsOnline = false;

            Assert.That(Grant("match_complete", 50), Is.EqualTo(XpGrantResult.Granted));
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(50));
            Assert.That(_service.SavedData.PendingGrants, Has.Count.EqualTo(1));

            _backend.IsOnline = true;
            Run(_service.RefreshAsync(CancellationToken.None));

            Assert.That(_backend.SyncCount, Is.GreaterThan(0));
            Assert.That(_service.SavedData.PendingGrants, Is.Empty);
        }

        [Test]
        public void Sync_WithAStaleRemoteRecord_NeverLowersProgress()
        {
            Start();
            Grant("match_complete", 250);

            _backend.RemoteRecord = RemoteRecord(SeasonJson.SeasonId, 100);
            Run(_service.RefreshAsync(CancellationToken.None));

            // A reconnect that read an old total must not snap the tier backwards; that reads
            // as lost progress even when the next sync repairs it.
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(250));
            Assert.That(_service.Snapshot.Progress.Tier, Is.EqualTo(2));
        }

        [Test]
        public void Sync_WithAheadRemoteRecord_AdoptsTheHigherTotalAndItsClaims()
        {
            Start();
            Grant("match_complete", 50);

            var remote = RemoteRecord(SeasonJson.SeasonId, 300);
            remote.RecordClaim(Key(1, SeasonTrack.Free, "f1"));
            _backend.RemoteRecord = remote;

            Run(_service.RefreshAsync(CancellationToken.None));

            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(300));
            Assert.That(_service.SavedData.HasClaimed(Key(1, SeasonTrack.Free, "f1")), Is.True);
        }

        [Test]
        public void Sync_WithARecordFromADifferentSeason_IsIgnored()
        {
            Start();
            Grant("match_complete", 50);

            _backend.RemoteRecord = RemoteRecord("some_other_season", 5_000);
            Run(_service.RefreshAsync(CancellationToken.None));

            // Merging across a rollover would resurrect a finished season's numbers against
            // the current ladder.
            Assert.That(_service.SavedData.TotalXp, Is.EqualTo(50));
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────────

        private void Start(SeasonPassData season = null)
        {
            _content.Add(season ?? SeasonJson.Standard());

            _service = new SeasonPassService(_clock, _content, _backend, _config);
            _service.SetRewardGranter(_granter);
            _service.SetWallet(_wallet);

            Run(_service.InitializeAsync(CancellationToken.None));
        }

        private void AdvanceToSecondSeason()
        {
            _content.Add(SeasonJson.Standard(SecondSeasonId, "2026-07-01T00:00:00Z",
                "2026-08-01T00:00:00Z"));

            _clock.UtcNow = new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc);

            Run(_service.RefreshAsync(CancellationToken.None));
        }

        private XpGrantResult Grant(string sourceId, int amount = 0, string grantId = null) =>
            RunFor(_service.GrantXpAsync(sourceId, amount, grantId, CancellationToken.None));

        private ClaimResult Claim(int tier, SeasonTrack track, string rewardId) =>
            RunFor(_service.ClaimAsync(Ref(tier, track, rewardId), CancellationToken.None));

        private TrackUnlockResult Unlock(SeasonTrack track,
            SeasonPassPayment payment = SeasonPassPayment.Currency) =>
            RunFor(_service.UnlockTrackAsync(track, payment, CancellationToken.None));

        private QuestProgressResult Report(string questId, int amount = 1) =>
            RunFor(_service.ReportQuestProgressAsync(questId, amount, CancellationToken.None));

        private SeasonQuestProgress FindQuest(string questId)
        {
            foreach (var progress in _service.SavedData.QuestProgress)
            {
                if (progress.QuestId == questId) return progress;
            }

            return null;
        }

        private static SeasonRewardRef Ref(int tier, SeasonTrack track, string rewardId) =>
            new(SeasonJson.SeasonId, tier, track, rewardId);

        private static string Key(int tier, SeasonTrack track, string rewardId) =>
            Ref(tier, track, rewardId).ToClaimKey();

        private static SeasonPassSavedData RemoteRecord(string seasonId, int totalXp)
        {
            var record = new SeasonPassSavedData { Id = "remote" };
            record.BeginSeason(seasonId, null, 0);
            record.AddXp(totalXp);

            return record;
        }

        private static UniSeasonPassConfig BuildConfig(
            SeasonExpiryPolicy policy = SeasonExpiryPolicy.AutoGrant, bool autoClaim = false,
            bool allowOfflineGrants = true)
        {
            var config = ScriptableObject.CreateInstance<UniSeasonPassConfig>();

            // Overwriting through JsonUtility sets the private serialized fields without
            // opening test-only setters on a shipped asset.
            JsonUtility.FromJsonOverwrite($@"{{
              ""_saveId"": ""season_pass_test"",
              ""_flushOnCheckpoint"": true,
              ""_maxArchiveEntries"": 8,
              ""_expiryPolicy"": {(int)policy},
              ""_autoClaim"": {(autoClaim ? "true" : "false")},
              ""_allowOfflineGrants"": {(allowOfflineGrants ? "true" : "false")},
              ""_syncOnRefresh"": true
            }}", config);

            return config;
        }

        private static void Run(UniTask task) => task.GetAwaiter().GetResult();

        private static T RunFor<T>(UniTask<T> task) => task.GetAwaiter().GetResult();
    }
}
