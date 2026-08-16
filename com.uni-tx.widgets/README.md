# UniTx Widgets

Stack-based UI widget manager with async push/pop, typed data, Addressables prefab mapping and device safe-area layout.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

A stack-based UI screen manager: push and pop widgets asynchronously, hand them
typed data, and resolve their prefabs through Addressables. Includes `SafeAreaFitter` for
laying UI out around notches, punch-holes and the home indicator.

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
  "com.uni-tx.widgets": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.widgets#widgets@1.1.0"
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
5. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.widgets#widgets@1.1.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`, `com.uni-tx.resources`
- **Unity registry dependencies:** none

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
await UniWidgets.PushAsync<MainMenuWidget>(token);
await UniWidgets.PushAsync<ConfirmDialogWidget>(new ConfirmDialogData { Message = "Sure?" }, token);

await UniWidgets.PopAsync(token);
```

## Samples

Import from **Package Manager ▸ UniTx Widgets ▸ Samples**.

- **Widget Stack** — Pushing and popping screens with typed data, back-button integration and stack queries.
- **Safe Area Layout** — Keeping UI clear of notches, punch-holes and the home indicator, including landscape balancing.

## Notes

- Requires `UniTxConfig.WidgetsAssetDataKey` to point at an Addressable
  `AssetData` asset mapping widget **type names** to prefabs.
- Wire `IUnityEventListener.OnBackButtonPressed` to `PopAsync` so the hardware back
  button navigates instead of quitting.
- **Safe area:** put one `SafeAreaFitter` on a single full-screen container and parent
  interactive UI to it. Keep backgrounds outside, or they letterbox themselves. Enable
  **Player Settings ▸ Android ▸ Render outside safe area**, otherwise Unity shrinks the
  window to the safe region and reports no insets at all.
- On **Unity 6.6+** prefer uGUI's own `UnityEngine.UI.SafeArea` (uGUI 2.6.0).
  `SafeAreaFitter` exists because core packages are pinned to the editor version and 6.5
  only ships uGUI 2.5.0; the inspector shape matches Unity's so migrating is a swap.

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
