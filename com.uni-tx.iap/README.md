# UniTx IAP

SDK-agnostic in-app purchases: awaitable buys, restore handling, entitlement fan-out and a
Unity IAP 5.x adapter — with the store SDK kept behind a seam so a project that does not
sell anything never links a billing library.

- **Awaitable purchases** — `await UniIap.PurchaseAsync(id)` instead of a listener pair.
- **One place to grant content** — `UniIap.OnPurchased` fires for purchases *and* restores,
  renewals and deferred orders that clear later.
- **Orders are always confirmed**, so the store stops re-delivering them.
- **Degrades instead of failing** — no adapter means `IapResult.Unsupported`, not a crash.

## Install

Unity's Package Manager **cannot resolve git dependencies declared inside a package**
([manual](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-git.html)), so this
package's siblings are not pulled in automatically. Paste the whole block into
`Packages/manifest.json` — order does not matter there, UPM resolves the set together:

```jsonc
"dependencies": {
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.2.0",
  "com.uni-tx.core": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.2.0",
  "com.uni-tx.iap": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.iap#iap@1.2.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.2.0`
3. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.2.0`
4. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.iap#iap@1.2.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`
- **Unity registry dependencies:** none required. Install
  `com.unity.purchasing` (5.0.0+) from **Package Manager ▸ Unity Registry** to enable the
  Unity IAP adapter.

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of `CS0246`.

## Quick start

1. **Create the catalog** — `Assets ▸ Create ▸ UniTx ▸ IAP ▸ Config`, and put it in a
   `Resources` folder as `UniIapConfig` if you want the facade to find it by itself.
2. **Add your products.** The ids must match App Store Connect and the Google Play Console
   exactly. Set store-specific overrides only when the ids differ between platforms.
3. **Initialize once at boot**, then buy from the UI:

```csharp
using UniTx.Iap;
using UniTx.Iap.Providers;   // only with com.unity.purchasing installed

// At boot — subscribe BEFORE initializing, so restores fired during startup are caught.
UniIap.OnPurchased += Grant;
await UniIap.InitializeAsync(new UnityIapProvider(), config, cToken);

// From the buy button.
var result = await UniIap.PurchaseAsync("com.game.gems", cToken);
if (result.Result == IapResult.Cancelled) ShowToast("Purchase cancelled");

// The single place content is granted.
void Grant(IapPurchase purchase)
{
    if (purchase.ProductId == "com.game.gems") Wallet.Add(100);
}
```

### Grant content from the event, not the return value

This is the one thing worth getting right. `PurchaseAsync` returns only for the purchase
*you started*. These never come back through it:

| Situation | How the entitlement arrives |
|---|---|
| Player reinstalls and restores | `OnPurchased` |
| Ask-to-Buy approval clears next launch | `OnPurchased` |
| Subscription renews | `OnPurchased` |
| App was killed mid-purchase | `OnPurchased`, on the next launch |

Granting from the return value ships a game that loses purchases the player has paid for,
and every one of those becomes a refund request.

## Providers

| Provider | Requires | Notes |
|---|---|---|
| `UnityIapProvider` | `com.unity.purchasing` 5.0.0+ | Compiled only when the package is present. |
| `NoOpIapProvider` | nothing | Everything resolves to `Unsupported`. Use on desktop and in tests. |

The adapter lives in its own assembly (`com.uni-tx.iap.unity`) behind a
`UNITX_UNITY_IAP` constraint, so the package compiles with or without Unity IAP installed.
It targets the **5.x service API** (`UnityIAPServices.StoreController()`) — not the 4.x
`IStoreController`/`IStoreListener` pair that most published samples still show.

## Samples

**Shop Screen** — awaiting a purchase, granting from the entitlement event, restoring on
iOS, and showing store-localized prices. Import via **Package Manager ▸ Samples**.

## Notes

- **Prices come from the store.** `UniIap.GetPrice(id)` returns the player's own currency
  and formatting. Showing a hard-coded price that differs from the payment sheet is a
  store-review rejection.
- **iOS needs a Restore button.** An app selling non-consumables without a
  player-initiated restore is rejected. Call `UniIap.RestoreAsync()`.
- **`IsOwned` gates UI, nothing more.** A client-side ownership check is trivially
  defeated; validate receipts on a server for anything valuable. `IapPurchase.Receipt`
  carries the raw store receipt for exactly that.
- **Consumables are never marked owned**, so they can be bought again. That comes from the
  `Kind` you declare in the catalog, which is why it has to be right.
- **Test purchases** run against the store sandbox — a Google Play internal-testing track
  or an App Store sandbox account. The editor uses Unity IAP's fake store.

## Conventions

- Async is UniTask only; every async call takes and forwards a `CancellationToken`.
- Serialized fields are `[SerializeField] private T _name;` with a `public T Name => _name;`
  getter.
- Statics reset at `SubsystemRegistration`, so the package behaves with domain reload
  disabled.

## License

MIT — see [LICENSE.md](LICENSE.md).
