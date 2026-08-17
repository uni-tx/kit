# UniTx Rewards

Generic reward definitions and kind-routing delivery into the game's economy.

**Unity 6.5 (6000.5) or newer** · MIT · v1.5.0

## Install

Unity's Package Manager **cannot resolve git dependencies declared inside a package**
([manual](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-git.html)), so this
package's siblings are not pulled in automatically. Paste the whole block into
`Packages/manifest.json` — order does not matter there, UPM resolves the set together:

```jsonc
"dependencies": {
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.7.0",
  "com.uni-tx.core": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.7.0",
  "com.uni-tx.resources": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.7.0",
  "com.uni-tx.content": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.7.0",
  "com.uni-tx.events": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.7.0",
  "com.uni-tx.entity": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.7.0",
  "com.uni-tx.currency": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.currency#currency@1.7.0",
  "com.uni-tx.rewards": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.rewards#rewards@1.7.0"
}
```

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`, `com.uni-tx.resources`,
  `com.uni-tx.content`, `com.uni-tx.events`, `com.uni-tx.entity`, `com.uni-tx.currency`
- **Unity registry dependencies** (resolved automatically by UPM):
  - `com.unity.test-framework` 1.4.6 (the shipped Tests/ assemblies)

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of `CS0246`.

## Quick start

Rewards are static content; delivery is a service. Define rewards as JSON content:

```jsonc
{
  "_id": "daily_chest",
  "_kind": 0,
  "_itemId": "gems",
  "_amount": 25
}
```

Then grant them through the service, which routes by kind — currency rewards land in the
entity-based currency system, item/cosmetic/booster/custom rewards land on the entity
whose id matches `_itemId` (that entity implements `IRewardConsumer`):

```csharp
var rewards = resolver.Resolve<IRewardService>();
await rewards.InitializeAsync(cToken);

var chest = content.GetData<RewardData>("daily_chest");
await rewards.GrantAsync(chest, "chest:2026-08-17", cToken); // idempotent by grant id
```

A reward is recorded as delivered only after `GrantAsync` returns `Granted`. A handler
that fails leaves the reward unclaimed and retryable, never marked collected and gone.

## Samples

Import from **Package Manager ▸ UniTx Rewards ▸ Samples**.

- **Reward Flow** — currency and entity rewards delivered through the reward service,
  showing kind routing and idempotent grant ids.

## Notes

- **Rewards decide what, handlers decide where.** The service routes by `RewardKind`;
  the handler owns what a reward means. Install your own per kind with `SetHandler`.
- **Currency rewards are idempotent.** Pass a grant id (a purchase id, a server grant id)
  and a replayed delivery is ignored instead of minting currency twice.
- **Entity rewards land on entities.** An entity registered in the entity service that
  implements `IRewardConsumer` receives item, cosmetic, booster and custom rewards by its
  id — the same entity foundation the season pass and the currency system build on.
- **A kind with no handler logs and succeeds**, so the whole flow is playable and testable
  before an economy exists. The warning exists so it cannot go unnoticed in a build.

## Conventions

Every package in the kit follows the same rules:

- **UniTask only** — no coroutines, no `System.Threading.Tasks`, no `async void`.
  Fire-and-forget is `UniTaskVoid` + `.Forget()`.
- **Cancellation tokens** — every async API takes one as its last argument. Pass
  `this.GetCancellationTokenOnDestroy()` from a MonoBehaviour.
- **Serialized fields** — `[SerializeField] private T _name;` exposed through a
  read-only property.
- Interfaces and implementations split; statics are facades over a swappable service.

## License

[MIT](LICENSE.md)
