using UniTx.Core;
using UniTx.Widgets;
using UnityEngine;

namespace UniTx.Widgets.Samples
{
    /// <summary>
    /// Laying UI out around notches, punch-holes and the home indicator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scene setup.</b> The rule is one safe-area container, not many:
    /// </para>
    /// <code>
    /// Canvas (Screen Space - Overlay)
    ///  ├─ Background          ← full-bleed art, deliberately OUTSIDE the safe area
    ///  └─ SafeAreaRoot        ← SafeAreaFitter lives here
    ///      ├─ TopBar
    ///      ├─ Content
    ///      └─ BottomBar
    /// </code>
    /// <para>
    /// Put every interactive element under <c>SafeAreaRoot</c>. Backgrounds stay outside so
    /// they still reach the screen edges — insetting them leaves visible letterboxing.
    /// </para>
    /// <para>
    /// Also enable <b>Player Settings ▸ Android ▸ Render outside safe area</b>. With it off,
    /// Unity shrinks the player window to the safe region and reports no insets at all, so
    /// the game letterboxes itself instead of using the full display.
    /// </para>
    /// <para>
    /// <b>On Unity 6.6+</b> prefer uGUI's own <c>UnityEngine.UI.SafeArea</c> component
    /// (uGUI 2.6.0). <see cref="SafeAreaFitter"/> exists because core packages are pinned to
    /// the editor version and Unity 6.5 only ships uGUI 2.5.0.
    /// </para>
    /// </remarks>
    public sealed class SafeAreaLayoutSample : MonoBehaviour
    {
        [Header("Containers")]
        [Tooltip("Full-screen container holding every interactive element.")]
        [SerializeField] private SafeAreaFitter _safeAreaRoot;

        [Tooltip("A bottom bar that should reach the screen edge horizontally but still " +
                 "clear the home indicator vertically.")]
        [SerializeField] private SafeAreaFitter _bottomBar;

        private void Start()
        {
            // Only inset the edge this element actually touches. Insetting all four would
            // leave visible gutters down the sides of a full-width bar.
            if (_bottomBar != null) _bottomBar.SetEdges(SafeAreaEdges.Bottom);

            // In landscape the notch sits on one side, so a container inset on that side
            // only ends up visibly off-centre. Balancing mirrors the larger inset onto the
            // opposite edge — costs a little width, keeps the composition centred.
            if (_safeAreaRoot != null) _safeAreaRoot.SetEdges(SafeAreaEdges.All);

            UniSafeArea.OnChanged += HandleSafeAreaChanged;
            LogCurrentInsets();
        }

        private void OnDestroy() => UniSafeArea.OnChanged -= HandleSafeAreaChanged;

        private static void HandleSafeAreaChanged(SafeAreaInsets insets)
            // Fires on rotation, resolution change, or a foldable opening.
            => Debug.Log($"Safe area changed: {insets}");

        private static void LogCurrentInsets()
        {
            var insets = UniSafeArea.Insets;

            Debug.Log(insets.IsZero
                ? "No safe-area insets — a device without a cutout, or the editor Game view."
                : $"Insets {insets}");

            // The bounding safe area does not describe a punch-hole sitting *inside* the top
            // edge, so anything that must dodge the camera exactly needs the cutout rects.
            foreach (var cutout in UniSafeArea.Cutouts)
            {
                Debug.Log($"Cutout at {cutout}");
            }
        }

        /// <summary>
        /// Previews a notch in the editor, where the Game view reports no safe area.
        /// </summary>
        [ContextMenu("Preview iPhone-style Insets")]
        public void PreviewNotch()
            // Roughly a Dynamic Island phone in portrait: a tall top inset and a home
            // indicator at the bottom.
            => UniSafeArea.SetOverride(new SafeAreaInsets(0f, 0f, 0.04f, 0.06f));

        /// <summary>
        /// Previews a landscape notch on the left edge only.
        /// </summary>
        [ContextMenu("Preview Landscape Notch")]
        public void PreviewLandscapeNotch()
            => UniSafeArea.SetOverride(new SafeAreaInsets(0.06f, 0f, 0.03f, 0f));

        /// <summary>
        /// Clears the preview and returns to what the device actually reports.
        /// </summary>
        [ContextMenu("Clear Preview")]
        public void ClearPreview()
        {
            UniSafeArea.SetOverride(SafeAreaInsets.Zero);
            UniSafeArea.Refresh();
        }
    }
}
