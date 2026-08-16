# UniTx Serialization

Batched, atomic JSON save/load to persistent storage with flush-on-pause and schema migration.

**Unity 6.5 (6000.5) or newer** · MIT · v1.3.0

Batched JSON saves written atomically to persistent storage, flushed
automatically on pause and quit.

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
  "com.uni-tx.serialization": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.3.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.ioc#ioc@1.3.0`
3. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.core#core@1.3.0`
4. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.3.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`
- **Unity registry dependencies** (resolved automatically by UPM):
  - `com.unity.modules.jsonserialize` 1.0.0 (JsonUtility)
  - `com.unity.test-framework` 1.4.6 (the shipped Tests/ assemblies)

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
var progress = saves.Load<PlayerProgress>("player-progress");
progress.AddCoins(25);

saves.Save(progress);   // queued for the next batch
saves.Flush();          // write now — use before a purchase or level completion
```

## Samples

Import from **Package Manager ▸ UniTx Serialization ▸ Samples**.

- **Save Load** — Defining saved data, batched autosave, forced flush, and migrating an older save version.

## Notes

- The periodic autosave writes off the main thread; `Flush` stays
  blocking because pause/quit/low-memory may not survive an await. A few KB to mobile flash
  is tens of milliseconds — a visible hitch mid-frame.
- `JsonUtility` maps **fields**. Auto-properties are silently dropped — the
  usual cause of "my save keeps resetting".
- Writes are atomic (temp file + `File.Replace`) and keep a `.bak` that `Load` falls
  back to, so an interrupted write costs the last batch rather than the whole save.
- `Flush()` before anything a player would file a bug about losing.

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
