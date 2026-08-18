# Changelog

All notable changes to `com.uni-tx.store` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

## [1.10.0] - 2026-08-18

### Added

- Initial release.
- `StoreEntity` — the shop's static and saved data live in a `com.uni-tx.entity`
  entity: stable save key (`store`), the store id as the content key, and persistence
  routed through `IStoreBackend`.
- `StoreService` — a content-defined shop where every offer is one of three kinds:
  **IAP** (sold through `UniIap`, with the price always read from the store so a
  hard-coded price can never drift from the payment sheet), **free** (claimable on a
  per-offer cooldown — the repeat-visit loop a Lucky Pack uses), and **rewarded**
  (a rewarded ad via `UniAds`, granted only when the ad actually completes). Claims
  are idempotent per grant id, a claim is recorded only after delivery succeeds, and
  failed deliveries stay claimable for retry.
- `StoreCalculator` — pure, engine-free rules: whether an offer is on cooldown, at its
  claim limit, or ready, so the shop's decisions are unit-testable without the engine.
- `UniStore` — static facade over the service, plus `UniStoreConfig` policy asset
  (save id, forced store id, flush-on-checkpoint).
- `IStoreRewardGranter` seam with the default `StoreRewardGranter` mapping offers onto
  the kit's `com.uni-tx.rewards` service, and `LoggingStoreRewardGranter` for games
  without an economy yet.
- Optional guarded integrations: `com.uni-tx.store.iap` (grants IAP offers from
  `UniIap.OnPurchased`, so restores and deferred purchases pay out) and
  `com.uni-tx.store.analytics` (reports claims to `UniAnalytics`).
- Bootstrap `StoreStep` for wiring the shop into the boot flow.
- Samples: `StoreFlow` (headless lifecycle) and `StoreScreen` (a scrollable uGUI shop
  with daily deals first, free offer last).
- EditMode tests for the calculator and the service (cooldowns, limits, idempotency,
  IAP/rewarded paths, failure retry).
