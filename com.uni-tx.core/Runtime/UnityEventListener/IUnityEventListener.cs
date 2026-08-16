using System;

namespace UniTx.Core
{
    /// <summary>
    /// Broadcasts common Unity lifecycle events so plain C# services can observe them
    /// without each becoming a MonoBehaviour.
    /// </summary>
    /// <remarks>
    /// One hidden scene object drives every subscriber, so a hundred listeners cost one
    /// <c>Update</c> callback instead of a hundred — the usual reason a mobile frame
    /// budget disappears into script overhead.
    /// </remarks>
    public interface IUnityEventListener
    {
        /// <summary>
        /// Invoked every frame during the <c>Update</c> phase.
        /// </summary>
        event Action OnUpdate;

        /// <summary>
        /// Invoked every frame during the <c>LateUpdate</c> phase.
        /// </summary>
        event Action OnLateUpdate;

        /// <summary>
        /// Invoked every physics step during the <c>FixedUpdate</c> phase.
        /// </summary>
        event Action OnFixedUpdate;

        /// <summary>
        /// Invoked when the application is paused (<c>true</c>) or resumed (<c>false</c>).
        /// </summary>
        /// <remarks>
        /// On mobile this is the last reliable callback before the process can be killed —
        /// flush saves here rather than in <see cref="OnQuit"/>.
        /// </remarks>
        event Action<bool> OnPause;

        /// <summary>
        /// Invoked when the application gains (<c>true</c>) or loses (<c>false</c>) focus.
        /// </summary>
        event Action<bool> OnFocus;

        /// <summary>
        /// Invoked when the application is quitting.
        /// </summary>
        /// <remarks>
        /// Not guaranteed on mobile, where the OS may terminate the process without it.
        /// </remarks>
        event Action OnQuit;

        /// <summary>
        /// Invoked when the OS reports memory pressure.
        /// </summary>
        /// <remarks>
        /// On Android and iOS this is the last warning before the process is killed to
        /// reclaim memory. Drop caches, release unused assets and flush saves here — a
        /// player who gets killed mid-session loses whatever was still buffered, and on iOS
        /// repeated terminations are what users describe as "the game keeps crashing".
        /// </remarks>
        event Action OnLowMemory;

        /// <summary>
        /// Invoked when the back button (project-wide <c>UI/Cancel</c>) is pressed.
        /// </summary>
        event Action OnBackButtonPressed;
    }
}
