# Changelog

All notable changes to `com.uni-tx.ladder` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

## [1.10.0] - 2026-08-18

### Changed

- Version bumped in lockstep with the rest of the kit (24 packages at 1.10.0).


## [1.9.0] - 2026-08-17

### Added

- Initial release.
- `LadderEntity` — the ladder's static and saved data live in a `com.uni-tx.entity`
  entity: stable save key (`ladder`), the ladder id as the content key, and persistence
  routed through `ILadderBackend`.
- `LadderService` — a cumulative climb fed by `ReportStepsAsync(steps)`, where every rung
  whose threshold the total crosses becomes claimable, claims are recorded only after
  delivery confirms, and the top rung's claim raises `LadderCompleted`. `RefreshAsync`
  re-evaluates the selection and retries failed deliveries.
- `LadderCalculator` — pure ladder math: rung state evaluation (locked, reached, claimed),
  what a claim would do, and the progress toward the next rung for a screen.
- `QuestsLadderBridge` — optional integration (own assembly, behind a define constraint)
  that listens to `QuestClaimed` and reports one step per claimed quest, configurable via
  a mapping function — complete a task, climb the ladder.
- Claims recorded only after `ILadderRewardGranter` confirms delivery; a refusal or a
  throw keeps the rung claimable and retries it on the next claim or refresh, raising
  `LadderGrantFailed` instead of marking the rung collected.
- Idempotent grant ids scoped to (ladder, rung, reward), passed through to the kit's
  reward service so a replayed delivery cannot double-pay.
- `UniLadderConfig` — policy asset (save id, flush on checkpoint, forced ladder id) with
  `DescribeProblems()`; `LadderData` reports its own content problems, including duplicate
  rung ids and rungs that share a step threshold.
- Struct events on the kit bus: `LadderStepsAdded`, `LadderRungReached`, `LadderRungClaimed`,
  `LadderGrantFailed`, `LadderCompleted`.
- `ILadderBackend` with `LocalLadderBackend` over the kit's serialisation service, so
  server authority slots in without touching a call site.
- Optional integration assembly behind a define constraint so the package compiles
  without it: `LadderAnalytics` (steps added, rung reached, rung claimed, grant failures,
  completion).
- `LadderStep` in `com.uni-tx.bootstrap`, so wiring is opt-in per project.
- Samples: `LadderFlow` (headless lifecycle: report steps, claim rungs, complete the
  ladder) and `LadderScreen` (uGUI ladder with progress bar, claim button and
  locked/claimed overlays).
