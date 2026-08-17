# Changelog

All notable changes to `com.uni-tx.bootstrap` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

## [1.5.0] - 2026-08-17

### Added

- `BindDependenciesStep` now binds `CurrencyService` and `RewardService`, so the
  entity-based economy services are resolvable out of the box.
- `SeasonPassStep` defaults the pass's wallet and granter to the kit's currency and
  reward services when the game has not bound its own.

### Changed

- `FullBootstrapSample` loads entities through `LoadEntitiesAsync`.
- This release also adds `com.uni-tx.currency` and `com.uni-tx.rewards`.

## [1.4.0] - 2026-08-17

### Added

- `SeasonPassStep` — binds and starts `com.uni-tx.season-pass`. A step of its own rather
  than part of `BindDependenciesStep`, so a game without a season pass does not pay for
  one. Place it after content loading and after the game's own economy is bound, since the
  first refresh can claim rewards and needs the reward granter to already be resolvable.

## [1.3.0] - 2026-08-16

### Changed

- Declared every Unity registry package this package's runtime code actually uses.
  Registry dependencies — unlike git URLs — do resolve from a package's own
  `package.json`, so declaring them is what makes a single-package install work; omitting
  one left consumers with a `CS0246` for a type they never asked about.

## [1.2.0] - 2026-08-16

### Changed

- Version bump only; all UniTx packages are released in lockstep.

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
