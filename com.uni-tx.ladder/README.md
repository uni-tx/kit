# UniTx Ladder

Reward ladders: a cumulative climb fed by reported steps — typically quest completions —
where each rung pays out once its threshold is crossed and the top rung is the grand
prize. Entirely local and free — no server, no paid service — with two seams
(`ILadderBackend`, `ILadderRewardGranter`) that make a backend and your own economy drop in
later without touching a call site, plus an optional `QuestsLadderBridge` that turns every
claimed quest into a step.

**Unity 6.5 (`6000.5`) or newer.**

---

## Install

UPM cannot resolve git dependencies declared inside a package, so paste the whole chain into
`Packages/manifest.json`. Order does not matter there.

```jsonc
"dependencies": {
  "com.cysharp.unitask":      "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc":           "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.9.0",
  "com.uni-tx.core":          "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.9.0",
  "com.uni-tx.events":        "https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.9.0",
  "com.uni-tx.resources":     "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.9.0",
  "com.uni-tx.content":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.9.0",
  "com.uni-tx.serialization": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.9.0",
  "com.uni-tx.entity":        "https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.9.0",
  "com.uni-tx.currency":      "https://github.com/uni-tx/kit.git?path=/com.uni-tx.currency#currency@1.9.0",
  "com.uni-tx.rewards":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.rewards#rewards@1.9.0",
  "com.uni-tx.ladder":        "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ladder#ladder@1.9.0"
}
```

`com.uni-tx.entity` is the foundation the ladder builds on — its static and saved data live
in a `LadderEntity`, with a stable save key and the ladder id as the content key.
`com.uni-tx.rewards` is the default for delivering rewards (and through it,
`com.uni-tx.currency` for currency rewards); it is picked up automatically when present and
can be replaced by binding your own `ILadderRewardGranter`.

Also optional, and picked up automatically when present:
- `com.uni-tx.analytics` enables `LadderAnalytics`.
- `com.uni-tx.quests` enables `QuestsLadderBridge` — the climb fed by quest completions.

Neither is a declared dependency, so a game that tracks nothing or has no quests is not
made to ship an extra system.

---

## Quick start

```csharp
// 1. Ladders are JSON content, loaded by Addressables label.
ContentRegistry.Register<LadderData>("ladder_default");
await content.LoadContentAsync(new[] { "content" }, cToken);

// 2. Start the climb. The granter is the only piece you must write.
var service = new LadderService(content, new LocalLadderBackend(serialisation));
service.SetRewardGranter(myGranter);

await UniLadder.InitializeAsync(service, cToken);

// 3. Gameplay reports steps; rungs unlock as the total crosses their thresholds.
await UniLadder.ReportStepsAsync(1, cToken);

// 4. A reached rung can be claimed.
var result = await UniLadder.ClaimAsync("grand_prize", cToken);
Debug.Log($"Claimed: {result}.");

// 5. Call this on app resume and when the ladder screen opens.
await UniLadder.RefreshAsync(cToken);
```

Or add `LadderStep` to your `AppLoader`, after content loading and after your own economy
is bound.

### Climbing from quests

With `com.uni-tx.quests` installed, the bridge climbs the ladder for every claimed quest:

```csharp
// After both services are up: each claimed quest adds one step.
using var bridge = new QuestsLadderBridge(UniLadder.Service);

// Quests weigh differently? Map the quest to its steps.
using var weighted = new QuestsLadderBridge(UniLadder.Service,
    claimed => claimed.QuestId == "weekly_marathon" ? 3 : 1);
```

The bridge listens to `QuestClaimed` — raised only after a quest's rewards actually land —
so the ladder advances exactly when the player has something in hand.

---

## The one thing to get right

**Write a granter that tells the truth.**

```csharp
public UniTask<bool> GrantAsync(LadderRungData rung, LadderRewardData reward,
    LadderRungRef reference, string grantId, CancellationToken cToken = default)
{
    if (!_inventory.TryAdd(reward.ItemId, reward.Amount)) return UniTask.FromResult(false);

    return UniTask.FromResult(true);
}
```

A claim is recorded **only after** the granter returns `true`. Return `false` and the
rung's rewards stay claimable, go on the retry queue, and raise `LadderGrantFailed`. A
granter that swallows a failure and returns `true` marks a rung collected that never
arrived — the one bug in this system a player will notice and never forgive.

---

## What it handles

| Concern | How |
|---|---|
| The climb | `ReportStepsAsync(steps)` grows a cumulative total; every rung whose threshold it crosses becomes claimable. |
| Steps from quests | `QuestsLadderBridge` listens to `QuestClaimed` and reports one step per quest (configurable) — complete a task, climb the ladder. |
| Rung rewards | Each rung pays its own rewards; the top rung is the grand prize. |
| Claim once | A claim flag plus an idempotent grant id per (ladder, rung, reward) — a replayed delivery cannot double-pay. |
| Failed deliveries | Recorded, never advanced past — the same rung is retried on the next claim or refresh. |
| Ladder replacement | The save key never changes; a new ladder id restarts the climb while the applied-grant ledger survives. |
| Completion | `LadderCompleted` fires when the grand prize is claimed — the event's conversion signal. |

---

## Content

One JSON file per ladder, tagged with an Addressables label. Fields are `_`-prefixed
because `JsonUtility` maps fields, not properties.

```jsonc
[{
  "_id": "launch_ladder",
  "_displayName": "Launch Event",
  "_rungs": [{
    "_id": "first_claim",
    "_displayName": "First Claim",
    "_iconAddress": "icon_first_claim",
    "_steps": 1,
    "_rewards": [
      { "_rewardId": "fc_coins", "_kind": 0, "_itemId": "coins", "_amount": 100, "_iconAddress": "icon_coins" }
    ]
  }]
}]
```

`_steps` is **cumulative** — the total steps climbed must reach it. Rungs are sorted on
load, so authoring order does not matter; the last rung in the sorted order is the grand
prize. `LadderData.DescribeProblems()` reports what would misbehave rather than fail
loudly — including a duplicate rung id (indistinguishable from a re-claim in telemetry)
and two rungs sharing a threshold (the second one is unreachable).

Editing a shipped file in place changes the ladder underneath players who are already
climbing it. Ship a new file under a new id instead — the climb restarts, the grant ledger
survives.

---

## Events

Struct events on the kit bus, so a screen, a toast and an analytics adapter can all listen
without knowing about each other:

`LadderStepsAdded` · `LadderRungReached` · `LadderRungClaimed` · `LadderGrantFailed` ·
`LadderCompleted`

`ILadderService.OnChanged` carries a whole `LadderSnapshot` — ladder id, total steps, every
rung with its state and rewards, plus progress toward the next rung — and is what a UI
binds to.

---

## Server authority

`LocalLadderBackend` stores progress on the device — complete for single-player, and
trivially editable by the player. For anything valuable, implement `ILadderBackend` against
a validated endpoint:

- `LoadAsync` / `SaveAsync` — read and write the player's record.
- `IsOnline` / `IsAuthoritative` — a remote backend reports the truth; the service itself
  is unchanged either way.

The client-side rules stay useful even then: they keep the UI honest and stop obvious abuse
before it reaches the wire. They are not a substitute for validating on the server.

---

## Notes

**Built on `com.uni-tx.entity`.** `LadderEntity` pairs the ladder definition (static
content, keyed by the ladder id) with the player's climb (saved data, keyed by the stable
save id). The two keys being independent is exactly what makes a ladder replacement safe —
the save never moves while the content re-points.

**Free tier.** Everything here runs on the device with no account and no paid service; the
only optional integrations are `com.uni-tx.analytics`, whose providers are free-tier
friendly, and `com.uni-tx.quests`, which is local too. If you want a server later,
`ILadderBackend` is the seam — no call site changes.

**Call `RefreshAsync` on resume and when the screen opens.** Nothing else re-evaluates the
selection — a ladder replaced server-side only notices when it runs.

Sample: **Ladder Flow** (headless, the whole lifecycle) and **Ladder Screen** (uGUI,
needs widgets and sprite-loader).
