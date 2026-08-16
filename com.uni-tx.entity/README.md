# UniTx Entity

Runtime entities that combine static content data with persisted saved data.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

Runtime entities that join static content data with per-player saved data, so a
balance patch and a player's progress stay independent.

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
  "com.uni-tx.resources": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.resources#resources@1.2.0",
  "com.uni-tx.content": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.2.0",
  "com.uni-tx.serialization": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.2.0",
  "com.uni-tx.entity": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.2.0"
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
5. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.2.0`
6. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.serialization#serialization@1.2.0`
7. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.entity#entity@1.2.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`, `com.uni-tx.resources`, `com.uni-tx.content`, `com.uni-tx.serialization`
- **Unity registry dependencies:** none

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
public sealed class Hero : EntityBase<HeroData, HeroSavedData>
{
    public Hero(string id) : base(id) { }

    public int Attack => Data.BaseAttack + (SavedData.Level - 1) * 2;

    protected override void OnInject(IResolver resolver) { }
    protected override void OnInit() { }
    protected override void OnReset() { }
}
```

## Samples

Import from **Package Manager ▸ UniTx Entity ▸ Samples**.

- **Entity Demo** — An entity backed by content data and a save file, wired through the entity service.

## Notes

- Content ships with the build; saved data belongs to the player. Keeping them apart
  is what lets a balance patch ship without rewriting player progress.
- `LoadEntities` builds one entity per `IEntityData` in content, so content must be
  loaded first.

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
