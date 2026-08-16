# UniTx Ads

SDK-agnostic ads facade for rewarded, interstitial, app-open, banner and MREC placements, with nine-way positioning and safe-area offsetting.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

An SDK-agnostic ads facade covering rewarded, interstitial, app-open, banner and
MREC placements, with nine-way positioning, safe-area offsetting, cooldown and overlap
protection, and a simulated provider for development. A LevelPlay adapter ships behind a
version define.

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
  "com.uni-tx.ads": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ads#ads@1.2.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.2.0`
3. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.2.0`
4. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ads#ads@1.2.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`
- **Unity registry dependencies:** none

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
// Ad unit ids live on a UniAdsConfig asset — no code changes to ship.
await UniAds.InitializeAsync(new LevelPlayAdsProvider(), config, token);
UniAds.SetConsent(true);

var result = await UniAds.ShowRewardedAsync(cToken: token);
if (result.ShouldReward) GrantCoins(100);   // never branch on "the ad closed"

await UniAds.ShowBannerAsync(AdPlacement.At(AdPosition.BottomCenter), token);
await UniAds.ShowMrecAsync(AdPlacement.At(AdPosition.Center), token);

// Permanent capability, not fill — LevelPlay has no app-open unit, so hide the feature.
if (UniAds.Supports(AdFormat.AppOpen)) await UniAds.ShowAppOpenAsync(cToken: token);
```

### Formats and providers

| Format | LevelPlay | Notes |
|---|---|---|
| Rewarded | ✅ | Grant only on `ShouldReward` |
| Interstitial | ✅ | Paced by `InterstitialCooldown` |
| Banner | ✅ | Adaptive size, nine anchors or an exact dp coordinate |
| MREC | ✅ | A 300x250 banner ad unit — needs its own id |
| App-open | ❌ | LevelPlay has no app-open ad unit; use AdMob or AppLovin |

## Samples

Import from **Package Manager ▸ UniTx Ads ▸ Samples**.

- **Ads Placements** — Awaiting a rewarded ad result, showing an interstitial with a cooldown, and stubbing a provider in tests.

## Notes

- No SDK dependency. `NoOpAdsProvider` simulates results so the reward flow can be
  built and tested before integration.
- **Ad unit ids go on a `UniAdsConfig` asset**, not in code. Every field is blank by
  default — a template must never ship someone else's live ad unit. Test mode is forced in
  the editor and development builds, because testing against live units generates invalid
  traffic that ad networks penalize accounts for.
- Branch on `AdShowResult.ShouldReward`, never on "the ad closed" — rewarding on close
  pays players who skipped.
- `Supports(format)` is *permanent capability*; `IsReady(format)` is fill. Gate feature
  visibility on the first and button state on the second.
- **LevelPlay has no app-open format.** Its ad units are rewarded, interstitial, banner and
  native, so the adapter reports `Unsupported` rather than leaving callers waiting.
- **LevelPlay's `respectSafeArea` is Android-only**, so the facade computes the inset
  itself and hands it to the adapter — otherwise an iOS bottom banner sits under the home
  indicator.
- Destroy an inline ad when leaving the screen that owned it. Hiding leaves it
  auto-refreshing and burning impressions nobody sees.
- The LevelPlay adapter lives in its own assembly, constrained to `UNITX_LEVELPLAY`, so it
  compiles only when `com.unity.services.levelplay` (9.0.0+) is installed. It targets the
  9.x `Unity.Services.LevelPlay` namespace — the published 8.x docs still say
  `com.unity3d.mediation`, which no longer exists.

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
