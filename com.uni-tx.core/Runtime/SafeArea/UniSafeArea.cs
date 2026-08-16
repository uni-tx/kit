using System;
using UnityEngine;

namespace UniTx.Core
{
    /// <summary>
    /// The device's safe area, as insets, with a change notification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately has no UI dependency. Canvas UI is only half the problem: an ad banner
    /// is a native view positioned in device pixels, outside Unity's canvas entirely, and it
    /// needs the same numbers. Both consumers read them from here rather than each computing
    /// their own.
    /// </para>
    /// <para>
    /// <b>uGUI 2.6.0 ships a <c>UnityEngine.UI.SafeArea</c> component</b> that covers the
    /// canvas case natively. It is not usable here: core packages are pinned to the editor
    /// version, and Unity 6.5 (6000.5) only offers uGUI 2.5.0. On Unity 6.6+ prefer Unity's
    /// component for canvas UI — <c>SafeAreaFitter</c> in <c>com.uni-tx.widgets</c>
    /// deliberately mirrors its shape so the swap is a component change.
    /// </para>
    /// <para>
    /// Values come from <see cref="Screen.safeArea"/>, whose origin is the bottom-left
    /// corner. Note that with <b>Render outside safe area</b> disabled on Android, Unity
    /// resizes the player window to the safe region and this reports no insets at all.
    /// </para>
    /// </remarks>
    public static class UniSafeArea
    {
        private static Rect _lastSafeArea;
        private static int _lastWidth;
        private static int _lastHeight;
        private static ScreenOrientation _lastOrientation;
        private static bool _isPolling;

        /// <summary>
        /// Raised when the safe area changes — rotation, resolution change, or a foldable opening.
        /// </summary>
        public static event Action<SafeAreaInsets> OnChanged;

        /// <summary>
        /// Gets the current insets, normalized to 0..1 of each screen dimension.
        /// </summary>
        public static SafeAreaInsets Insets { get; private set; } = SafeAreaInsets.Zero;

        /// <summary>
        /// Gets the raw safe-area rectangle in pixels, origin bottom-left.
        /// </summary>
        public static Rect SafeArea => Screen.safeArea;

        /// <summary>
        /// Indicates whether any edge is currently obscured.
        /// </summary>
        public static bool HasInsets => !Insets.IsZero;

        /// <summary>
        /// Gets the screen areas that display nothing at all, in pixels.
        /// </summary>
        /// <remarks>
        /// A notch or punch-hole sits inside the bounding safe area, so a layout that only
        /// respects <see cref="Insets"/> can still place something under a punch-hole in the
        /// middle of the top edge. Use this when exact placement matters.
        /// </remarks>
        public static Rect[] Cutouts => Screen.cutouts;

        /// <summary>
        /// Starts watching for safe-area changes.
        /// </summary>
        /// <param name="listener">Lifecycle source driving the check each frame.</param>
        /// <remarks>
        /// Bootstrap calls this. Without a listener the insets are still correct at the
        /// moment they are read, but nothing is raised when the device rotates.
        /// </remarks>
        public static void Initialize(IUnityEventListener listener)
        {
            Refresh();

            if (listener == null || _isPolling) return;

            // Unity raises no event for a safe-area change, so it has to be polled. The
            // check is four field comparisons and only allocates when something actually
            // changed, which is cheap enough to run per frame.
            listener.OnUpdate += Refresh;
            _isPolling = true;
        }

        /// <summary>
        /// Indicates whether a lifecycle listener is currently driving the poll.
        /// </summary>
        /// <remarks>
        /// A component that lays itself out from the safe area should poll on its own when
        /// this is false, so it still responds to rotation in a scene with no bootstrap.
        /// </remarks>
        public static bool IsPolling => _isPolling;

        /// <summary>
        /// Stops watching for safe-area changes.
        /// </summary>
        /// <param name="listener">The lifecycle source passed to <see cref="Initialize"/>.</param>
        /// <remarks>
        /// Detaches the poll but deliberately leaves <see cref="OnChanged"/> subscribers
        /// alone. Persistent UI usually outlives the bootstrap object that started the poll
        /// — clearing subscribers here silently stopped a live <c>SafeAreaFitter</c> on a
        /// DontDestroyOnLoad canvas from ever updating again, because it only subscribes in
        /// <c>OnEnable</c>. Subscribers are cleared for real at
        /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>.
        /// </remarks>
        public static void Reset(IUnityEventListener listener)
        {
            if (listener != null && _isPolling) listener.OnUpdate -= Refresh;

            _isPolling = false;
        }

        /// <summary>
        /// Recomputes the insets now and raises <see cref="OnChanged"/> if they moved.
        /// </summary>
        public static void Refresh()
        {
            var safeArea = Screen.safeArea;
            var width = Screen.width;
            var height = Screen.height;
            var orientation = Screen.orientation;

            if (safeArea == _lastSafeArea &&
                width == _lastWidth &&
                height == _lastHeight &&
                orientation == _lastOrientation)
            {
                return;
            }

            _lastSafeArea = safeArea;
            _lastWidth = width;
            _lastHeight = height;
            _lastOrientation = orientation;

            // Guard against a zero-sized screen, which happens for a frame while the app is
            // backgrounded on Android and would otherwise divide by zero.
            if (width <= 0 || height <= 0) return;

            Insets = new SafeAreaInsets(
                left: safeArea.xMin / width,
                right: (width - safeArea.xMax) / width,
                bottom: safeArea.yMin / height,
                top: (height - safeArea.yMax) / height);

            OnChanged.SafeInvoke(Insets);
        }

        /// <summary>
        /// Overrides the insets, for testing and for previewing a device in the editor.
        /// </summary>
        /// <param name="insets">Insets to report until the next <see cref="Refresh"/>.</param>
        /// <remarks>
        /// The editor Game view reports no safe area, so notch layouts are otherwise only
        /// verifiable on a device. This makes them testable and previewable.
        /// </remarks>
        public static void SetOverride(SafeAreaInsets insets)
        {
            Insets = insets;
            OnChanged.SafeInvoke(Insets);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnChanged = null;
            Insets = SafeAreaInsets.Zero;
            _lastSafeArea = default;
            _lastWidth = 0;
            _lastHeight = 0;
            _lastOrientation = ScreenOrientation.AutoRotation;
            _isPolling = false;
        }
    }
}
