using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UniTx.Core
{
    /// <summary>
    /// Global access points and platform helpers for the UniTx kit.
    /// </summary>
    public static class UniStatics
    {
        /// <summary>
        /// Gets or sets the root GameObject that persists across scenes.
        /// </summary>
        public static GameObject Root { get; set; }

        /// <summary>
        /// Gets or sets the active UniTx configuration asset.
        /// </summary>
        public static UniTxConfig Config { get; set; }

        /// <summary>
        /// Indicates whether the application is running in the Unity Editor.
        /// </summary>
        public static bool IsEditor =>
#if UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>
        /// Indicates whether verbose <see cref="LogInfo"/> output is compiled in.
        /// </summary>
        /// <remarks>
        /// Informational logging is stripped from release players, so this is false there.
        /// </remarks>
        public static bool IsVerboseLoggingEnabled =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        /// <summary>
        /// Indicates whether the application is running on Android.
        /// </summary>
        public static bool IsAndroid => Application.platform == RuntimePlatform.Android;

        /// <summary>
        /// Indicates whether the application is running on iOS.
        /// </summary>
        public static bool IsIOS => Application.platform == RuntimePlatform.IPhonePlayer;

        /// <summary>
        /// Indicates whether the application is running on a mobile platform (Android or iOS).
        /// </summary>
        public static bool IsMobile => IsAndroid || IsIOS;

        /// <summary>
        /// Logs an informational message. Compiled out of release players.
        /// </summary>
        /// <param name="msg">The message object to log.</param>
        /// <param name="ctx">Optional context object; its type name prefixes the message.</param>
        /// <param name="color">Optional editor console color. Ignored in players.</param>
        /// <remarks>
        /// Marked <see cref="ConditionalAttribute"/>, so in a release player the call site
        /// disappears entirely and the arguments are never evaluated — no string
        /// interpolation, no boxing, no allocation. Keeps per-frame logging free in
        /// shipped builds, which matters under the playable-ads budget.
        /// </remarks>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void LogInfo(object msg, object ctx = null, Color color = default)
            => Debug.Log(Format(msg, ctx, color), ctx as Object);

        /// <summary>
        /// Logs a warning. Kept in release players.
        /// </summary>
        /// <param name="msg">The message object to log.</param>
        /// <param name="ctx">Optional context object; its type name prefixes the message.</param>
        [HideInCallstack]
        public static void LogWarning(object msg, object ctx = null)
            => Debug.LogWarning(Format(msg, ctx, Color.yellow), ctx as Object);

        /// <summary>
        /// Logs an error. Kept in release players.
        /// </summary>
        /// <param name="msg">The message object to log.</param>
        /// <param name="ctx">Optional context object; its type name prefixes the message.</param>
        [HideInCallstack]
        public static void LogError(object msg, object ctx = null)
            => Debug.LogError(Format(msg, ctx, Color.red), ctx as Object);

        /// <summary>
        /// Logs an exception with its full stack trace. Kept in release players.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="ctx">Optional context object used for click-to-select in the console.</param>
        [HideInCallstack]
        public static void LogException(System.Exception exception, object ctx = null)
            => Debug.LogException(exception, ctx as Object);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string Format(object msg, object ctx, Color color)
        {
            var prefix = ctx == null ? "[UniTx]" : $"[{ctx.GetType().Name}]";
            var line = $"{prefix} {msg}";

            // Rich-text color tags are console-only; in a player log they are literal
            // noise in logcat/console output, so only apply them in the editor.
            return IsEditor ? line.WithColor(color == default ? Color.white : color) : line;
        }

        /// <summary>
        /// Resets kit statics before each play session.
        /// </summary>
        /// <remarks>
        /// Runs at <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>, the first
        /// callback of a play session. With <b>Enter Play Mode Options ▸ Reload Domain</b>
        /// disabled — the default fast-enter setup in Unity 6 — statics survive between
        /// sessions, so leftover state from the previous run would leak in. Clearing here
        /// makes behaviour identical with and without domain reload.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Root = null;
            Config = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadDefaultConfig()
        {
            // A UniTxStep in the scene assigns the config explicitly; this is the fallback
            // for projects that keep one at Resources/UniTxConfig instead.
            Config ??= Resources.Load<UniTxConfig>(UniTxConfig.DefaultResourcePath);
        }
    }
}
