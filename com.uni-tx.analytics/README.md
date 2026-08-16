# UniTx Analytics

SDK-agnostic analytics facade with batching and a debug provider; adapters plug in behind version defines.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

An SDK-agnostic analytics facade. Events fan out to every registered provider,
consent is gated centrally, and a debug provider ships in the box.

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
  "com.uni-tx.analytics": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.analytics#analytics@1.1.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.1.0`
3. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.1.0`
4. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.analytics#analytics@1.1.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`
- **Unity registry dependencies:** none

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
UniAnalytics.Register(new DebugAnalyticsProvider());
await UniAnalytics.InitializeAsync(token);
UniAnalytics.SetConsent(true);

UniAnalytics.Track("level_start", "level", 1);
```

## Samples

Import from **Package Manager ▸ UniTx Analytics ▸ Samples**.

- **Analytics Providers** — Logging typed events through the facade, and writing a custom provider adapter.

## Notes

- No SDK dependency. Register an adapter per backend; a project with none still
  runs and events go nowhere.
- Consent is gated in the facade, so a provider that forgets to honour it cannot leak.
- Flush on `OnPause` — on mobile that is the last callback before the OS may kill the app.

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
