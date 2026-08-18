# Ladder Screen sample

A ready-to-restyle uGUI ladder: header, progress bar toward the next rung, and one row
per rung with its step threshold, rewards and a claim button.

## What it demonstrates

- **Binding to `UniLadder.OnChanged`** rather than polling — the climb changes a handful
  of times per session.
- **Which state each control reads**: `LadderRungSnapshot.State` drives the claim button,
  the dim overlay and the claimed overlay.
- **What to do with a refusal**: `GrantFailed` is logged distinctly, because that reward
  is still owed and will be retried on the next refresh.

## Wiring

1. Add this folder to the scene.
2. Hook up the prefab's references in the inspector:
   - `_ladderNameLabel`, `_stepsLabel` — header.
   - `_progressFill` (an `Image` with fill type set to "Filled"), `_progressLabel`.
   - `_rungPrefab` — a `LadderRungCell` row; `_listContent` — the list's content `RectTransform`.
3. The screen binds to `UniLadder.Service` on `Initialize()`, so it works with any game
   that installs the service through the bootstrap `LadderStep` or the facade directly.

## Dependencies

The assembly is skipped when the kit's `com.uni-tx.widgets` and `com.uni-tx.sprite-loader`
packages are absent, so installing this sample never breaks a project that does not use
them.
