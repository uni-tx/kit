# Changelog

All notable changes to `com.uni-tx.rewards` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

## [1.7.0] - 2026-08-17

### Changed

- Version bump only; all UniTx packages are released in lockstep. This release adds the
  `DailyRewardsScreen` uGUI sample to `com.uni-tx.daily-rewards` and a mandatory
  dev-to-public sync verification rule.

## [1.6.0] - 2026-08-17

### Changed

- Version bump only; all UniTx packages are released in lockstep. This release adds
  `com.uni-tx.daily-rewards` as the kit's twenty-first package, which delivers its
  rewards through this package's `IRewardService`.

## [1.5.0] - 2026-08-17

### Added

- Initial release as the kit's twentieth package.
- `RewardData` — generic static reward definition (kind, item id, amount, icon) loaded
  as JSON content, with a programmatic constructor for mapping rewards from other systems.
- `IRewardService` / `RewardService` — kind-routing delivery with idempotent grant ids
  and `RewardGranted` events on the kit bus.
- `CurrencyRewardHandler` — currency rewards into `com.uni-tx.currency`.
- `EntityRewardHandler` — item, cosmetic, booster and custom rewards onto a registered
  `IRewardConsumer` entity, which is what makes reward delivery entity-based.
- `LoggingRewardHandler` — fallback that keeps the flow playable before an economy exists.
