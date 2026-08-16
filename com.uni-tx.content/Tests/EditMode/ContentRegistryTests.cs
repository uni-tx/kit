using System;
using System.Linq;
using NUnit.Framework;

namespace UniTx.Content.Tests.EditMode
{
    [Serializable]
    public sealed class TestContentData : IData
    {
        public string _id;
        public string _name;

        public string Id => _id;
        public string Name => _name;
    }

    public class ContentRegistryTests
    {
        [SetUp]
        public void SetUp() => ContentRegistry.Register<TestContentData>("test_content");

        [Test]
        public void GetLoader_RegisteredFile_ReturnsLoader()
        {
            Assert.IsNotNull(ContentRegistry.GetLoader("test_content"));
        }

        [Test]
        public void GetLoader_UnregisteredFile_ReturnsNull()
        {
            Assert.IsNull(ContentRegistry.GetLoader("nope"));
        }

        [Test]
        public void Register_InterfaceType_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => ContentRegistry.Register<IData>("bad"));
        }

        [Test]
        public void DataLoader_LoadsJsonArray()
        {
            var loader = ContentRegistry.GetLoader("test_content");
            const string json = "{ \"Items\": [ { \"_id\": \"a\", \"_name\": \"Alpha\" }, { \"_id\": \"b\", \"_name\": \"Beta\" } ] }";

            var items = loader.Load(json).Cast<TestContentData>().ToArray();

            Assert.AreEqual(2, items.Length);
            Assert.AreEqual("Alpha", items[0].Name);
            Assert.AreEqual("b", items[1].Id);
        }

        [Test]
        public void DataLoader_EmptyJson_ReturnsEmpty()
        {
            var loader = ContentRegistry.GetLoader("test_content");

            Assert.IsEmpty(loader.Load("{ \"Items\": [] }"));
        }
    }
}
