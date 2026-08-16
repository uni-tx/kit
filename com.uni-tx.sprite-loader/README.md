# UniTx Sprite Loader

Addressables sprite loading for uGUI Image components, with automatic release on destroy.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

Loads an Addressables sprite into a uGUI `Image`, cancelling superseded
loads and releasing the previous sprite once the new one arrives.

## Install

Unity's Package Manager **cannot resolve git dependencies declared inside a package**
([manual](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-git.html)), so this
package's siblings are not pulled in automatically. Paste the whole block into
`Packages/manifest.json` — order does not matter there, UPM resolves the set together:

```jsonc
"dependencies": {
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.1.0",
  "com.uni-tx.core": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.1.0",
  "com.uni-tx.resources": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.1.0",
  "com.uni-tx.sprite-loader": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.sprite-loader#sprite-loader@1.1.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.1.0`
3. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.1.0`
4. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.1.0`
5. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.sprite-loader#sprite-loader@1.1.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`, `com.uni-tx.resources`
- **Unity registry dependencies** (resolved automatically): `com.unity.ugui` and `com.unity.addressables`.

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
// Format on the component: "Icons/{0}"
await _iconLoader.LoadSpriteAsync(new[] { "sword" }, token);

_iconLoader.UnloadSprite();
```

## Samples

Import from **Package Manager ▸ UniTx Sprite Loader ▸ Samples**.

- **Sprite Loading** — Loading a formatted addressable sprite key into an Image and releasing it cleanly.


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
