using System;
using NUnit.Framework;
using UniTx.Core;

namespace UniTx.Core.Tests.EditMode
{
    public class CoreExtensionsTests
    {
        [Test]
        public void ToUnixTimestamp_ReturnsSecondsSinceEpoch()
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(0L, epoch.ToUnixTimestamp());
        }

        [Test]
        public void SafeInvoke_WithNullAction_DoesNotThrow()
        {
            Action action = null;

            Assert.DoesNotThrow(action.SafeInvoke);
        }

        [Test]
        public void SafeInvoke_WithValue_Invokes()
        {
            var received = 0;
            Action<int> action = v => received = v;

            action.SafeInvoke(7);

            Assert.AreEqual(7, received);
        }

        [Test]
        public void SafeInvoke_Generic_ReturnsDefaultForNull()
        {
            Func<string, int> func = null;

            Assert.AreEqual(0, func.SafeInvoke("x"));
        }

        [Test]
        public void FixTurkishChars_ReplacesTurkishCharacters()
        {
            Assert.AreEqual("Isik Sisi Gugum", "Işık Şişi Güğüm".FixTurkishChars());
        }

        [Test]
        public void SafeCancelAndDispose_NullSource_DoesNotThrow()
        {
            System.Threading.CancellationTokenSource cts = null;

            Assert.DoesNotThrow(() => cts.SafeCancelAndDispose());
        }
    }
}
