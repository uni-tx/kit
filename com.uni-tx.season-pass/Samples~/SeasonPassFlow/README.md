# Season Pass Flow

The whole lifecycle in one script, with no UI in the way: earning capped XP, claiming the
free track, buying the paid track mid-season and watching it back-grant, skipping a tier,
completing a quest, and refreshing.

## Setup

1. Import this sample, then add `season_summer.json` to an Addressables group.
2. Give it the label `content` (or change `Content Label` on the component).
3. Leave its **asset name** as `season_summer` — `ContentRegistry.Register` binds that exact
   name to the type, and a mismatch makes the loader skip the file with a warning.
4. Put `SeasonPassFlowSample` on a GameObject and press play. Read the console.

The season in the JSON runs from 2026 to 2030 so the sample is runnable whenever you open
it. A real season is four to eight weeks; ship one file per season and let the dates decide
which is active rather than editing one file in place.

## What to look at

**`SampleRewardGranter` is the part you replace.** The season pass never owns your inventory
or your currency balances — it tracks what a player has earned and calls you to deliver it.
Note the return value: `false` leaves the reward claimable and queued for retry. A granter
that swallows a failure and returns `true` marks the reward collected when it never arrived,
which is the one bug in this whole system players will notice and never forgive.

**Grant ids make retries free.** `GrantXpAsync(..., grantId: "match-0001")` applied twice
adds XP once. Use the match id, the session id, whatever your game already has — a dropped
connection then costs nothing instead of double-paying.

**Unlocking a track back-grants it.** Buying premium at tier 6 immediately pays out tiers 1
through 6 on the premium track. It deliberately does *not* claim the free rewards you left
sitting there; those are the player's to collect.

**Refresh is what notices time passing.** Rollover, expiry, daily windows and retries all
happen in `RefreshAsync`. Call it on app resume and when the season screen opens. A session
left running across a season boundary sees nothing until it runs.

## Trying the rollover

Set `_endUtc` to a date in the past, add a second definition to the same JSON array with a
start date before now, and press play. The previous season is archived, XP and ownership
reset, and — under the default `AutoGrant` policy — everything unlocked but never claimed is
delivered on the way out. What the granter already handed over is untouched: it lives in your
inventory, not in the pass.
