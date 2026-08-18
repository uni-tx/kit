# Changelog

All notable changes to `com.uni-tx.iap` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

## [1.11.0] - 2026-08-18

### Changed

- Version bumped in lockstep with the rest of the kit (25 packages at 1.11.0).

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

### Changed

- Version bump only; all UniTx packages are released in lockstep. This release adds the
  `DailyRewardsScreen` uGUI sample to `com.uni-tx.daily-rewards` and a mandatory
  dev-to-public sync verification rule.

## [1.6.0] - 2026-08-17

### Changed

- Version bump only; all UniTx packages are released in lockstep. This release adds
  `com.uni-tx.daily-rewards` as the kit's twenty-first package.

## [1.5.0] - 2026-08-17

### Changed

- Version bump only; all UniTx packages are released in lockstep. This release adds
  `com.uni-tx.currency` and `com.uni-tx.rewards`, and reworks `com.uni-tx.entity`
  (decoupled content and save keys, async initialization).

## [1.4.0] - 2026-08-17

### Changed

- Version bump only; all UniTx packages are released in lockstep. This release adds
  `com.uni-tx.season-pass` as the kit's eighteenth package.

## [1.3.0] - 2026-08-16

### Changed

- Declared every Unity registry package this package's runtime code actually uses.
  Registry dependencies — unlike git URLs — do resolve from a package's own
  `package.json`, so declaring them is what makes a single-package install work; omitting
  one left consumers with a `CS0246` for a type they never asked about.

## [1.2.0] - 2026-08-16

### Added

- Initial release.
- `UniIap` — static facade with `InitializeAsync`, `PurchaseAsync`, `RestoreAsync`,
  `IsOwned`, `GetPrice` and `GetTitle`.
- `UniIap.OnPurchased` — the single place content is granted. Fires for direct purchases,
  restores, subscription renewals and deferred orders that clear later, so a game cannot
  lose an entitlement by only handling the purchase call's return value.
- `IIapProvider` — the store seam, with `NoOpIapProvider` as the default so a project
  without a billing SDK still compiles and runs.
- `UnityIapProvider` — adapter for **Unity IAP 5.x**, targeting the
  `UnityIAPServices.StoreController()` service API rather than the 4.x
  `IStoreController`/`IStoreListener` pair. Confirms every pending order, so the store
  stops re-delivering purchases and Google Play consumables can be bought again.
- `UniIapConfig` — catalog `ScriptableObject` with per-store product id overrides and
  `DescribeProblems()`, which reports blank and duplicate ids before they surface as an
  opaque store fetch failure.
- Purchase overlap guard, so a double-tapped buy button reaches the store once.
- Sample: **Shop Screen**.
