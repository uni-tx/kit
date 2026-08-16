# UniTx Events

Zero-allocation, priority-ordered event bus for value-type events.

**Unity 6.5 (6000.5) or newer** · MIT · v1.3.0

A priority-ordered event bus for value-type events. Dispatch is allocation-free
— listeners live in per-type typed arrays, so nothing is boxed on subscribe and nothing is
copied on raise. Unsubscribing from inside a handler is safe.

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
  "com.uni-tx.events": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.events#events@1.3.0"
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

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`
- **Unity registry dependencies** (resolved automatically by UPM):
  - `com.unity.test-framework` 1.4.6 (the shipped Tests/ assemblies)

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
public readonly struct CoinCollected : IEvent
{
    public readonly int Value;
    public CoinCollected(int value) => Value = value;
}

UniEvents.Subscribe<CoinCollected>(e => _coins += e.Value);
UniEvents.Raise(new CoinCollected(10));
UniEvents.Unsubscribe<CoinCollected>(Handler);   // always, in OnDisable
```

## Samples

Import from **Package Manager ▸ UniTx Events ▸ Samples**.

- **Event Bus Basics** — Publishing and subscribing struct events, priority ordering and safe unsubscribe during dispatch.

## Notes

- Events must be `struct`. That is what keeps dispatch allocation-free.
- Always unsubscribe in `OnDisable`. The bus holds the delegate, the delegate holds your
  component, and a missed unsubscribe keeps the whole object alive across scene loads.
- `SubscriberCount<T>()` is a cheap leak check.

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
