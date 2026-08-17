# Changelog

All notable changes to `com.uni-tx.currency` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

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
