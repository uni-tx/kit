using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniTx.Core;
using UniTx.IoC;
using UnityEngine.TestTools;

namespace UniTx.Audio.Tests.PlayMode
{
    public class UniAudioTests
    {
        [SetUp]
        public void SetUp()
        {
            UniAudio.Reset();

            // IoCStatics.Resolver now throws rather than returning null when the container
            // has not been created, so gate on IsInitialized.
            if (IoCStatics.IsInitialized)
            {
                IoCStatics.Binder.Bind<UnityEventListener>().AsSingleton().Conclude();
            }
        }

        [TearDown]
        public void TearDown() => UniAudio.Reset();

        [UnityTest]
        public IEnumerator Initialize_AndReset_CyclesCleanly()
        {
            yield return UniAudio.InitializeAsync(CancellationToken.None).ToCoroutine();
            Assert.IsFalse(UniAudio.IsSfxMuted);

            UniAudio.SetMuteSfx(true);
            Assert.IsTrue(UniAudio.IsSfxMuted);

            UniAudio.Reset();
        }

        [UnityTest]
        public IEnumerator Initialize_Twice_Throws()
        {
            yield return UniAudio.InitializeAsync(CancellationToken.None).ToCoroutine();
            Assert.Throws<System.InvalidOperationException>(
                () => UniAudio.InitializeAsync(CancellationToken.None));
        }

        [Test]
        public void DefaultVolume_IsOne()
        {
            Assert.AreEqual(1f, UniAudio.SfxVolume);
            Assert.AreEqual(1f, UniAudio.MusicVolume);
        }
    }
}
