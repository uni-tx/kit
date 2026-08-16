# UniTx Content

JSON-driven static game data: register types to files, load by Addressables label, query by key or type.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

Static game data as JSON: bind a file name to a type, load by Addressables
label, then query by id or by type.

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
  "com.uni-tx.content": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.content#content@1.2.0"
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

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`, `com.uni-tx.resources`
- **Unity registry dependencies:** none

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
ContentRegistry.Register<WeaponData>("weapons");
await content.LoadContentAsync(new[] { "content" }, token);

var pistol = content.GetData<WeaponData>("weapon_pistol");
var all = content.GetAllData<WeaponData>();
```

## Samples

Import from **Package Manager ▸ UniTx Content ▸ Samples**.

- **Content Catalog** — Registering data types, loading a JSON catalog by label, and querying it by id and by type.

## Notes

- `JsonUtility` maps **fields**, not properties. Anything that must load needs a
  serialized field.
- The Addressable asset's name must match the registered file name exactly, or the file is
  skipped with a warning.

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
