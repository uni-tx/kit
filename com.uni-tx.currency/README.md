# UniTx Currency

Currencies as entities: static currency definitions joined with per-player balances.

**Unity 6.5 (6000.5) or newer** · MIT · v1.5.0

## Install

Unity's Package Manager **cannot resolve git dependencies declared inside a package**
([manual](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-git.html)), so this
package's siblings are not pulled in automatically. Paste the whole block into
`Packages/manifest.json` — order does not matter there, UPM resolves the set together:

```jsonc
"dependencies": {
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.10.0",
  "com.uni-tx.core": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.10.0",
  "com.uni-tx.resources": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.10.0",
  "com.uni-tx.content": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.10.0",
  "com.uni-tx.serialization": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.10.0",
  "com.uni-tx.events": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.10.0",
  "com.uni-tx.entity": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.10.0",
  "com.uni-tx.currency": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.currency#currency@1.10.0"
}
```

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`, `com.uni-tx.resources`,
  `com.uni-tx.content`, `com.uni-tx.serialization`, `com.uni-tx.events`, `com.uni-tx.entity`
- **Unity registry dependencies** (resolved automatically by UPM):
  - `com.unity.test-framework` 1.4.6 (the shipped Tests/ assemblies)

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of `CS0246`.

## Quick start

Currencies are content-driven entities. Define each one as JSON content:

```jsonc
{
  "_id": "gems",
  "_displayName": "Gems",
  "_kind": 1,
  "_initialBalance": 0,
  "_maxBalance": 10000
}
```

Load content, then let the entity service build the currency entities (content first —
entities are built from the `CurrencyData` objects the content service is holding):

```csharp
await ContentLoader.LoadContentAsync(labels, cToken);
await entityService.LoadEntitiesAsync(cToken);

var wallet = resolver.Resolve<ICurrencyService>();
await wallet.InitializeAsync(cToken);

wallet.GrantAsync("gems", 500, "welcome-bundle", cToken);   // idempotent by grant id
wallet.TrySpend("gems", 120);                               // atomic
var balance = wallet.GetBalance("gems");
```

## Samples

Import from **Package Manager ▸ UniTx Currency ▸ Samples**.

- **Currency Wallet** — two currencies built as entities from content, then granted and
  spent through the currency service.

## Notes

- **Static and saved data never merge.** Tuning, the icon and the cap live in
  `CurrencyData` and ship with the build; the balance lives in `CurrencySavedData` and
  belongs to the player. A balance patch cannot touch a save.
- **Grants are idempotent.** Pass a grant id (a purchase id, a server grant id) and a
  replayed delivery is ignored instead of minting currency twice. The ledger is bounded.
- **The cap is content, not code.** Set `_maxBalance` per currency; grants are trimmed
  and report `Capped` so a caller can tell the difference from a full grant.
- **Spend is atomic.** `TrySpend` either deducts and returns `true`, or changes nothing
  and returns `false` — there is no in-between for a caller to misread.

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
