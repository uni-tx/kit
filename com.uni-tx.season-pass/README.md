# UniTx Season Pass

A seasonal battle pass: free and paid reward tracks, XP that only moves forward, capped earn
sources, quests, tier skips, and a rollover that archives a season without taking anything the
player earned.

Works entirely offline out of the box, and has one seam — `ISeasonPassBackend` — that makes a
server authoritative later without touching a single call site.

**Unity 6.5 (`6000.5`) or newer.**

---

## Install

UPM cannot resolve git dependencies declared inside a package, so paste the whole chain into
`Packages/manifest.json`. Order does not matter there.

```jsonc
"dependencies": {
  "com.cysharp.unitask":     "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc":          "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.8.0",
  "com.uni-tx.core":         "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.8.0",
  "com.uni-tx.events":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.8.0",
  "com.uni-tx.resources":    "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.8.0",
  "com.uni-tx.content":      "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.8.0",
  "com.uni-tx.serialization":"https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.8.0",
  "com.uni-tx.entity":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.8.0",
  "com.uni-tx.currency":     "https://github.com/uni-tx/kit.git?path=/com.uni-tx.currency#currency@1.8.0",
  "com.uni-tx.rewards":      "https://github.com/uni-tx/kit.git?path=/com.uni-tx.rewards#rewards@1.8.0",
  "com.uni-tx.season-pass":  "https://github.com/uni-tx/kit.git?path=/com.uni-tx.season-pass#season-pass@1.8.0"
}
```

`com.uni-tx.entity` is the foundation the pass builds on — its static and saved data live
in a `SeasonPassEntity`, with a stable save key and the season id as the content key.
`com.uni-tx.currency` and `com.uni-tx.rewards` are the defaults for selling the paid track
and delivering rewards; both are picked up automatically when present, and either can be
replaced by binding your own `ISeasonPassWallet` / `ISeasonPassRewardGranter`.

Also optional, and picked up automatically when present: `com.uni-tx.iap` enables
`SeasonPassIapBridge`, `com.uni-tx.analytics` enables `SeasonPassAnalytics`. Neither is a
declared dependency, so a game that sells nothing is not made to ship a billing SDK.

---

## Quick start

```csharp
// 1. Season definitions are JSON content, loaded by Addressables label.
ContentRegistry.Register<SeasonPassData>("season_summer");
await content.LoadContentAsync(new[] { "content" }, cToken);

// 2. Start the pass. The granter is the only piece you must write.
var service = new SeasonPassService(clock, content, new LocalSeasonPassBackend(serialisation));
service.SetRewardGranter(myGranter);
service.SetWallet(myWallet);

await UniSeasonPass.InitializeAsync(service, cToken);

// 3. Earn, from a source the season whitelists.
await UniSeasonPass.GrantXpAsync("match_complete", grantId: matchId, cToken: cToken);

// 4. Collect.
await UniSeasonPass.ClaimAllAsync(cToken);

// 5. Sell the paid track. Everything already passed on it pays out immediately.
await UniSeasonPass.UnlockTrackAsync(SeasonTrack.Premium, SeasonPassPayment.Currency, cToken);
```

Or add `SeasonPassStep` to your `AppLoader`, after content loading and after your own economy
is bound.

---

## The one thing to get right

**Write a granter that tells the truth.**

```csharp
public UniTask<bool> GrantAsync(SeasonRewardData reward, SeasonRewardRef reference,
    CancellationToken cToken = default)
{
    if (!_inventory.TryAdd(reward.ItemId, reward.Amount)) return UniTask.FromResult(false);

    return UniTask.FromResult(true);
}
```

A claim is recorded **only after** the granter returns `true`. Return `false` and the reward
stays claimable, goes on a retry queue, and raises `SeasonRewardGrantFailed`. A granter that
swallows a failure and returns `true` marks a reward collected that never arrived — the one
bug in this system a player will notice and never forgive.

---

## What it handles

| Concern | How |
|---|---|
| Season rollover | Archives the outgoing season, resets XP, claims, ownership and quests, keeps banked skips. Granted rewards live in your inventory and are never touched. |
| Unclaimed at season end | `AutoGrant` (default, forgiving), `GraceWindow` (claim for N hours after the end), or `Forfeit`. |
| Buying the pass mid-season | Back-grants every tier already passed on the newly owned track — and deliberately not the free rewards the player chose to leave. |
| Tier skips | Converted to exactly the XP the next tier needs, so total XP stays the only number that decides standing. Skips past the final tier are banked for next season. |
| Reconnect after a disconnect | XP is monotonic and reconciliation takes the higher of local and remote, so a stale read cannot rewind a player's tier. |
| Replayed grants | An idempotency id per grant; a repeat is ignored rather than paid twice. |
| Farming | Per-source daily caps on fixed UTC-midnight windows, plus a source whitelist — an unlisted id is refused. |
| Device clock tampering | Time is a high-water mark: it moves forward, never back. Bind `ServerClock` to also resist fast-forwarding. |
| Past the final tier | Repeatable bonus tiers, if the season defines them. |

---

## Content

One JSON file per season, tagged with an Addressables label. Fields are `_`-prefixed because
`JsonUtility` maps fields, not properties.

```jsonc
[{
  "_id": "season_summer",
  "_startUtc": "2026-09-01T00:00:00Z",
  "_endUtc": "2026-10-15T00:00:00Z",
  "_tiers": [
    { "_tier": 1, "_requiredXp": 100, "_rewards": [
      { "_rewardId": "t1_coins", "_track": 0, "_kind": 0, "_itemId": "coins", "_amount": 100 },
      { "_rewardId": "t1_gems",  "_track": 1, "_kind": 0, "_itemId": "gems",  "_amount": 25 }
    ] }
  ],
  "_trackOffers": [
    { "_track": 1, "_productId": "com.game.pass", "_currencyId": "gems", "_currencyCost": 500 }
  ],
  "_xpSources": [
    { "_sourceId": "match_complete", "_xpPerEvent": 50, "_dailyCap": 500 }
  ]
}]
```

Thresholds are **cumulative** and are sorted on load, so authoring order does not matter.
`SeasonPassData.DescribeProblems()` reports what would misbehave rather than fail loudly —
including a duplicate reward id on the same track, which would be unclaimable, since collecting
one marks both.

Ship one file per season and let the dates decide which is active. Editing a single file in
place changes the ladder underneath players who are already climbing it.

---

## Events

Struct events on the kit bus, so a screen, a toast and an analytics adapter can all listen
without knowing about each other:

`SeasonXpGranted` · `SeasonTierUnlocked` · `SeasonRewardClaimed` · `SeasonRewardGrantFailed` ·
`SeasonTrackUnlocked` · `SeasonChanged` · `SeasonEndingSoon` · `SeasonQuestCompleted`

`ISeasonPassService.OnChanged` carries a whole `SeasonPassSnapshot` and is what a UI binds to.

---

## Server authority

`LocalSeasonPassBackend` stores progress on the device — complete for single-player, and
trivially editable by the player. For anything valuable, implement `ISeasonPassBackend` against
a validated endpoint:

- `LoadAsync` / `SaveAsync` — read and write the player's record.
- `SyncAsync` — replay the pending grant queue and return what the server believes.
  `SeasonPassReconciler` merges it in, resolving upward on every field.
- `IsOnline` — while false, grants are applied locally and queued, so play never blocks on
  connectivity.

The client-side rules stay useful even then: they keep the UI honest and stop obvious abuse
before it reaches the wire. They are not a substitute for validating on the server.

---

## Notes

**Built on `com.uni-tx.entity`.** `SeasonPassEntity` pairs the season definition (static
content, keyed by the season id) with the player's progress (saved data, keyed by the stable
save id). The two keys being independent is exactly what makes rollover safe — the save never
moves while the content re-points every season.

**Call `RefreshAsync` on resume and when the screen opens.** Nothing else drives the passage of
time — a session left open across a season boundary notices only when it runs.

Samples: **Season Pass Flow** (headless, the whole lifecycle) and **Season Pass Screen** (uGUI,
needs widgets and sprite-loader).
