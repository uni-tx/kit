using NUnit.Framework;
using UnityEngine;

namespace UniTx.Core.Tests.EditMode
{
    public class SafeAreaInsetsTests
    {
        [Test]
        public void Zero_HasNoInsets()
        {
            Assert.IsTrue(SafeAreaInsets.Zero.IsZero);
            Assert.AreEqual(0f, SafeAreaInsets.Zero.Top);
        }

        [Test]
        public void Constructor_ClampsNegativeValuesToZero()
        {
            // A negative inset would push content off-screen rather than away from a cutout.
            var insets = new SafeAreaInsets(-1f, -0.5f, -0.1f, 0.2f);

            Assert.AreEqual(0f, insets.Left);
            Assert.AreEqual(0f, insets.Right);
            Assert.AreEqual(0f, insets.Bottom);
            Assert.AreEqual(0.2f, insets.Top);
        }

        [Test]
        public void ToPixels_ScalesByScreenSize()
        {
            var insets = new SafeAreaInsets(0.1f, 0.2f, 0.05f, 0.15f);

            var (left, right, bottom, top) = insets.ToPixels(1000f, 2000f);

            Assert.AreEqual(100f, left, 0.01f);
            Assert.AreEqual(200f, right, 0.01f);
            Assert.AreEqual(100f, bottom, 0.01f);
            Assert.AreEqual(300f, top, 0.01f);
        }

        [Test]
        public void Masked_ZeroesUnselectedEdges()
        {
            var insets = new SafeAreaInsets(0.1f, 0.1f, 0.1f, 0.1f);

            var vertical = insets.Masked(SafeAreaEdges.Vertical);

            // A bottom bar that should reach the screen edge keeps its horizontal bleed.
            Assert.AreEqual(0f, vertical.Left);
            Assert.AreEqual(0f, vertical.Right);
            Assert.AreEqual(0.1f, vertical.Bottom);
            Assert.AreEqual(0.1f, vertical.Top);
        }

        [Test]
        public void Masked_None_ZeroesEverything()
            => Assert.IsTrue(new SafeAreaInsets(0.1f, 0.1f, 0.1f, 0.1f).Masked(SafeAreaEdges.None).IsZero);

        [Test]
        public void Balanced_MirrorsTheLargerInsetOntoTheOppositeEdge()
        {
            // A landscape notch obscures one side only, which shifts a centred layout.
            var insets = new SafeAreaInsets(0.08f, 0f, 0f, 0f);

            var balanced = insets.Balanced(horizontal: true, vertical: false);

            Assert.AreEqual(0.08f, balanced.Left);
            Assert.AreEqual(0.08f, balanced.Right, "the opposite edge should mirror the notch");
            Assert.AreEqual(0f, balanced.Bottom, "vertical balancing was not requested");
        }

        [Test]
        public void Balanced_LeavesSymmetricInsetsUntouched()
        {
            var insets = new SafeAreaInsets(0.05f, 0.05f, 0f, 0f);

            Assert.AreEqual(insets, insets.Balanced(horizontal: true, vertical: true));
        }

        [Test]
        public void Equality_UsesApproximateComparison()
        {
            var a = new SafeAreaInsets(0.1f, 0.2f, 0.3f, 0.4f);
            var b = new SafeAreaInsets(0.1f, 0.2f, 0.3f, 0.4f);

            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DetectsDifference()
            => Assert.AreNotEqual(new SafeAreaInsets(0.1f, 0f, 0f, 0f), new SafeAreaInsets(0f, 0.1f, 0f, 0f));
    }

    public class UniSafeAreaTests
    {
        [TearDown]
        public void TearDown() => UniSafeArea.SetOverride(SafeAreaInsets.Zero);

        [Test]
        public void SetOverride_ReportsTheSuppliedInsets()
        {
            var insets = new SafeAreaInsets(0f, 0f, 0.03f, 0.06f);

            UniSafeArea.SetOverride(insets);

            Assert.AreEqual(insets, UniSafeArea.Insets);
            Assert.IsTrue(UniSafeArea.HasInsets);
        }

        [Test]
        public void SetOverride_RaisesOnChanged()
        {
            var raised = SafeAreaInsets.Zero;
            void Handler(SafeAreaInsets i) => raised = i;

            UniSafeArea.OnChanged += Handler;

            try
            {
                UniSafeArea.SetOverride(new SafeAreaInsets(0f, 0f, 0.02f, 0.05f));
            }
            finally
            {
                UniSafeArea.OnChanged -= Handler;
            }

            Assert.AreEqual(0.05f, raised.Top, 0.0001f);
        }

        [Test]
        public void Refresh_OnAScreenWithNoCutout_ReportsNoInsets()
        {
            // The editor Game view reports a full-screen safe area, which is what makes
            // SetOverride necessary for testing notch layouts at all.
            UniSafeArea.Refresh();

            Assert.IsFalse(UniSafeArea.Insets.Left < 0f);
            Assert.IsFalse(UniSafeArea.Insets.Top < 0f);
        }

        [Test]
        public void Initialize_WithNullListener_DoesNotThrow()
            // Valid setup: values stay correct on read, only change notifications are lost.
            => Assert.DoesNotThrow(() => UniSafeArea.Initialize(null));

        [Test]
        public void Reset_KeepsSubscribers_SoPersistentUiKeepsUpdating()
        {
            // Persistent UI outlives the bootstrap object that started the poll. Clearing
            // subscribers on Reset silently stopped a live SafeAreaFitter on a
            // DontDestroyOnLoad canvas from ever updating again — it only subscribes in
            // OnEnable, which never fires a second time.
            var received = 0;
            void Handler(SafeAreaInsets _) => received++;

            UniSafeArea.OnChanged += Handler;

            try
            {
                UniSafeArea.Reset(null);
                UniSafeArea.SetOverride(new SafeAreaInsets(0f, 0f, 0.02f, 0.04f));

                Assert.AreEqual(1, received, "a subscriber must survive Reset");
            }
            finally
            {
                UniSafeArea.OnChanged -= Handler;
            }
        }

        [Test]
        public void IsPolling_ReflectsWhetherAListenerIsDriving()
        {
            UniSafeArea.Reset(null);
            Assert.IsFalse(UniSafeArea.IsPolling);

            var listener = new UnityEventListener();

            try
            {
                UniSafeArea.Initialize(listener);
                Assert.IsTrue(UniSafeArea.IsPolling, "components rely on this to decide whether to self-poll");

                UniSafeArea.Reset(listener);
                Assert.IsFalse(UniSafeArea.IsPolling);
            }
            finally
            {
                UniSafeArea.Reset(listener);
            }
        }

        [Test]
        public void Cutouts_AreQueryable()
            => Assert.IsNotNull(UniSafeArea.Cutouts);
    }
}
