using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <summary>Reading a change to some members of a global data block: what is refused before TIA Portal is asked anything.</summary>
    [TestClass]
    public sealed class DbMemberEditReaderTests
    {
        [TestMethod]
        public void Parse_ARemoveASetAndAnAdd_ReadsEachList()
        {
            var edit = DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""remove"": [ ""Old"" ],
                ""set"": [ { ""name"": ""Count"", ""startValue"": 9 } ],
                ""add"": [ { ""name"": ""Extra"", ""dataType"": ""Int"", ""startValue"": 7 } ] }");

            Assert.AreEqual("Cell", edit.Block);
            CollectionAssert.AreEqual(new[] { "Old" }, edit.Remove.ToArray());
            Assert.AreEqual("9", edit.Set.Single().StartValue);
            Assert.AreEqual("Int", edit.Add.Single().DataType);
        }

        [TestMethod]
        public void Parse_NumbersAndBooleans_AreTakenAsTiaPortalWritesThem()
        {
            var edit = DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""add"": [
                { ""name"": ""A"", ""dataType"": ""Real"", ""startValue"": 21.5 },
                { ""name"": ""B"", ""dataType"": ""Bool"", ""startValue"": true },
                { ""name"": ""C"", ""dataType"": ""String[10]"", ""startValue"": ""'abc'"" } ] }");

            CollectionAssert.AreEqual(new[] { "21.5", "true", "'abc'" }, edit.Add.Select(row => row.StartValue).ToArray());
        }

        [TestMethod]
        public void Parse_NoBlock_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => DbMemberEditReader.Parse(@"{ ""remove"": [ ""Old"" ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_AnEditThatChangesNothing_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => DbMemberEditReader.Parse(@"{ ""block"": ""Cell"" }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_AMemberAddedWithNoDataType_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""add"": [ { ""name"": ""Extra"" } ] }"));

            StringAssert.Contains(failure.Message, "dataType", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_ASetThatChangesNothing_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""set"": [ { ""name"": ""Count"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_AMemberNamedTwiceCaseAside_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""remove"": [ ""Count"" ], ""set"": [ { ""name"": ""COUNT"", ""startValue"": 1 } ] }"));

            StringAssert.Contains(failure.Message, "named more than once", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_AFieldAMemberDoesNotTake_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""set"": [ { ""name"": ""Count"", ""comment"": ""x"" } ] }"));

            StringAssert.Contains(failure.Message, "comment", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_ANameHoldingMarkup_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""remove"": [ ""A<B"" ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_ADataTypeHoldingMarkup_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""add"": [ { ""name"": ""A"", ""dataType"": ""Int&amp;"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }
    }
}
