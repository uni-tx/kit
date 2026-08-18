# UniTx Quests

Quests, missions and goals: counter objectives fed from gameplay events, one-time, daily
and weekly cadences on UTC resets, prerequisite chains, and idempotent claims recorded only
after the rewards land. Entirely local and free — no server, no paid service — with two
seams (`IQuestsBackend`, `IQuestRewardGranter`) that make a backend and your own economy
drop in later without touching a call site.

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
  "com.uni-tx.quests":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.quests#quests@1.9.0"
}
```

`com.uni-tx.entity` is the foundation the board builds on — its static and saved data live
in a `QuestsEntity`, with a stable save key and the set id as the content key.
`com.uni-tx.rewards` is the default for delivering rewards (and through it,
`com.uni-tx.currency` for currency rewards); it is picked up automatically when present and
can be replaced by binding your own `IQuestRewardGranter`.

Also optional, and picked up automatically when present: `com.uni-tx.analytics` enables
`QuestsAnalytics`. It is not a declared dependency, so a game that tracks nothing is not
made to ship a provider.

---

## Quick start

```csharp
// 1. Quest sets are JSON content, loaded by Addressables label.
ContentRegistry.Register<QuestSetData>("quests_default");
await content.LoadContentAsync(new[] { "content" }, cToken);

// 2. Start the board. The granter is the only piece you must write.
var service = new QuestsService(clock, content,
    new LocalQuestsBackend(serialisation));
service.SetRewardGranter(myGranter);

await UniQuests.InitializeAsync(service, cToken);

// 3. Gameplay reports events; the board matches them to objectives by key.
await UniQuests.ReportProgressAsync("win_match", 1, cToken);

// 4. A completed quest can be claimed.
var result = await UniQuests.ClaimAsync("daily_win", cToken);
Debug.Log($"Claimed: {result}.");

// 5. Call this on app resume and when the quests screen opens.
await UniQuests.RefreshAsync(cToken);
```

Or add `QuestsStep` to your `AppLoader`, after content loading and after your own economy
is bound.

---

## The one thing to get right

**Write a granter that tells the truth.**

```csharp
public UniTask<bool> GrantAsync(QuestData quest, QuestRewardData reward,
    QuestRef reference, string grantId, CancellationToken cToken = default)
{
    if (!_inventory.TryAdd(reward.ItemId, reward.Amount)) return UniTask.FromResult(false);

    return UniTask.FromResult(true);
}
```

A claim is recorded **only after** the granter returns `true`. Return `false` and the
quest's rewards stay claimable, go on the retry queue, and raise `QuestGrantFailed`. A
granter that swallows a failure and returns `true` marks a quest collected that never
arrived — the one bug in this system a player will notice and never forgive.

---

## What it handles

| Concern | How |
|---|---|
| Progress from gameplay | `ReportProgressAsync(key, amount)` advances every objective whose key matches — the game reports events, the board decides what they mean. |
| Cadence | Per quest: **None** (one-time, claimed stays claimed), **Daily** (resets at the UTC reset hour), **Weekly** (resets at the hour on the week-start day). Mixed cadences coexist in one set. |
| Prerequisites | `_requiredQuestId` gates a quest until its prerequisite is claimed — a tutorial that hands over to a daily loop. |
| Claim once | A claim flag plus an idempotent grant id per (set, quest, period, reward) — a replayed delivery cannot double-pay. |
| Failed deliveries | Recorded, never advanced past — the same quest is retried on the next claim or refresh. |
| The reset hour | Configurable, UTC — midnight by default, any hour 0-23. The week-start day is configurable too (Monday by default). |
| Device clock tampering | Time is a high-water mark: it moves forward, never back. Bind `ServerClock` to also resist fast-forwarding. |
| Board replacement | The save key never changes; a new set id resets progress while the applied-grant ledger survives. |

---

## Content

One JSON file per quest set, tagged with an Addressables label. Fields are `_`-prefixed
because `JsonUtility` maps fields, not properties.

```jsonc
[{
  "_id": "quests_default",
  "_displayName": "Today's Quests",
  "_quests": [{
    "_id": "daily_win",
    "_displayName": "Win Matches",
    "_description": "Win two matches today",
    "_iconAddress": "icon_daily_win",
    "_reset": 1,
    "_order": 0,
    "_objectives": [
      { "_key": "win_match", "_displayName": "Win a match", "_target": 2, "_iconAddress": "icon_win_match" }
    ],
    "_rewards": [
      { "_rewardId": "dw_coins", "_kind": 0, "_itemId": "coins", "_amount": 100, "_iconAddress": "icon_coins" }
    ]
  }]
}]
```

`_reset` values are stable: `0` = one-time, `1` = daily, `2` = weekly. Quests are sorted by
`_order` on load, so authoring order does not matter. `QuestSetData.DescribeProblems()`
reports what would misbehave rather than fail loudly — including a duplicate quest id
(indistinguishable from a re-claim in telemetry) and a prerequisite that does not exist (a
quest locked forever).

Editing a shipped file in place changes the board underneath players who are already
working it. Ship a new file under a new id instead — the progress resets, the grant ledger
survives.

---

## Events

Struct events on the kit bus, so a screen, a toast and an analytics adapter can all listen
without knowing about each other:

`QuestStarted` · `QuestProgressed` · `QuestCompleted` · `QuestClaimed` · `QuestGrantFailed` ·
`QuestPeriodReset`

`IQuestsService.OnChanged` carries a whole `QuestsSnapshot` — set id, every quest with its
state, objectives and rewards, plus the countdown to the next reset — and is what a UI
binds to.

---

## Server authority

`LocalQuestsBackend` stores progress on the device — complete for single-player, and
trivially editable by the player. For anything valuable, implement `IQuestsBackend` against
a validated endpoint:

- `LoadAsync` / `SaveAsync` — read and write the player's record.
- `IsOnline` / `IsAuthoritative` — a remote backend reports the truth; the service itself
  is unchanged either way.

The client-side rules stay useful even then: they keep the UI honest and stop obvious abuse
before it reaches the wire. They are not a substitute for validating on the server.

---

## Notes

**Built on `com.uni-tx.entity`.** `QuestsEntity` pairs the set definition (static content,
keyed by the set id) with the player's progress (saved data, keyed by the stable save id).
The two keys being independent is exactly what makes a board replacement safe — the save
never moves while the content re-points.

**Free tier.** Everything here runs on the device with no account and no paid service; the
only optional integration is `com.uni-tx.analytics`, whose providers are free-tier friendly.
If you want a server later, `IQuestsBackend` is the seam — no call site changes.

**Call `RefreshAsync` on resume and when the screen opens.** Nothing else drives the passage
of time — a session left open across a reset boundary notices only when it runs.

Sample: **Quests Flow** (headless, the whole lifecycle on a manual clock) and
**Quests Screen** (uGUI, needs widgets and sprite-loader).
