# Changelog

All notable changes to `com.uni-tx.daily-rewards` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

## [1.10.0] - 2026-08-18

### Changed

- Version bumped in lockstep with the rest of the kit (24 packages at 1.10.0).


## [1.9.0] - 2026-08-17

### Changed

- Version bump only; all UniTx packages are released in lockstep. This release adds
  `com.uni-tx.ladder`, the kit's reward ladder package (a cumulative climb fed by
  quest completions, per-rung rewards, grand prize), to the kit.


## [1.8.0] - 2026-08-17

### Changed

- Version bump only; all UniTx packages are released in lockstep. This release adds
  `com.uni-tx.quests`, the kit's quests/missions package (counter objectives,
  daily/weekly resets, prerequisite chains, idempotent claims), to the kit.

## [1.7.0] - 2026-08-17

### Added

- `DailyRewardsScreen` sample — a uGUI calendar screen (widgets + sprite-loader) that
  repaints from `OnChanged`, dims future slots instead of hiding them, and claims from
  today's slot. The flow sample's slots now ship `_iconAddress` values for its cells.

## [1.6.0] - 2026-08-17

### Added

- Initial release.
- `DailyRewardsEntity` — the calendar's static and saved data live in a
  `com.uni-tx.entity` entity: stable save key (`daily_rewards`), the calendar id as the
  content key, and persistence routed through `IDailyRewardsBackend`.
- `DailyRewardsService` — one idempotent claim per day, streak tracking, and a reset that
  `UniDailyRewardsConfig.ResetHourUtc` decides the hour of. `RefreshAsync` is the only
  thing that notices the passage of time — call it on resume and when the screen opens.
- `DailyRewardsCalculator` — pure calendar math: which slot a claim lands on, how the
  position advances, and the effective streak.
- Calendar and streak modes: `Calendar` skips missed days (the position follows the wall
  clock), `Streak` resets to day one, so the day-N reward genuinely costs N consecutive
  logins. `Loop` wraps after the last slot; a finite calendar stops paying out.
- Claims recorded only after `IDailyRewardsRewardGranter` confirms delivery; a refusal or
  a throw keeps the slot claimable and retries it on the next claim or refresh, raising
  `DailyRewardGrantFailed` instead of marking the day collected.
- Idempotent grant ids scoped to the calendar day, passed through to the kit's reward
  service so a replayed delivery cannot double-pay.
- Clock high-water mark: time may move forward but never back, so winding the device clock
  back cannot reopen a claimed day or refill the calendar.
- `UniDailyRewardsConfig` — policy asset (save id, reset hour, flush on checkpoint, forced
  calendar id) with `DescribeProblems()`; `DailyRewardsData` reports its own content
  problems, including duplicate reward ids.
- Struct events on the kit bus: `DailyRewardClaimed`, `DailyRewardGrantFailed`,
  `DailyStreakReset` (the churn signal).
- `IDailyRewardsBackend` with `LocalDailyRewardsBackend` over the kit's serialisation
  service, so server authority slots in without touching a call site.
- Optional integration assembly behind a define constraint so the package compiles
  without it: `DailyRewardsAnalytics` (claims, grant failures, streak resets).
- `DailyRewardsStep` in `com.uni-tx.bootstrap`, so wiring is opt-in per project.
- Sample: `DailyRewardsFlow` (headless lifecycle on a manual clock, including a missed-day
  streak break and the day-7 milestone).
