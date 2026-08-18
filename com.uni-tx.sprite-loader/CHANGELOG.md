# Changelog

All notable changes to `com.uni-tx.sprite-loader` are documented here.

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

### Changed

- `com.unity.addressables` floor raised to **4.0.1** (registry latest), up from 2.9.1.

## [1.1.0] - 2026-08-16

### Changed

- **Minimum editor is now Unity 6.5 (`6000.5`)**, up from `6000.0`.
- **`dependencies` no longer contains git URLs.** Unity's Package Manager cannot resolve
  git dependencies between packages, so every UniTx sibling and UniTask listed there made
  this package fail to install. They are now documented as an ordered install list and a
  copy-paste `manifest.json` block in the README.

### Added

- `samples` entries in `package.json`, so the bundled samples are importable from the
  Package Manager. Without them a `Samples~` folder is invisible and ships as dead weight.
- `documentationUrl`, `changelogUrl` and `licensesUrl` metadata.

## [1.0.0] - 2026-08-15

Initial internal version. Never published — the public repository did not exist and the
declared git dependencies could not resolve.
