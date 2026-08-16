# UniTx Bootstrap

Concrete loading steps that wire the whole UniTx kit together in the right order.

**Unity 6.5 (6000.5) or newer** · MIT · v1.3.0

The concrete loading steps that wire the whole kit together in the right
order. Installing this installs everything.

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
  "com.uni-tx.events": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.3.0",
  "com.uni-tx.resources": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.3.0",
  "com.uni-tx.pooling": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.pooling#pooling@1.3.0",
  "com.uni-tx.audio": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.audio#audio@1.3.0",
  "com.uni-tx.content": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.3.0",
  "com.uni-tx.serialization": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.3.0",
  "com.uni-tx.widgets": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.widgets#widgets@1.3.0",
  "com.uni-tx.entity": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.3.0",
  "com.uni-tx.sprite-loader": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.sprite-loader#sprite-loader@1.3.0",
  "com.uni-tx.bootstrap": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.bootstrap#bootstrap@1.3.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.3.0`
3. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.3.0`
4. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.3.0`
5. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.3.0`
6. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.pooling#pooling@1.3.0`
7. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.audio#audio@1.3.0`
8. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.3.0`
9. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.3.0`
10. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.widgets#widgets@1.3.0`
11. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.3.0`
12. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.sprite-loader#sprite-loader@1.3.0`
13. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.bootstrap#bootstrap@1.3.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`, `com.uni-tx.events`, `com.uni-tx.resources`, `com.uni-tx.pooling`, `com.uni-tx.audio`, `com.uni-tx.content`, `com.uni-tx.serialization`, `com.uni-tx.widgets`, `com.uni-tx.entity`, `com.uni-tx.sprite-loader`
- **Unity registry dependencies** (resolved automatically by UPM):
  - `com.unity.test-framework` 1.4.6 (the shipped Tests/ assemblies)

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

Scene setup — one GameObject with `AppLoader`, and these steps in its list:

1. `UniTxStep` — config, root object, events, resources, widgets, audio
2. `BindDependenciesStep` — binds the kit's services
3. `InitDependenciesStep` — injects, then initializes everything bound
4. your own steps

## Samples

Import from **Package Manager ▸ UniTx Bootstrap ▸ Samples**.

- **Full Bootstrap** — A complete AppLoader scene wiring config, IoC bindings and every UniTx service.

## Notes

- Step order matters: nothing can be injected before it is bound, and nothing
  should be initialized before its dependencies are injected.
- `BindDependenciesStep` can bind `ServerClock` instead of `LocalClock` via its inspector
  toggle.

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
