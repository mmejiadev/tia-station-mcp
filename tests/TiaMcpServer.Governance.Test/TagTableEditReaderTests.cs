using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// A change to some rows of a PLC tag table, read before TIA Portal is touched. What is refused here is a call that
    /// would do one thing to a tag where the author meant another, or nothing at all.
    /// </remarks>
    [TestClass]
    public sealed class TagTableEditReaderTests
    {
        private const string Full = @"{ ""table"": ""Cell/IO"",
            ""remove"": [ ""Old_Lamp"" ],
            ""set"": [ { ""name"": ""Start"", ""address"": ""%I0.1"", ""comment"": """" } ],
            ""add"": [ { ""name"": ""Stop"", ""dataType"": ""Bool"", ""address"": ""%I0.2"", ""comment"": ""Stop button, NC"" } ] }";

        [TestMethod]
        public void Parse_AFullEdit_ReadsEveryPart()
        {
            var edit = TagTableEditReader.Parse(Full);

            Assert.AreEqual("Cell/IO", edit.Table);
            Assert.AreEqual("Old_Lamp", edit.Remove.Single());
            Assert.AreEqual("%I0.1", edit.Set.Single().Address);
            Assert.IsNull(edit.Set.Single().DataTypeName);
            Assert.AreEqual("Bool", edit.Add.Single().DataTypeName);
            Assert.AreEqual("Stop button, NC", edit.Add.Single().Comment);
        }

        /// <remarks>An empty comment clears it; a comment left out leaves it.</remarks>
        [TestMethod]
        public void Parse_AnEmptyComment_IsKeptAsEmpty()
        {
            var edit = TagTableEditReader.Parse(Full);

            Assert.AreEqual(string.Empty, edit.Set.Single().Comment);
        }

        /// <remarks>Measured 2026-11-19: TIA Portal takes any text as an address, so this is the only check there is.</remarks>
        [TestMethod]
        [DataRow("%i0.7", "%I0.7")]
        [DataRow("%QB1", "%QB1")]
        [DataRow("%MW10", "%MW10")]
        [DataRow("%ID4", "%ID4")]
        [DataRow("%ML8", "%ML8")]
        public void Parse_ATagAddress_IsTakenInUpperCase(string written, string expected)
        {
            var edit = TagTableEditReader.Parse(@"{ ""table"": ""T"", ""set"": [ { ""name"": ""A"", ""address"": """ + written + @""" } ] }");

            Assert.AreEqual(expected, edit.Set.Single().Address);
        }

        [TestMethod]
        public void Touched_AFullEdit_NamesTheTagsRemovedAndSet()
        {
            var edit = TagTableEditReader.Parse(Full);

            CollectionAssert.AreEqual(new[] { "Old_Lamp", "Start" }, edit.Touched.ToList());
        }

        [TestMethod]
        [DataRow(@"{ ""remove"": [ ""A"" ] }", "'table'")]
        [DataRow(@"{ ""table"": ""T"" }", "changes nothing")]
        [DataRow(@"{ ""table"": ""T"", ""add"": [ { ""name"": ""A"", ""dataType"": ""Bool"" } ] }", "'address'")]
        [DataRow(@"{ ""table"": ""T"", ""set"": [ { ""name"": ""A"" } ] }", "changes nothing")]
        [DataRow(@"{ ""table"": ""T"", ""set"": [ { ""name"": ""A"", ""value"": 1 } ] }", "'value'")]
        [DataRow(@"{ ""table"": ""T"", ""remove"": [ ""A"" ], ""set"": [ { ""name"": ""a"", ""comment"": ""x"" } ] }", "named more than once: A")]
        [DataRow(@"{ ""table"": ""T"", ""remove"": [ 3 ] }", "as strings")]
        [DataRow(@"{ ""table"": ""T"", ""add"": { ""name"": ""A"" } }", "must be a list")]
        [DataRow("not json", "not valid JSON")]
        [DataRow(@"{ ""table"": ""T"", ""add"": [ { ""name"": ""A"", ""dataType"": ""Bool"", ""address"": ""%Q0.8"" } ] }", "not a tag address")]
        [DataRow(@"{ ""table"": ""T"", ""set"": [ { ""name"": ""A"", ""address"": ""nonsense"" } ] }", "not a tag address")]
        [DataRow(@"{ ""table"": ""T"", ""set"": [ { ""name"": ""A"", ""address"": ""%DB1.DBX0.0"" } ] }", "not a tag address")]
        public void Parse_AnIncompleteOrAmbiguousEdit_IsRefused(string json, string reason)
        {
            var failure = Assert.ThrowsException<PortalException>(() => TagTableEditReader.Parse(json));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, reason, System.StringComparison.Ordinal);
        }
    }
}
