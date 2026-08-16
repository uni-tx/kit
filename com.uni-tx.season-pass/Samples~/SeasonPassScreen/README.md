# Season Pass Screen

A uGUI season pass screen wired end to end: header with the season name, countdown and tier
bar; a scrollable ladder of tier cells with a free row and a paid row; a claim-all button
with an unclaimed badge; and a buy button that disappears once the track is owned.

## Requirements

This sample needs two more UniTx packages beyond the season pass's own install chain:

```jsonc
"com.uni-tx.widgets":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.widgets#widgets@1.4.0",
"com.uni-tx.sprite-loader": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.sprite-loader#sprite-loader@1.4.0"
```

Its assembly is constrained on both, so importing the sample without them **skips** the
assembly rather than filling your console with errors. If the scripts appear to do nothing,
that is why — install the two packages and they compile.

## Setup

1. Build a `SeasonPassTierCell` prefab: a tier number label, a free reward button with an
   `Image` and an `ImageSpriteLoader`, the same pair for the paid reward, plus a locked
   overlay and a claimed overlay. Assign the fields in the inspector.
2. Put `SeasonPassScreenSample` on your screen root and assign the header controls, the
   ladder content `RectTransform` and the cell prefab.
3. Initialize the season pass first — through `SeasonPassStep` or by hand — then push the
   screen. `Initialize` binds it to `UniSeasonPass.Service` automatically.

## What to look at

**It repaints from `OnChanged`, never from `Update`.** The pass changes a handful of times a
session; rebuilding a hundred cells every frame is pure waste on a mid-range Android device.

**Locked paid rewards are dimmed, not hidden.** Showing what the paid track holds is the
conversion mechanic — the screen is the offer.

**Buttons re-enable in a `finally`.** A granter that refuses would otherwise leave the player
looking at a dead button with no way to retry.

**Claim results are branched on, not ignored.** `GrantFailed` means the reward is still owed
and will be retried on the next refresh; `InsufficientFunds` on the buy button is where a real
game opens the currency store instead of showing an error.
