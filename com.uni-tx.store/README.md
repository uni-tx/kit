# UniTx Store

A content-driven shop. Every offer is one of three kinds — **IAP** (sold through the
store with the price always read from the payment backend, never hard-coded), **free**
(claimable on a per-offer cooldown — the repeat-visit loop a Lucky Pack uses), or
**rewarded** (a rewarded video, granted only when the ad actually completes). Claims are
idempotent, delivered through your own economy, and a failed delivery stays claimable
for retry. Entirely local and free — no server, no paid service — with two seams
(`IStoreBackend`, `IStoreRewardGranter`) that make a backend and your own economy drop
in later without touching a call site.

**Unity 6.5 (`6000.5`) or newer.**

---

## Install

UPM cannot resolve git dependencies declared inside a package, so paste the whole chain into
`Packages/manifest.json`. Order does not matter there.

```jsonc
"dependencies": {
  "com.cysharp.unitask":      "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc":           "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.10.0",
  "com.uni-tx.core":          "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.10.0",
  "com.uni-tx.events":        "https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.10.0",
  "com.uni-tx.resources":     "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.10.0",
  "com.uni-tx.content":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.10.0",
  "com.uni-tx.serialization": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.10.0",
  "com.uni-tx.entity":        "https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.10.0",
  "com.uni-tx.currency":      "https://github.com/uni-tx/kit.git?path=/com.uni-tx.currency#currency@1.10.0",
  "com.uni-tx.rewards":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.rewards#rewards@1.10.0",
  "com.uni-tx.iap":           "https://github.com/uni-tx/kit.git?path=/com.uni-tx.iap#iap@1.10.0",
  "com.uni-tx.ads":           "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ads#ads@1.10.0",
  "com.uni-tx.store":         "https://github.com/uni-tx/kit.git?path=/com.uni-tx.store#store@1.10.0"
}
```

`com.uni-tx.entity` is the foundation the store builds on — its static and saved data live
in a `StoreEntity`, with a stable save key and the store id as the content key.
`com.uni-tx.rewards` is the default for delivering rewards (and through it,
`com.uni-tx.currency` for currency rewards); it is picked up automatically when present and
can be replaced by binding your own `IStoreRewardGranter`. `com.uni-tx.iap` powers IAP
offers and `com.uni-tx.ads` powers rewarded offers — each is a thin facade with a
`NoOp` provider, so a store with no payment or ad backend installed still runs: paid and
rewarded offers simply report `Unsupported`/`NotReady` instead of throwing.

Also optional, and picked up automatically when present:
- `com.uni-tx.analytics` enables `StoreAnalytics`.

---

## Quick start

```csharp
// 1. The shop is JSON content, loaded by Addressables label.
ContentRegistry.Register<StoreData>("store_default");
await content.LoadContentAsync(new[] { "content" }, cToken);

// 2. Start the shop. The granter is the only piece you must write.
var service = new StoreService(content, new LocalStoreBackend(serialisation));
service.SetRewardGranter(myGranter);

await UniStore.InitializeAsync(service, cToken);

// 3. A player claims an offer. Free and rewarded offers resolve here.
var result = await UniStore.ClaimAsync("free_pack", cToken);
Debug.Log($"Claimed: {result}.");

// 4. Call this on app resume and when the shop screen opens.
await UniStore.RefreshAsync(cToken);
```

Or add `StoreStep` to your `AppLoader`, after content loading and after your own economy
is bound.

### Buying an IAP offer

```csharp
var result = await UniStore.ClaimAsync("starter_pack", cToken);
```

An IAP offer forwards to `UniIap.PurchaseAsync` and reports the store's verdict. The
entitlement itself is granted from `UniIap.OnPurchased` — which the service subscribes to
on initialize — the only place restores, deferred purchases and subscription renewals ever
appear. Wiring the grant to the purchase call instead ships a game that loses content
players paid for.

### Watching a rewarded offer

```csharp
var result = await UniStore.ClaimAsync("bonus_coins", cToken);
// Result is Rewarded when the ad completed; Skipped / NotReady / Unsupported otherwise.
```

Rewards are granted **only** when the ad completes — never on close, never on skip — and
the grant is flushed before the next frame.

---

## The one thing to get right

**Write a granter that tells the truth.**

```csharp
public UniTask<bool> GrantAsync(StoreOfferData offer, StoreRewardData reward,
    StoreOfferRef reference, string grantId, CancellationToken cToken = default)
{
    if (!_inventory.TryAdd(reward.ItemId, reward.Amount)) return UniTask.FromResult(false);

    return UniTask.FromResult(true);
}
```

A claim is recorded **only after** the granter returns `true`. Return `false` and the
offer's rewards stay claimable and go on the retry queue. A granter that swallows a
failure and returns `true` marks an offer collected that never arrived — the one bug in
this system a player will notice and never forgive.

---

## What it handles

| Concern | How |
|---|---|
| Offer kinds | `Iap` sells through `UniIap`; `Free` claims on a per-offer cooldown; `Rewarded` watches a rewarded ad via `UniAds` and grants only on completion. |
| IAP entitlements | The service subscribes to `UniIap.OnPurchased`, so a restore or a deferred purchase pays out — never wired to the purchase call's return value. |
| Price display | Always read through `UniIap.GetPrice(productId)` — the store returns the player's own currency and formatting, and showing a price that differs from the payment sheet is a store-review rejection. |
| Cooldowns | Per-offer `CooldownSeconds` in the JSON; the calculator reports how long until the next claim. |
| Claim limits | Per-offer `MaxClaims` (0 = unlimited) for one-time starter packs, daily caps and event limits. |
| Idempotency | Every claim carries a deterministic grant id; a replayed claim never double-pays. |
| Delivery | `IStoreRewardGranter` routes each reward through your economy; the default maps onto the kit's reward service. |
| Retry | A failed delivery stays claimable and is retried on the next refresh — a player who saw the failure and closed the app still gets the reward. |
| Persistence | `StoreEntity` behind `IStoreBackend`; `LocalStoreBackend` saves through the kit's serialisation service, batched and atomic. |
| Events | `StoreOfferClaimed` on the kit bus for toasts, quests and analytics. |
| Restores | The service subscribes to `UniIap.OnPurchased`, so an iOS restore re-delivers owned offers. |
| Degradation | No IAP/ads provider installed → paid and rewarded offers report `Unsupported`/`NotReady` rather than throwing. |

---

## Configuration

`UniStoreConfig` (Assets ▸ Create ▸ UniTx ▸ Store ▸ Config):

- `SaveId` — stable save key (default `store`).
- `ForcedStoreId` — pin a specific store instead of picking the first registered one.
- `FlushOnCheckpoint` — write a claim to disk immediately (default on; a claim lost to a
  crash is a support ticket, not a rounding error).
- `VerboseLogging` — log every claim and refresh.

## Samples

- **Store Flow** — the whole lifecycle headless: claim a free offer, watch a rewarded
  offer, buy an IAP; with no provider installed the paid kinds degrade gracefully.
- **Store Screen** — a scrollable uGUI shop with section headers, daily deals first and
  the free offer last, each row showing rewards, price and a claim button.
