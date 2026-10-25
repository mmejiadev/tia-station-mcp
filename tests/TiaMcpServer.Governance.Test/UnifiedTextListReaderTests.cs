using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTextLists;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// A text list written as the classic <c>GenerateHmiTextList</c> takes it, refused when TIA Portal V20
    /// would import it and keep something else: a range, a value given twice, a text over two lines.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedTextListReaderTests
    {
        [TestMethod]
        public void Parse_States_KeepsEveryValueAndTextInOrder()
        {
            var list = UnifiedTextListReader.Parse(@"{ ""name"": "" States "", ""entries"": [ { ""value"": 0, ""text"": "" Stopped "" }, { ""value"": -5, ""text"": ""It's on: Ñ"" } ] }");

            Assert.AreEqual("States", list.Name);
            Assert.AreEqual(2, list.Entries.Count);
            Assert.AreEqual("Stopped", list.Entries[0].Text);
            Assert.AreEqual(-5L, list.Entries[1].From);
            Assert.IsTrue(list.Entries[1].IsSingleValue);
        }

        [TestMethod]
        [DataRow(@"{ ""entries"": [ { ""value"": 0, ""text"": ""A"" } ] }", DisplayName = "no name")]
        [DataRow(@"{ ""name"": ""Two words"", ""entries"": [ { ""value"": 0, ""text"": ""A"" } ] }", DisplayName = "name with a space")]
        [DataRow(@"{ ""name"": ""1States"", ""entries"": [ { ""value"": 0, ""text"": ""A"" } ] }", DisplayName = "name starting with a digit")]
        [DataRow(@"{ ""name"": ""States"" }", DisplayName = "no entries")]
        [DataRow(@"{ ""name"": ""States"", ""entries"": [] }", DisplayName = "empty entries")]
        [DataRow(@"{ ""name"": ""States"", ""entries"": [ { ""text"": ""A"" } ] }", DisplayName = "no value")]
        [DataRow(@"{ ""name"": ""States"", ""entries"": [ { ""value"": 1.5, ""text"": ""A"" } ] }", DisplayName = "fractional value")]
        [DataRow(@"{ ""name"": ""States"", ""entries"": [ { ""value"": 3000000000, ""text"": ""A"" } ] }", DisplayName = "value past 32 bits")]
        [DataRow(@"{ ""name"": ""States"", ""entries"": [ { ""value"": 0, ""text"": ""A"" }, { ""value"": 0, ""text"": ""B"" } ] }", DisplayName = "value twice")]
        [DataRow(@"{ ""name"": ""States"", ""entries"": [ { ""value"": 0, ""text"": "" "" } ] }", DisplayName = "blank text")]
        [DataRow(@"{ ""name"": ""States"", ""entries"": [ { ""value"": 0, ""text"": ""A\nB"" } ] }", DisplayName = "text on two lines")]
        [DataRow(@"{ ""name"": ""States"", ""entries"": [ { ""from"": 10, ""to"": 20, ""text"": ""A"" } ] }", DisplayName = "a range")]
        [DataRow(@"{ ""name"": ""States"", ""entries"": [ { ""value"": 0, ""text"": ""A"" } ], ""default"": ""B"" }", DisplayName = "unknown list field")]
        [DataRow(@"{ ""name"": ""Null"", ""entries"": [ { ""value"": 0, ""text"": ""A"" } ] }", DisplayName = "a YAML null word")]
        [DataRow(@"{ ""name"": ""yes"", ""entries"": [ { ""value"": 0, ""text"": ""A"" } ] }", DisplayName = "a YAML boolean word")]
        [DataRow(@"[ ]", DisplayName = "not an object")]
        [DataRow(@"{ ""name"": ", DisplayName = "invalid JSON")]
        public void Parse_Wrong_ThrowsInvalidParams(string json)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedTextListReader.Parse(json));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }
    }
}
