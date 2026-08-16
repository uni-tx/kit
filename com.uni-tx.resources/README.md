# UniTx Resources

Unified async asset loading over Addressables, with progress, cancellation and grouped release.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

Async asset loading over Addressables with progress, cancellation and grouped
release. Handle-to-UniTask conversion comes from UniTask's own `UniTask.Addressables`
assembly rather than a hand-rolled bridge.

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
  "com.uni-tx.resources": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.2.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.2.0`
3. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.2.0`
4. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.2.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`
- **Unity registry dependencies** (resolved automatically): `com.unity.addressables`.

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
var sprite = await UniResources.LoadAssetAsync<Sprite>("Icons/Coin", cToken: token);
var enemies = await UniResources.LoadAssetGroupAsync<GameObject>(new[] { "level-01" }, cToken: token);

UniResources.DisposeAsset(sprite);
UniResources.DisposeAssetGroup(enemies);
```

## Samples

Import from **Package Manager ▸ UniTx Resources ▸ Samples**.

- **Load And Release** — Loading single assets, asset groups by label, and instantiating prefabs with progress and cancellation.

## Notes

- `GetDownloadSizeAsync` + `PreloadAsync` for remote content. Check the size
  and download with progress before first use, or the game stalls on cellular with no way
  to decline.
- Addressables reference-counts. Every `LoadAssetAsync` needs a matching
  `DisposeAsset`, or the asset stays resident for the session.
- `ResetAsync` deliberately does **not** clear the download cache. Use
  `ClearDownloadCacheAsync` when you really want to force a re-download.

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
