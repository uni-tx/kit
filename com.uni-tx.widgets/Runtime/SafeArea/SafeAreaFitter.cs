using UniTx.Core;
using UnityEngine;

namespace UniTx.Widgets
{
    /// <summary>
    /// Drives a <see cref="RectTransform"/> to fit inside the device's safe area.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Put this on a single full-screen container and parent your interactive UI to it.
    /// Backgrounds and full-bleed art stay outside, so they still reach the screen edges.
    /// </para>
    /// <para>
    /// <b>On Unity 6.6+ prefer uGUI's own <c>UnityEngine.UI.SafeArea</c></b> (uGUI 2.6.0).
    /// This exists because core packages are pinned to the editor version and Unity 6.5
    /// only offers uGUI 2.5.0. The inspector shape here deliberately matches Unity's —
    /// per-edge insets, a reference orientation, and balance/centering — so migrating is a
    /// component swap rather than a re-layout.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UniTx/Safe Area Fitter")]
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Which edges to inset. Clear an edge to let content bleed off that side — " +
                 "a bottom bar that should reach the screen edge, for example.")]
        [SerializeField] private SafeAreaEdges _edges = SafeAreaEdges.All;

        [Tooltip("Mirror the larger horizontal inset onto the opposite edge so a landscape " +
                 "notch does not shift the layout off-centre.")]
        [SerializeField] private bool _balanceHorizontally;

        [Tooltip("Mirror the larger vertical inset onto the opposite edge.")]
        [SerializeField] private bool _balanceVertically;

        [Header("Editor preview")]
        [Tooltip("Simulate insets in the Game view, which reports no safe area. Play-mode " +
                 "only; never applied in a build.")]
        [SerializeField] private bool _previewInEditor;

        [SerializeField, Range(0f, 0.2f)] private float _previewTop = 0.05f;
        [SerializeField, Range(0f, 0.2f)] private float _previewBottom = 0.03f;

        private RectTransform _rectTransform;
        private SafeAreaInsets _appliedInsets;
        private bool _hasApplied;

        /// <summary>
        /// Gets the edges this fitter insets.
        /// </summary>
        public SafeAreaEdges Edges => _edges;

        /// <summary>
        /// Sets which edges to inset and reapplies immediately.
        /// </summary>
        /// <param name="edges">Edges to inset.</param>
        public void SetEdges(SafeAreaEdges edges)
        {
            _edges = edges;
            Apply(force: true);
        }

        private void Awake() => _rectTransform = GetComponent<RectTransform>();

        private void OnEnable()
        {
            UniSafeArea.OnChanged += HandleSafeAreaChanged;
            Apply(force: true);
        }

        private void OnDisable() => UniSafeArea.OnChanged -= HandleSafeAreaChanged;

        private void Update()
        {
            // Poll only when nobody else is. Bootstrap drives the shared poll off the
            // update loop; without it — a sample scene, the widgets package used standalone,
            // or edit mode — this component is the only thing that would notice a rotation.
            //
            // The previous condition keyed off HasInsets, which skipped the refresh exactly
            // on the devices that have a notch, so an unbootstrapped scene never responded
            // to rotation on the hardware it mattered for.
            if (!UniSafeArea.IsPolling) UniSafeArea.Refresh();

            Apply(force: false);
        }

        private void HandleSafeAreaChanged(SafeAreaInsets insets) => Apply(force: true);

        private void Apply(bool force)
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) return;

            var insets = ResolveInsets();

            if (!force && _hasApplied && insets == _appliedInsets) return;

            _appliedInsets = insets;
            _hasApplied = true;

            // Anchors rather than offsets: the container then stretches with the parent, so
            // a resolution change needs no recalculation of pixel sizes.
            _rectTransform.anchorMin = new Vector2(insets.Left, insets.Bottom);
            _rectTransform.anchorMax = new Vector2(1f - insets.Right, 1f - insets.Top);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }

        private SafeAreaInsets ResolveInsets()
        {
            var insets = UniSafeArea.Insets;

#if UNITY_EDITOR
            // Editor-only, and only when the real device reports nothing — so plugging in a
            // device profile that does report insets still wins over the preview values.
            if (_previewInEditor && insets.IsZero)
            {
                insets = new SafeAreaInsets(0f, 0f, _previewBottom, _previewTop);
            }
#endif

            return insets.Masked(_edges).Balanced(_balanceHorizontally, _balanceVertically);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (isActiveAndEnabled) Apply(force: true);
        }
#endif
    }
}
