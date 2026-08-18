# UniTx Daily Rewards

Daily login reward calendars: one idempotent claim per day, a streak that rewards
consecutive logins, and a reset whose hour you decide. Entirely local and free — no server,
no paid service — with two seams (`IDailyRewardsBackend`, `IDailyRewardsRewardGranter`) that
make a backend and your own economy drop in later without touching a call site.

**Unity 6.5 (`6000.5`) or newer.**

---

## Install

UPM cannot resolve git dependencies declared inside a package, so paste the whole chain into
`Packages/manifest.json`. Order does not matter there.

```jsonc
"dependencies": {
  "com.cysharp.unitask":     "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc":          "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.9.0",
  "com.uni-tx.core":         "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.9.0",
  "com.uni-tx.events":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.9.0",
  "com.uni-tx.resources":    "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.9.0",
  "com.uni-tx.content":      "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.9.0",
  "com.uni-tx.serialization":"https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.9.0",
  "com.uni-tx.entity":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.9.0",
  "com.uni-tx.currency":     "https://github.com/uni-tx/kit.git?path=/com.uni-tx.currency#currency@1.9.0",
  "com.uni-tx.rewards":      "https://github.com/uni-tx/kit.git?path=/com.uni-tx.rewards#rewards@1.9.0",
  "com.uni-tx.daily-rewards":"https://github.com/uni-tx/kit.git?path=/com.uni-tx.daily-rewards#daily-rewards@1.9.0"
}
```

`com.uni-tx.entity` is the foundation the calendar builds on — its static and saved data
live in a `DailyRewardsEntity`, with a stable save key and the calendar id as the content
key. `com.uni-tx.rewards` is the default for delivering rewards (and through it,
`com.uni-tx.currency` for currency rewards); it is picked up automatically when present and
can be replaced by binding your own `IDailyRewardsRewardGranter`.

Also optional, and picked up automatically when present: `com.uni-tx.analytics` enables
`DailyRewardsAnalytics`. It is not a declared dependency, so a game that tracks nothing is
not made to ship a provider.

---

## Quick start

```csharp
// 1. Calendar definitions are JSON content, loaded by Addressables label.
ContentRegistry.Register<DailyRewardsData>("daily_default");
await content.LoadContentAsync(new[] { "content" }, cToken);

// 2. Start the calendar. The granter is the only piece you must write.
var service = new DailyRewardsService(clock, content,
    new LocalDailyRewardsBackend(serialisation));
service.SetRewardGranter(myGranter);

await UniDailyRewards.InitializeAsync(service, cToken);

// 3. The daily ritual.
if (UniDailyRewards.IsClaimable)
{
    var result = await UniDailyRewards.ClaimAsync(cToken);
    Debug.Log($"Claimed: {result} — streak is now {UniDailyRewards.Snapshot.Streak}.");
}

// 4. Call this on app resume and when the rewards screen opens.
await UniDailyRewards.RefreshAsync(cToken);
```

Or add `DailyRewardsStep` to your `AppLoader`, after content loading and after your own
economy is bound.

---

## The one thing to get right

**Write a granter that tells the truth.**

```csharp
public UniTask<bool> GrantAsync(DailyRewardSlotData slot, DailyRewardRef reference,
    string grantId, CancellationToken cToken = default)
{
    if (!_inventory.TryAdd(slot.ItemId, slot.Amount)) return UniTask.FromResult(false);

    return UniTask.FromResult(true);
}
```

A claim is recorded **only after** the granter returns `true`. Return `false` and the day's
reward stays claimable, goes on the retry queue, and raises `DailyRewardGrantFailed`. A
granter that swallows a failure and returns `true` marks a day collected that never arrived —
the one bug in this system a player will notice and never forgive.

---

## What it handles

| Concern | How |
|---|---|
| One claim per day | A day-boundary guard, plus an idempotent grant id per calendar day — a replayed delivery cannot double-pay. |
| Missed days | **Calendar mode** (default): the position follows the wall clock, missed slots are skipped. **Streak mode**: missing a day resets to day one, so day N really costs N consecutive logins. |
| After the last slot | **Loop** (default) wraps back to day one; a finite calendar stops paying out (`Finished`). |
| The reset hour | Configurable, UTC — midnight by default, any hour 0-23 (a game whose day starts at 9 a.m. local should say 9). |
| Streak | Consecutive claims grow it; `DailyStreakReset` fires when it breaks; the snapshot shows zero until the next claim restarts it. |
| Milestone rewards | Any slot can be flagged `_isMilestone` (the day-7 chest); the snapshot exposes it so the UI can feature it. |
| Failed deliveries | Recorded, never advanced past — the same slot is retried on the next claim or refresh. |
| Device clock tampering | Time is a high-water mark: it moves forward, never back. Bind `ServerClock` to also resist fast-forwarding. |
| Calendar replacement | The save key never changes; a new calendar id resets the position while the collected-claims history survives. |

---

## Content

One JSON file per calendar, tagged with an Addressables label. Fields are `_`-prefixed
because `JsonUtility` maps fields, not properties.

```jsonc
[{
  "_id": "daily_default",
  "_displayName": "Daily Rewards",
  "_mode": 0,
  "_loop": true,
  "_slots": [
    { "_day": 1, "_rewardId": "d1_coins", "_kind": 0, "_itemId": "coins", "_amount": 50, "_iconAddress": "icon_d1_coins" },
    { "_day": 7, "_rewardId": "d7_chest", "_kind": 1, "_itemId": "chest", "_amount": 1, "_isMilestone": true, "_iconAddress": "icon_d7_chest" }
  ]
}]
```

Slots are sorted on load, so authoring order does not matter. `DailyRewardsData.DescribeProblems()`
reports what would misbehave rather than fail loudly — including a duplicated reward id,
which would be indistinguishable from a re-claim in telemetry.

Editing a shipped file in place changes the ladder underneath players who are already
climbing it. Ship a new file under a new id instead — the position resets, the history
survives.

---

## Events

Struct events on the kit bus, so a screen, a toast and an analytics adapter can all listen
without knowing about each other:

`DailyRewardClaimed` · `DailyRewardGrantFailed` · `DailyStreakReset`

`IDailyRewardsService.OnChanged` carries a whole `DailyRewardsSnapshot` — state, streak,
current slot, countdown — and is what a UI binds to.

---

## Server authority

`LocalDailyRewardsBackend` stores progress on the device — complete for single-player, and
trivially editable by the player. For anything valuable, implement `IDailyRewardsBackend`
against a validated endpoint:

- `LoadAsync` / `SaveAsync` — read and write the player's record.
- `IsOnline` / `IsAuthoritative` — a remote backend reports the truth; the service itself
  is unchanged either way.

The client-side rules stay useful even then: they keep the UI honest and stop obvious abuse
before it reaches the wire. They are not a substitute for validating on the server.

---

## Notes

**Built on `com.uni-tx.entity`.** `DailyRewardsEntity` pairs the calendar definition (static
content, keyed by the calendar id) with the player's position (saved data, keyed by the
stable save id). The two keys being independent is exactly what makes a calendar replacement
safe — the save never moves while the content re-points.

**Free tier.** Everything here runs on the device with no account and no paid service; the
only optional integration is `com.uni-tx.analytics`, whose providers are free-tier friendly.
If you want a server later, `IDailyRewardsBackend` is the seam — no call site changes.

**Call `RefreshAsync` on resume and when the screen opens.** Nothing else drives the passage
of time — a session left open across a reset boundary notices only when it runs.

Sample: **Daily Rewards Flow** (headless, the whole lifecycle on a manual clock) and
**Daily Rewards Screen** (uGUI, needs widgets and sprite-loader).
