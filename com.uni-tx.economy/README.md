# UniTx Economy

The economy layer of the kit: **any number of named economies**, each a content-defined
grouping of currencies with **exchange rules** (convert one currency into another at a
fixed rate) and **virtual purchases** (cost one or more currencies, grant rewards through
the kit's reward service — atomically and idempotently). Entirely local and free — no
server, no paid service — with one seam (`IEconomyBackend`) that makes a backend drop in
later without touching a call site.

Balances themselves live in `com.uni-tx.currency` — the economy layer is the rules on
top: which currencies belong together, how they convert, and what a player can buy with
them. A game can ship a core economy, a meta economy, a seasonal economy, one per game
mode — each is an isolated group of currencies with its own rules, so the core loop can
never be flooded by a seasonal event and vice versa.

**Unity 6.5 (`6000.5`) or newer.**

---

## Install

UPM cannot resolve git dependencies declared inside a package, so paste the whole chain into
`Packages/manifest.json`. Order does not matter there.

```jsonc
"dependencies": {
  "com.cysharp.unitask":      "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc":           "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.11.0",
  "com.uni-tx.core":          "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.11.0",
  "com.uni-tx.events":        "https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.11.0",
  "com.uni-tx.resources":     "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.11.0",
  "com.uni-tx.content":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.11.0",
  "com.uni-tx.serialization": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.11.0",
  "com.uni-tx.entity":        "https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.11.0",
  "com.uni-tx.currency":      "https://github.com/uni-tx/kit.git?path=/com.uni-tx.currency#currency@1.11.0",
  "com.uni-tx.rewards":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.rewards#rewards@1.11.0",
  "com.uni-tx.economy":       "https://github.com/uni-tx/kit.git?path=/com.uni-tx.economy#economy@1.11.0"
}
```

`com.uni-tx.currency` is the wallet the economy reads and mutates — bind it (and load
currency content) before using the economy. `com.uni-tx.rewards` is the default for
delivering purchase rewards; it is picked up automatically when present and can be
replaced by a game's own reward routing.

Also optional, and picked up automatically when present:
- `com.uni-tx.analytics` enables `EconomyAnalytics`.

---

## Quick start

```csharp
// 1. Economies are JSON content, loaded by Addressables label.
ContentRegistry.Register<EconomyData>("economy_default");
await content.LoadContentAsync(new[] { "content" }, cToken);

// 2. Start the economy after the currency wallet is bound.
await UniEconomy.InitializeAsync(service, cToken);

// 3. Read, exchange and buy.
var snapshot = UniEconomy.Snapshot;                       // currencies + balances + rules + purchases
var exchanged = await UniEconomy.ExchangeAsync("core", "coins_to_gems", 10, "x-1");
var bought    = await UniEconomy.PurchaseAsync("core", "power_up", "k-1");
```

---

## Concepts

### N economies

An economy is one `EconomyData` content item: a display name, the currency ids that
belong to it, its exchange rules, and its purchases. Register as many as the game needs —
each gets its own entity and save file (`economy:<id>`), so they never collide and never
contaminate each other.

### Exchange

`ExchangeRuleData` converts a source currency into a target currency at a whole-unit
rate (one source unit buys `rate` target units), with optional minimum/maximum bounds.
`ExchangeAsync` spends the source atomically, grants the target, and records the
exchange id — a replay of the same id is a no-op, so a retried request cannot move
currency twice.

### Virtual purchase

`PurchaseData` lists costs (one or more currency lines) and rewards (kit `RewardData`).
`PurchaseAsync` checks affordability, charges every cost line, then grants every reward
through `IRewardService` — idempotent per purchase key. If a reward fails to deliver, the
costs stay charged (the player keeps the purchase) and the rewards are retried on the
next refresh without re-charging.

---

## Samples

- **Economy Flow** — two economies on a manual clock: grant, exchange, buy.
- **Economy Screen** — a uGUI wallet with one tab per economy (needs
  `com.uni-tx.widgets` and `com.uni-tx.sprite-loader`).

---

## Seams

- `IEconomyBackend` — where economy progress is stored. `LocalEconomyBackend` keeps
  everything on the device; a server-authoritative implementation slots in later.
- `IEconomyService` — the service interface, injectable and testable; `UniEconomy` is a
  thin static facade over it.
