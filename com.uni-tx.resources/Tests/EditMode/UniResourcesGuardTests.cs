using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace UniTx.Resources.Tests.EditMode
{
    public class UniResourcesGuardTests
    {
        [Test]
        public void LoadAsset_WithoutInit_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                UniResources.LoadAssetAsync<UnityEngine.Object>("anything", cToken: CancellationToken.None));
        }

        [Test]
        public void DisposeAsset_WithoutInit_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() => UniResources.DisposeAsset<UnityEngine.Object>(null));
        }
    }
}
