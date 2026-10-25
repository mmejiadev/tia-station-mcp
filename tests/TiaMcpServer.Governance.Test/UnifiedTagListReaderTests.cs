using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Fail closed: a tag is internal with a measured data type, or linked with both a connection and a
    /// PLC tag; anything in between, and any field the tool does not set, is refused before TIA Portal is asked.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedTagListReaderTests
    {
        private static readonly string[] NewTablePaths = { "Plant/Area_1/Valves", "Motors" };

        private static readonly string[] ValvesGroups = { "Plant", "Area_1" };

        [TestMethod]
        public void Parse_InternalAndLinkedTags_ReadsBoth()
        {
            var list = UnifiedTagListReader.Parse(@"{ ""newTables"": [ ""Motors"" ], ""tags"": [
                { ""name"": ""Speed_SP"", ""table"": ""Motors"", ""dataType"": ""int"" },
                { ""name"": ""Motor_Run"", ""connection"": ""HMI_Connection_1"", ""plcTag"": ""Motor_Run"" } ] }");

            Assert.AreEqual("Motors", list.NewTables[0]);
            Assert.AreEqual("Int", list.Tags[0].DataType);
            Assert.AreEqual("Motors", list.Tags[0].Table);
            Assert.IsNull(list.Tags[0].Link);
            Assert.AreEqual("HMI_Connection_1", list.Tags[1].Link!.Connection);
            Assert.AreEqual("Motor_Run", list.Tags[1].Link!.PlcTag);
            Assert.IsNull(list.Tags[1].DataType);
        }

        [TestMethod]
        [DataRow(@"{ ""tags"": [] }", DisplayName = "no tags")]
        [DataRow(@"{ ""tags"": [ { ""dataType"": ""Int"" } ] }", DisplayName = "no name")]
        [DataRow(@"{ ""tags"": [ { ""name"": ""A"" } ] }", DisplayName = "internal tag without a data type")]
        [DataRow(@"{ ""tags"": [ { ""name"": ""A"", ""dataType"": ""String"" } ] }", DisplayName = "a data type TIA Portal refused")]
        [DataRow(@"{ ""tags"": [ { ""name"": ""A"", ""connection"": ""HMI_Connection_1"" } ] }", DisplayName = "connection without a PLC tag")]
        [DataRow(@"{ ""tags"": [ { ""name"": ""A"", ""plcTag"": ""Start"" } ] }", DisplayName = "PLC tag without a connection")]
        [DataRow(@"{ ""tags"": [ { ""name"": ""A"", ""connection"": ""C"", ""plcTag"": ""Start"", ""dataType"": ""Bool"" } ] }", DisplayName = "linked tag with a data type")]
        [DataRow(@"{ ""tags"": [ { ""name"": ""A"", ""dataType"": ""Int"" }, { ""name"": ""a"", ""dataType"": ""Int"" } ] }", DisplayName = "name repeated in another case")]
        [DataRow(@"{ ""tags"": [ { ""name"": ""A"", ""dataType"": ""Int"", ""colour"": ""x"" } ] }", DisplayName = "a field the tool does not set")]
        [DataRow(@"{ ""tables"": [ ""Motors"" ], ""tags"": [ { ""name"": ""A"", ""dataType"": ""Int"" } ] }", DisplayName = "a list field the tool does not know")]
        [DataRow(@"{ ""tags"": [ { ""name"": 5, ""dataType"": ""Int"" } ] }", DisplayName = "name that is not a string")]
        [DataRow(@"not json", DisplayName = "not JSON")]
        [DataRow(@"{ ""newTables"": [ ""Plant//Valves"" ], ""tags"": [ { ""name"": ""A"", ""dataType"": ""Int"" } ] }", DisplayName = "a table path with an empty group")]
        [DataRow(@"{ ""newTables"": [ ""Plant/Valves"", ""Area/valves"" ], ""tags"": [ { ""name"": ""A"", ""dataType"": ""Int"" } ] }", DisplayName = "two new tables of one name in different groups")]
        public void Parse_IncompleteOrUnknown_ThrowsInvalidParams(string json)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedTagListReader.Parse(json));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_TablePaths_KeepTheirGroups()
        {
            var list = UnifiedTagListReader.Parse(@"{ ""newTables"": [ "" Plant / Area_1 / Valves "", ""Motors"" ], ""tags"": [ { ""name"": ""A"", ""table"": ""Valves"", ""dataType"": ""Int"" } ] }");

            CollectionAssert.AreEqual(NewTablePaths, list.NewTables.ToArray());
            Assert.AreEqual("Valves", UnifiedTagListReader.TableName(list.NewTables[0]));
            CollectionAssert.AreEqual(ValvesGroups, UnifiedTagListReader.GroupsOf(list.NewTables[0]).ToArray());
            Assert.AreEqual(0, UnifiedTagListReader.GroupsOf(list.NewTables[1]).Count);
        }

        [TestMethod]
        public void Parse_EmptyTable_MeansTheDefaultOne()
        {
            var list = UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""A"", ""table"": "" "", ""dataType"": ""Int"" } ] }");

            Assert.IsNull(list.Tags[0].Table);
        }

        [TestMethod]
        public void Normalise_EveryMeasuredDataType_IsKeptAsSpelled()
        {
            foreach (var type in UnifiedTagDataTypes.All)
            {
                Assert.AreEqual(type, UnifiedTagDataTypes.Normalise(type.ToUpperInvariant(), "T"));
            }
        }
    }
}
