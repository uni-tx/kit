# UniTx

A modular, mobile-first Unity framework shipped as **16 independent UPM packages**.
Install only the packages you need, by git URL.

**Unity 6.5 (`6000.5`) or newer** · MIT · **v1.1.0** (all packages in lockstep)

---

## Install

> Unity's Package Manager **cannot resolve git dependencies declared inside a package**
> ([manual](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-git.html)), so a
> package's `dependencies` lists Unity-registry packages only. UniTx siblings and UniTask
> are installed explicitly.

Paste the block for what you want into `Packages/manifest.json` — order does not matter
there, UPM resolves the set together. This example installs audio:

```jsonc
"dependencies": {
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.ioc":     "https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.1.0",
  "com.uni-tx.core":    "https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.1.0",
  "com.uni-tx.pooling": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.pooling#pooling@1.1.0",
  "com.uni-tx.audio":   "https://github.com/uni-tx/kit.git?path=/com.uni-tx.audio#audio@1.1.0"
}
```

Every package's README carries the exact block for its own dependency chain. If something
is missing, **UniTx ▸ Check Dependencies** in the editor reports what and prints the lines
that fix it.

---

## Packages

| Package | Does | Depends on |
|---|---|---|
| [`ioc`](com.uni-tx.ioc) | DI container: bind, resolve, scopes | — |
| [`core`](com.uni-tx.core) | Lifecycle, `AppLoader`, clocks, event listener, safe area | ioc |
| [`events`](com.uni-tx.events) | Allocation-free priority event bus | ioc, core |
| [`resources`](com.uni-tx.resources) | Async Addressables loading, download size + preload | ioc, core |
| [`pooling`](com.uni-tx.pooling) | GameObject pooling over `UnityEngine.Pool` | ioc, core |
| [`audio`](com.uni-tx.audio) | Pooled SFX + music, buses, crossfade | ioc, core, pooling |
| [`content`](com.uni-tx.content) | JSON game data by Addressables label | ioc, core, resources |
| [`serialization`](com.uni-tx.serialization) | Atomic batched JSON saves, off-thread | ioc, core |
| [`widgets`](com.uni-tx.widgets) | UI screen stack + safe-area layout | ioc, core, resources |
| [`entity`](com.uni-tx.entity) | Content joined with per-player saves | ioc, core, resources, content, serialization |
| [`sprite-loader`](com.uni-tx.sprite-loader) | Addressables sprites into uGUI `Image` | ioc, core, resources |
| [`localization`](com.uni-tx.localization) | Facade over `com.unity.localization` | ioc, core |
| [`tweening`](com.uni-tx.tweening) | Awaitable tweens with easing | — |
| [`analytics`](com.uni-tx.analytics) | SDK-agnostic analytics facade | ioc, core |
| [`ads`](com.uni-tx.ads) | SDK-agnostic ads facade (+ LevelPlay adapter) | ioc, core |
| [`bootstrap`](com.uni-tx.bootstrap) | Loading steps wiring everything together | all of the above |

```text
UniTask ─┬─ ioc ─── core ─┬─ events
         │                ├─ resources ─┬─ content ──┐
         │                │             ├─ widgets   ├─ entity
         │                │             └─ sprite-loader
         │                ├─ pooling ─── audio
         │                ├─ serialization ───────────┘
         │                ├─ localization
         │                ├─ analytics
         │                └─ ads
         └─ tweening (independent)
                              bootstrap → everything
```

---

## Conventions

- **UniTask only** — no coroutines, no `System.Threading.Tasks`, no `async void`.
- **Cancellation tokens** — every async API takes one last and forwards it.
- **Serialized fields** — `[SerializeField] private T _name;` behind a read-only property.
- **Mobile first** — allocation-conscious, safe-area aware, and responsive to
  `Application.lowMemory`.

Every package ships a README, CHANGELOG, tests, and at least one runnable sample
importable from the Package Manager.

---

## Samples

Import from **Package Manager ▸ \<package\> ▸ Samples**. They live under `Samples~`, which
Unity excludes from compilation until imported, so they add nothing to your build unless
you ask for them.

---

## Versioning

All packages move in lockstep. One tag per package, short suffix:
`core@1.1.0`, `audio@1.1.0`, …

## License

[MIT](com.uni-tx.core/LICENSE.md)
