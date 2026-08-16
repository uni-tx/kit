# UniTx Audio

Pool-based SFX and music playback with AudioMixer-driven volume, ducking and per-bus mute.

**Unity 6.5 (6000.5) or newer** · MIT · v1.3.0

Pooled sound effects and a dedicated music source, with independent SFX and music
buses, crossfading, and pause/resume wired to the application lifecycle.

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
  "com.uni-tx.pooling": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.pooling#pooling@1.3.0",
  "com.uni-tx.audio": "https://github.com/uni-tx/kit.git?path=/com.uni-tx.audio#audio@1.3.0"
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
5. `https://github.com/uni-tx/kit.git?path=/com.uni-tx.audio#audio@1.3.0`

</details>

- **UniTx dependencies:** `com.uni-tx.ioc`, `com.uni-tx.core`, `com.uni-tx.pooling`
- **Unity registry dependencies** (resolved automatically by UPM):
  - `com.unity.modules.audio` 1.0.0 (AudioSource, AudioMixer)
  - `com.unity.test-framework` 1.4.6 (the shipped Tests/ assemblies)

> `com.uni-tx.core` ships a dependency doctor that reports exactly which packages are
> missing, so a partial install fails with an explanation rather than a wall of
> `CS0246`.

## Quick start

```csharp
UniAudio.Play2D(_uiClick);
UniAudio.Play3D(_explosion, hitPoint);
await UniAudio.PlayMusicAsync(_battleTheme, fadeDuration: 1.5f, token);

UniAudio.SetSfxVolume(0.8f);
```

## Samples

Import from **Package Manager ▸ UniTx Audio ▸ Samples**.

- **Audio Playback** — 2D/3D/attached SFX, music with crossfade, and mixer-backed volume and mute controls.

## Notes

- Mobile mixes ~32 simultaneous voices, so the SFX pool is capped at 24; beyond that,
  released sources are destroyed rather than retained.
- Effects pause with the app: the return countdown freezes, so a backgrounded game does not
  recycle sounds the player has not heard yet.
- Bus volume is applied on top of each clip's own volume and recomputed from it, so
  repeated volume changes cannot compound toward silence.
- Call `PauseAll`/`ResumeAll` from `IUnityEventListener.OnPause` — `UniTxStep` wires
  this for you.

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
