# Changelog

All notable changes to `com.uni-tx.season-pass` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

## [1.4.0] - 2026-08-17

### Added

- Initial release.
- `UniSeasonPass` — static facade over `ISeasonPassService`, with `SubsystemRegistration`
  reset so a disabled domain reload cannot carry last session's service into this one.
- `SeasonPassService` — earning, ownership, claiming, expiry and rollover in one place.
  Three tracks (free, premium, premium plus), ordered so owning a higher one implies every
  track below it.
- Ephemeral season state and durable entitlements kept apart by type: `BeginSeason` wipes
  XP, claims, ownership and quest progress while banked tier skips and the season archive
  survive, and granted rewards are never stored by the pass at all.
- Monotonic season XP. `RaiseXpTo` never lowers a total, so a stale backend read on
  reconnect cannot snap a player's tier backwards.
- `SeasonPassCalculator` — pure tier maths: binary-searched cumulative thresholds,
  progress within a tier, and repeatable bonus tiers past the end of the ladder.
- Whitelisted XP sources with per-source daily caps on fixed UTC-midnight windows, and
  idempotency ids so a replayed grant is ignored rather than paid twice.
- Claims recorded only after `ISeasonPassRewardGranter` confirms delivery; a refusal or a
  throw queues the reward for retry and raises `SeasonRewardGrantFailed` instead of
  marking it collected.
- Retroactive back-grant: unlocking a paid track mid-season immediately pays out every
  tier already passed on it.
- Tier skips bought with money or currency, converted to exactly the XP needed for the
  next tier so total XP stays the single source of truth. Skips past the final tier are
  banked and applied at the next rollover.
- Configurable expiry: `AutoGrant`, `GraceWindow` or `Forfeit` for rewards unlocked but
  never claimed when a season closes.
- Quests with daily, weekly and seasonal reset windows, availability ranges and
  premium gating.
- `ISeasonPassBackend` with `LocalSeasonPassBackend` over the kit's serialisation service,
  an offline grant queue, and `SeasonPassReconciler`, which merges upward on every field.
- Clock high-water mark: time may move forward but never back, so winding the device clock
  back cannot reopen an ended season or refill a daily cap.
- `UniSeasonPassConfig` — policy asset with `DescribeProblems()`; `SeasonPassData` reports
  its own content problems, including duplicate reward ids that would be unclaimable.
- Struct events on the kit bus: XP granted, tier unlocked, reward claimed, grant failed,
  track unlocked, season changed, ending soon, quest completed.
- Optional integration assemblies, each behind its own define constraint so the package
  compiles without them: `SeasonPassIapBridge` (unlocks from `UniIap.OnPurchased`, so
  restores and deferred orders work) and `SeasonPassAnalytics` (funnel reporting).
- `SeasonPassStep` in `com.uni-tx.bootstrap`, so wiring is opt-in per project.
- Samples: `SeasonPassFlow` (headless lifecycle walk-through) and `SeasonPassScreen`
  (uGUI screen, skipped when widgets and sprite-loader are absent).
- Rollover waits for the incoming season to actually start. A season that is merely
  announced is displayed, never rolled into, so a teaser cannot wipe a player's standing
  weeks early; while one is shown, progress reads as zero rather than measuring the old
  save's XP against the new ladder.
- `XpGrantResult.Offline`, distinct from `SeasonInactive`. A player who lost signal during
  a running season should be told to reconnect, not that the season is over.
- A daily allowance is charged only once the XP has landed, and `GrantXpAsync` honours a
  cancelled token before mutating anything — a cancelled grant previously consumed the
  allowance and gave nothing back.
- `ClaimTierAsync` writes once for the whole tier rather than once per reward on it.
