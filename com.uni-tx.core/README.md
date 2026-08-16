# UniTx Core

Foundation for the UniTx kit: lifecycle contracts, app bootstrap, clocks, Unity event listener, safe-area insets, scene loading and extensions.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

The foundation every other package builds on: lifecycle contracts
(`IInitializable`, `IResettable`), the `AppLoader` bootstrap, `IClock`,
`IUnityEventListener`, device safe-area insets, async scene loading, and the shared
extensions.

## Install

Unity's Package Manager **cannot resolve git dependencies declared inside a package**
([manual](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-git.html)), so this
package's siblings are not pulled in automatically. Paste the whole block into
`Packages/manifest.json` — order does not matter there, UPM resolves the set together:

```jsonc
"dependencies": {
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.1.0",
  "com.uni-tx.core": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.1.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.1.0`
3. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.1.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`
- **Unity registry dependencies** (resolved automatically): `com.unity.inputsystem` (project-wide actions drive the back button).

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
public sealed class WarmUpStep : LoadingStepBase
{
    public override async UniTask InitializeAsync(CancellationToken cToken = default)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: cToken);
        UniStatics.LogInfo("Warm.", this);
    }
}
```

Add an `AppLoader` to a scene object and drag your steps into its list, in order.

## Samples

Import from **Package Manager ▸ UniTx Core ▸ Samples**.

- **App Bootstrap** — An AppLoader driving ordered loading steps, with a custom step of your own.
- **Clock And Lifecycle** — IClock (local and server) plus IUnityEventListener update/pause/quit/back-button hooks.

## Notes

- `IUnityEventListener.OnLowMemory` surfaces `Application.lowMemory`, the only
  warning Android and iOS give before killing the process. Flush saves there before
  dropping caches.
- `UniStatics.LogInfo` is `[Conditional]`, so its arguments are never evaluated in a
  release player. Use `LogWarning`/`LogError` for anything that must survive stripping.
- The back button reads the project-wide `UI/Cancel` action. Assign an Input Actions asset
  under **Edit ▸ Project Settings ▸ Input System Package**, or `OnBackButtonPressed`
  never fires.
- `ServerClock` reads the HTTP `Date` header from any HTTPS host — no API key, no rate
  limit. It falls back to device time rather than blocking startup.
- `UniSafeArea` exposes the device safe area as normalized insets plus a change event, with
  no UI dependency — canvas UI and native ad banners both need the same numbers, and a
  banner is positioned in device pixels outside Unity's canvas entirely.

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
