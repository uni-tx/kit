# UniTx Pooling

DI-aware GameObject pooling built on UnityEngine.Pool with lifecycle hooks and typed data injection.

**Unity 6.5 (6000.5) or newer** · MIT · v1.3.0

GameObject pooling on top of `UnityEngine.Pool.ObjectPool`, adding lifecycle
hooks, typed spawn data and dependency injection for pooled items.

## Install

Unity's Package Manager **cannot resolve git dependencies declared inside a package**
([manual](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-git.html)), so this
package's siblings are not pulled in automatically. Paste the whole block into
`Packages/manifest.json` — order does not matter there, UPM resolves the set together:

```jsonc
"dependencies": {
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.3.0",
  "com.uni-tx.core": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.3.0",
  "com.uni-tx.pooling": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.pooling#pooling@1.3.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.3.0`
3. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.3.0`
4. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.pooling#pooling@1.3.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`
- **Unity registry dependencies** (resolved automatically by UPM):
  - `com.unity.test-framework` 1.4.6 (the shipped Tests/ assemblies)

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
var spawner = new UniSpawner(projectilePrefab, transform, initialCapacity: 20);
spawner.Inject(IoCStatics.Resolver);
spawner.Prewarm(20);

var projectile = spawner.Spawn<Projectile>(data, transform.position);
```

## Samples

Import from **Package Manager ▸ UniTx Pooling ▸ Samples**.

- **Projectile Pool** — A pooled projectile with Initialize/Reset hooks, typed spawn data and automatic return.

## Notes

- `Reset()` must leave no state behind. A pooled object that keeps state is the
  classic pooling bug.
- `Spawn` defaults rotation to `Quaternion.identity`; `default(Quaternion)` is
  `(0,0,0,0)`, an invalid quaternion that produces NaN transforms.
- `Prewarm` during loading so the first burst does not instantiate mid-gameplay.

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
