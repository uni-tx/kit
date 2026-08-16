# Changelog

All notable changes to `com.uni-tx.widgets` are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). All UniTx
packages are released in lockstep at the same version.

## [Unreleased]

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
