# Changelog

All notable changes to `com.uni-tx.economy` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

## [1.11.0] - 2026-08-18

### Added

- Initial release.
- `EconomyEntity` — each economy's static and saved data live in a
  `com.uni-tx.entity` entity: stable save key (`economy:<id>`), the economy id as
  the content key, and persistence routed through `IEconomyBackend`.
- `EconomyService` — any number of named economies, each a content-defined grouping
  of currencies (its own `EconomyData` content item), with:
  - **exchange** — convert one currency into another at a content-defined rate,
    spent atomically from the source and granted to the target, idempotent per
    exchange id;
  - **virtual purchases** — costs (one or more currencies) granted as rewards
    (through the kit's `IRewardService`) atomically and idempotently per purchase
    id, so a replayed delivery cannot charge twice or pay twice.
  Balances themselves stay in `com.uni-tx.currency` — the economy layer is the
  rules on top: which currencies belong together, how they convert, and what a
  player can buy with them.
- `EconomyCalculator` — pure, engine-free rules: exchange output, rate bounds and
  purchase affordability, so the decisions are unit-testable without the engine.
- `UniEconomy` — static facade over the service, plus `UniEconomyConfig` policy
  asset (save prefix, forced economy id, flush-on-checkpoint).
- Guarded `com.uni-tx.economy.analytics` integration (reports exchanges and
  purchases to `UniAnalytics`).
- Bootstrap `EconomyStep`.
- Samples: **EconomyFlow** (two economies on a manual clock) and **EconomyScreen**
  (uGUI wallet with one tab per economy).
- EditMode tests for the calculator and the service.
