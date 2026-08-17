# Daily Rewards Screen

A uGUI daily rewards screen wired end to end: a header with the calendar name, the streak
and a countdown to the next claim; a row of day cells with reward icons and a milestone
badge on the featured day; and a claim button that only appears on today's slot.

## Requirements

This sample needs two more UniTx packages beyond the daily rewards package's own install
chain:

```jsonc
"com.uni-tx.widgets":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.widgets#widgets@1.8.0",
"com.uni-tx.sprite-loader": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.sprite-loader#sprite-loader@1.8.0"
```

Its assembly is constrained on both, so importing the sample without them **skips** the
assembly rather than filling your console with errors. If the scripts appear to do nothing,
that is why — install the two packages and they compile.

## Setup

1. Build a `DailyRewardsDayCell` prefab: a day number label, a reward `Image` with an
   `ImageSpriteLoader`, a claim `Button`, plus a milestone badge, a claimed overlay and a
   locked overlay. Assign the fields in the inspector.
2. Put `DailyRewardsScreenSample` on your screen root and assign the header controls, the
   calendar content `RectTransform` and the cell prefab.
3. Initialize the daily rewards service first — through `DailyRewardsStep` or by hand — then
   push the screen. `Initialize` binds it to `UniDailyRewards.Service` automatically.

Slot icons come from each slot's `_iconAddress` (an Addressables address). The flow sample's
`daily_rewards_default.json` ships addresses for every day so the screen has something to
load.

## What to look at

**It repaints from `OnChanged`, never from `Update`.** The calendar changes a handful of
times a session; rebuilding a week of cells every frame is pure waste on a mid-range Android
device.

**Future slots are dimmed, not hidden.** Showing what the rest of the week holds — the day-7
chest especially — is the retention mechanic. The screen is the offer.

**The claim button only exists on today's slot.** `RefreshAsync` on bind surfaces a fresh
claimable day but never auto-collects it; only a claim that failed earlier today is retried.
The tap is the player's.

**Buttons re-enable in a `finally`.** A granter that refuses would otherwise leave the player
looking at a dead button with no way to retry.

**Claim results are branched on, not ignored.** `GrantFailed` means the reward is still owed
and will be retried on the next refresh; `AlreadyClaimed` is a no-op the UI can shrug off.
