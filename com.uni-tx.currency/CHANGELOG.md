# Changelog

All notable changes to `com.uni-tx.currency` are documented here.

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

### Added

- Initial release as the kit's nineteenth package.
- `CurrencyData` — static per-currency definition (name, icon, soft/hard kind, starting
  balance, maximum balance) loaded as JSON content. Being an `IEntityData` makes every
  currency a content-driven entity.
- `Currency` — the currency entity: static definition joined with a per-player
  `CurrencySavedData` balance, with capped grants, atomic spends and a first-run starting
  balance.
- `CurrencyService` — balance reads, atomic `TrySpend`, and `GrantAsync` with an
  idempotent grant id, content-driven maximums and `CurrencyChanged` events on the kit bus.
