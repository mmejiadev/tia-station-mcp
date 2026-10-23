using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Creating WinCC Unified HMI tags, internal ones and the refusals that keep a list all or nothing.
    /// </summary>
    /// <remarks>
    /// A tag linked to a PLC tag needs an integrated connection, which Openness V20 cannot create
    /// (measured 2026-10-22), so a project built here has none; linking was measured on a copy of a class
    /// project that has one (row H10 of the HMI checklist). What is tested here is everything else.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test45UnifiedTags
    {
        private static readonly string TooLongName = new string('B', 129);

        private string _testDirectory = string.Empty;
        private string _hmiPath = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedTags");
            AssemblyHooks.SharedPortal.AddDevice("OrderNumber:6AV2 123-3GB32-0AW0/20.0.0.2", "HMI_MTP700");
            _hmiPath = AssemblyHooks.SharedPortal.GetSoftwarePaths("HMI_MTP700").Single();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreateUnifiedTags_EveryMeasuredDataType_ReadsBackAsWritten()
        {
            var tags = string.Join(", ", UnifiedTagDataTypes.All.Select(type => $@"{{ ""name"": ""T_{type}"", ""dataType"": ""{type}"" }}"));

            var created = Create($@"{{ ""tags"": [ {tags} ] }}");

            CollectionAssert.AreEqual(UnifiedTagDataTypes.All.ToArray(), created.Select(tag => tag!["dataType"]!.GetValue<string>()).ToArray());
        }

        [TestMethod]
        public void CreateUnifiedTags_InANewTable_LandsInIt()
        {
            var created = Create(@"{ ""newTables"": [ ""Motors"" ], ""tags"": [ { ""name"": ""Speed_SP"", ""table"": ""Motors"", ""dataType"": ""Int"" } ] }");

            Assert.AreEqual("Motors", created.Single()!["table"]!.GetValue<string>());
        }

        [TestMethod]
        public void GetUnifiedTags_AfterCreating_ListsThem()
        {
            Create(@"{ ""tags"": [ { ""name"": ""Level"", ""dataType"": ""Real"" } ] }");

            var tags = (JsonArray)AssemblyHooks.SharedPortal.GetUnifiedTags(_hmiPath)["tags"]!;

            Assert.IsTrue(tags.Any(tag => tag!["name"]!.GetValue<string>() == "Level" && tag["dataType"]!.GetValue<string>() == "Real"), tags.ToJsonString());
        }

        [TestMethod]
        public void CreateUnifiedTags_NameTaken_ThrowsInvalidStateAndCreatesNothing()
        {
            Create(@"{ ""tags"": [ { ""name"": ""Taken"", ""dataType"": ""Bool"" } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""tags"": [ { ""name"": ""Fresh"", ""dataType"": ""Bool"" }, { ""name"": ""TAKEN"", ""dataType"": ""Bool"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            Assert.IsFalse(TagNames().Contains("Fresh"), "a tag of a refused list was created");
        }

        /// <remarks>Openness accepts the name of a connection that does not exist, silently; this tool does not.</remarks>
        [TestMethod]
        public void CreateUnifiedTags_ConnectionMissing_ThrowsInvalidStateAndCreatesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => Create(@"{ ""tags"": [ { ""name"": ""Motor_Run"", ""connection"": ""HMI_Connection_1"", ""plcTag"": ""Motor_Run"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            Assert.IsFalse(TagNames().Contains("Motor_Run"));
        }

        [TestMethod]
        public void CreateUnifiedTags_TableMissing_ThrowsInvalidStateAndCreatesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => Create(@"{ ""tags"": [ { ""name"": ""Speed_SP"", ""table"": ""Motors"", ""dataType"": ""Int"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            Assert.IsFalse(TagNames().Contains("Speed_SP"));
        }

        /// <remarks>
        /// A name TIA Portal itself refuses (longer than 128 characters, the only kind it refused of sixteen tried, measured 2026-10-23) fails after the tags before it exist; they are removed again,
        /// with the table created for them, so the call stays all or nothing.
        /// </remarks>
        [TestMethod]
        public void CreateUnifiedTags_TiaPortalRefusesOne_RemovesTheOnesBefore()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => Create(@"{ ""newTables"": [ ""Motors"" ], ""tags"": [ { ""name"": ""Good"", ""table"": ""Motors"", ""dataType"": ""Int"" }, { ""name"": """ + TooLongName + @""", ""dataType"": ""Int"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.IsFalse(TagNames().Contains("Good"), failure.Message);
            Assert.IsFalse(((JsonArray)AssemblyHooks.SharedPortal.GetUnifiedTags(_hmiPath)["tags"]!).Any(tag => tag!["table"]!.GetValue<string>() == "Motors"), failure.Message);
        }

        private JsonArray Create(string json)
        {
            return AssemblyHooks.SharedPortal.CreateUnifiedTags(_hmiPath, UnifiedTagListReader.Parse(json));
        }

        private string[] TagNames()
        {
            return ((JsonArray)AssemblyHooks.SharedPortal.GetUnifiedTags(_hmiPath)["tags"]!).Select(tag => tag!["name"]!.GetValue<string>()).ToArray();
        }
    }
}
