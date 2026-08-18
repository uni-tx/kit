# Changelog

All notable changes to `com.uni-tx.quests` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

## [1.9.0] - 2026-08-17

### Changed

- Version bump only; all UniTx packages are released in lockstep. This release adds
  `com.uni-tx.ladder`, the kit's reward ladder package (a cumulative climb fed by
  quest completions, per-rung rewards, grand prize), to the kit.


## [1.8.0] - 2026-08-17

### Added

- Initial release.
- `QuestsEntity` — the board's static and saved data live in a `com.uni-tx.entity`
  entity: stable save key (`quests`), the set id as the content key, and persistence
  routed through `IQuestsBackend`.
- `QuestsService` — counter objectives fed from gameplay events via
  `ReportProgressAsync(key, amount)`, per-quest cadence (one-time, daily, weekly) with UTC
  resets, prerequisite chains, and claims recorded only after delivery confirms.
  `RefreshAsync` is the only thing that notices the passage of time — call it on resume
  and when the screen opens.
- `QuestCalculator` — pure quest math: state evaluation (available, in progress,
  completed, claimed, locked), what a report or a claim would do, and the next reset
  boundary.
- Cadence per quest: `None` (one-time, claimed stays claimed), `Daily` (resets at
  `UniQuestsConfig.ResetHourUtc`), `Weekly` (resets at the hour on the configurable
  week-start day). Mixed cadences coexist in one set.
- Prerequisite chains: `_requiredQuestId` gates a quest until its prerequisite is claimed,
  with unknown prerequisites reported by `DescribeProblems()` rather than locking forever.
- Claims recorded only after `IQuestRewardGranter` confirms delivery; a refusal or a throw
  keeps the quest claimable and retries it on the next claim or refresh, raising
  `QuestGrantFailed` instead of marking the quest collected.
- Idempotent grant ids scoped to (set, quest, period, reward), passed through to the kit's
  reward service so a replayed delivery cannot double-pay.
- Clock high-water mark: time may move forward but never back, so winding the device clock
  back cannot reopen a claimed quest.
- `UniQuestsConfig` — policy asset (save id, reset hour, week-start day, flush on
  checkpoint, forced set id) with `DescribeProblems()`; `QuestSetData` reports its own
  content problems, including duplicate quest ids and dangling prerequisites.
- Struct events on the kit bus: `QuestStarted`, `QuestProgressed`, `QuestCompleted`,
  `QuestClaimed`, `QuestGrantFailed`, `QuestPeriodReset`.
- `IQuestsBackend` with `LocalQuestsBackend` over the kit's serialisation service, so
  server authority slots in without touching a call site.
- Optional integration assembly behind a define constraint so the package compiles
  without it: `QuestsAnalytics` (started, progressed, completed, claimed, grant failures,
  period resets).
- `QuestsStep` in `com.uni-tx.bootstrap`, so wiring is opt-in per project.
- Samples: `QuestsFlow` (headless lifecycle on a manual clock, including a daily rollover)
  and `QuestsScreen` (uGUI quest list with per-objective progress, claim button and
  locked/claimed overlays).
