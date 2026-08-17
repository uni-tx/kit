# Daily Rewards Flow

A headless walk through the whole daily rewards lifecycle, with no UI:

1. Initialize the service over content + a local save.
2. Claim day one — the streak starts at 1.
3. Come back the next day — the streak grows and the calendar advances one slot.
4. Miss two days — in **calendar mode** the position skips ahead to wherever the calendar
   is now, and the streak resets (a `DailyStreakReset` event fires).
5. Land on day seven — the milestone chest.

## Setup

1. Make `daily_rewards_default.json` an Addressable asset and tag it with the `content`
   label (the field on the component defaults to `content`).
2. The asset name must match the registered file name: `daily_rewards_default`.
3. Add `DailyRewardsFlowSample` to a scene, press play, read the console.

## What it demonstrates

- A 7-day looping calendar (`"_loop": true`), so day eight would wrap back to slot zero.
- The default granter seam replaced with a pretend inventory, so no economy is needed.
- The kit's event bus carrying `DailyRewardClaimed` and `DailyStreakReset` to the console.
- A manual `SampleClock` — swap in `LocalClock` (or `ServerClock`) and the same code is a
  production flow.
