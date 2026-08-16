# UniTx IoC

Lightweight, allocation-conscious inversion-of-control container for Unity 6.5+.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

A small container: bind a concrete type, resolve it by any interface it implements.
Singleton and transient scopes, bulk `ResolveAll` passes, and explicit `Inject` calls
rather than constructor injection — so a service declares its dependencies in one place
and the container never has to reason about construction order.

## Install

Unity's Package Manager **cannot resolve git dependencies declared inside a package**
([manual](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-git.html)), so this
package's siblings are not pulled in automatically. Paste the whole block into
`Packages/manifest.json` — order does not matter there, UPM resolves the set together:

```jsonc
"dependencies": {
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.1.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.1.0`

</details>

- **UniTx dependencies:** none
- **Unity registry dependencies:** none

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
IoCStatics.Binder.Bind<ScoreService>().AsSingleton().Conclude();

var scores = IoCStatics.Resolver.Resolve<IScoreService>();

// Optional dependencies read as a feature toggle rather than an exception.
if (IoCStatics.Resolver.TryResolve<IAnalytics>(out var analytics)) analytics.Report("ready");
```

## Samples

Import from **Package Manager ▸ UniTx IoC ▸ Samples**.

- **Basic Container** — Binding, resolving and unbinding services, including ResolveAll bulk passes.


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
