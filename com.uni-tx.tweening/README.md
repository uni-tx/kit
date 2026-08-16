# UniTx Tweening

Allocation-light UniTask tween engine for transforms, colors and arbitrary values, with easing and awaitable sequencing.

**Unity 6.5 (6000.5) or newer** · MIT · v1.1.0

Awaitable tweens for transforms, colors, alpha and arbitrary values. Sequencing
is `await`, parallelism is `UniTask.WhenAll` — there is no sequence type to learn.

## Install

Unity's Package Manager **cannot resolve git dependencies declared inside a package**
([manual](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-git.html)), so this
package's siblings are not pulled in automatically. Paste the whole block into
`Packages/manifest.json` — order does not matter there, UPM resolves the set together:

```jsonc
"dependencies": {
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
  "com.uni-tx.tweening": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.tweening#tweening@1.2.0"
}
```

<details>
<summary>Or add them one at a time via <b>Add package from git URL</b></summary>

Use this exact order — dependencies before dependents, or the editor throws transient
compile errors between adds:

1. `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`
2. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.tweening#tweening@1.2.0`

</details>

- **UniTx dependencies:** none
- **Unity registry dependencies** (resolved automatically): `com.unity.modules.ui` (CanvasGroup).

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
await UniTween.MoveAsync(transform, target, 0.3f, Ease.OutBack, cToken: token);

await UniTask.WhenAll(
    UniTween.ScaleAsync(transform, Vector3.one, 0.2f, cToken: token),
    UniTween.FadeAsync(panel, 1f, 0.2f, unscaledTime: true, cToken: token));
```

## Samples

Import from **Package Manager ▸ UniTx Tweening ▸ Samples**.

- **Tween Gallery** — Move/scale/rotate/fade tweens, custom value tweens, easing curves and awaited sequences.

## Notes

- Always pass a token — `this.GetCancellationTokenOnDestroy()` is usually right.
  A tween whose target is destroyed mid-flight otherwise writes to a dead transform.
- Use `unscaledTime: true` for menu animations, or they freeze at `Time.timeScale = 0`.
- `PunchScaleAsync` and `ShakeAsync` restore the original transform even when cancelled.

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
