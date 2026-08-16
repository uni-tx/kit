using NUnit.Framework;
using UniTx.Core;

namespace UniTx.Bootstrap.Tests.EditMode
{
    public class LoadingStepTests
    {
        [Test]
        public void LoadingStepBase_IsInitializableAsync()
        {
            Assert.IsTrue(typeof(IInitializableAsync).IsAssignableFrom(typeof(LoadingStepBase)));
        }

        [Test]
        public void UniTxStep_IsLoadingStep()
        {
            Assert.IsTrue(typeof(LoadingStepBase).IsAssignableFrom(typeof(UniTxStep)));
        }

        [Test]
        public void BindDependenciesStep_IsLoadingStep()
        {
            Assert.IsTrue(typeof(LoadingStepBase).IsAssignableFrom(typeof(BindDependenciesStep)));
        }

        [Test]
        public void InitDependenciesStep_IsLoadingStep()
        {
            Assert.IsTrue(typeof(LoadingStepBase).IsAssignableFrom(typeof(InitDependenciesStep)));
        }
    }
}
