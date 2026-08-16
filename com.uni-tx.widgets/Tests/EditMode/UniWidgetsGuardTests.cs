using NUnit.Framework;

namespace UniTx.Widgets.Tests.EditMode
{
    public class UniWidgetsGuardTests
    {
        [SetUp]
        public void SetUp() => UniWidgets.Reset();

        [Test]
        public void Peek_WithoutInit_ReturnsNull()
        {
            Assert.IsNull(UniWidgets.Peek());
        }

        [Test]
        public void Reset_WithoutInit_DoesNotThrow()
        {
            Assert.DoesNotThrow(UniWidgets.Reset);
        }
    }
}
