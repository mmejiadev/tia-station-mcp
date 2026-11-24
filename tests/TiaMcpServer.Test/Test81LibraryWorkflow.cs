using System;
using System.IO;
using System.Linq;
using System.Text;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// A block type created, versioned and instantiated, in a project and in a global library, through the MCP tools alone - the
    /// point at which roadmap item 2.2 is done.
    /// </summary>
    /// <remarks>
    /// Every step is a tool call under a write policy naming exactly what the steps touch, as a session would make them; nothing
    /// is set up through the portal behind the tools' backs, beyond retrieving the class project 0967_1 to work on.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test81LibraryWorkflow
    {
        private const string Plc = "PLC_1";

        private const string Block = "FC_Kit";

        private static readonly string[] BlockOnly = { Block };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.FaceplateArchivePath, Path.Combine(_testDirectory, "project"));
            AssemblyHooks.SharedPortal.CompileSoftware(Plc);

            var policy = Path.Combine(_testDirectory, "library-policy.json");
            File.WriteAllText(policy, "{ \"study\": { \"allow\": [ \"" + Plc + "\", \"" + Plc + "/*\", \"library\", \"library/*\", \"global-library/*\", \"project\" ], \"deny\": [] } }");
            McpServer.SetServiceProvider(AssemblyHooks.BuildServices(policy));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            McpServer.SetServiceProvider(AssemblyHooks.BuildServices(Settings.Project1PolicyPath));
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void ABlockType_CreatedVersionedAndInstantiated_InAProjectAndAGlobalLibrary()
        {
            var library = CreateTypeAndPublishIt();
            AddASecondVersionThroughTheInstance(library);

            OpenAFreshProject();
            Applied(McpServer.CopyLibraryTypes(library, BlockOnly));
            Applied(McpServer.CreatePlcBlockFromLibrary(Plc, Block));

            var usage = McpServer.GetLibraryTypeUsage().Content["usages"]!.AsArray().Single(entry => entry!["type"]!.GetValue<string>() == Block)!;
            Assert.AreEqual("0.0.2", usage["instanceVersion"]!.GetValue<string>());
            var compared = McpServer.CompareLibraryTypeVersions(Block, "0.0.1", "0.0.2").Content;
            Assert.IsFalse(compared["contentIdentical"]!.GetValue<bool>(), compared.ToJsonString());
        }

        /// <remarks>
        /// After EditLibraryType the instance shows the version in work and is what that version is edited through; moving the
        /// instances to the newest released version must not delete it and put the older version in its place.
        /// </remarks>
        [TestMethod]
        public void UpdatePlcLibraryInstances_TheInstanceOfAVersionInWork_IsRefusedAndKeepsIt()
        {
            Applied(McpServer.CreateLibraryTypeFromDocument(Plc, BlockDocument("one"), Block));
            Applied(McpServer.ReleaseLibraryType(Block));
            Applied(McpServer.EditLibraryType(Block, Plc));

            var failure = Assert.ThrowsException<global::ModelContextProtocol.McpException>(() => McpServer.UpdatePlcLibraryInstances(Plc, Block));

            StringAssert.Contains(failure.Message, "in work", StringComparison.Ordinal);
            var usage = McpServer.GetLibraryTypeUsage().Content["usages"]!.AsArray().Single(entry => entry!["type"]!.GetValue<string>() == Block)!;
            Assert.AreEqual("0.0.2", usage["instanceVersion"]!.GetValue<string>());
        }

        /// <remarks>
        /// TIA Portal V21 exports nothing of a type in a global library (measured 2026-11-24 by this test's third run): the
        /// comparison there says so rather than failing, and the content is compared once the type is in a project.
        /// </remarks>
        [TestMethod]
        public void CompareLibraryTypeVersions_InAGlobalLibrary_ComparesWhatDescribesThemAndSaysWhyNotTheContent()
        {
            var library = CreateTypeAndPublishIt();

            var compared = McpServer.CompareLibraryTypeVersions(Block, "0.0.1", "0.0.1", library).Content;

            Assert.IsNull(compared["contentIdentical"], compared.ToJsonString());
            Assert.IsNotNull(compared["contentNote"], compared.ToJsonString());
            Assert.IsTrue(compared["properties"]!.AsArray().Count > 0, compared.ToJsonString());
            Console.WriteLine(compared["contentNote"]);
        }

        private string CreateTypeAndPublishIt()
        {
            Applied(McpServer.CreateLibraryTypeFromDocument(Plc, BlockDocument("one"), Block));
            Applied(McpServer.ReleaseLibraryType(Block));
            var library = Applied(McpServer.CreateGlobalLibrary(Path.Combine(_testDirectory, "libraries"), "Kit")).Content["library"]!.GetValue<string>();
            Applied(McpServer.PublishLibraryTypes(library, BlockOnly));

            return library;
        }

        /// <remarks>
        /// The version in work takes its content from a document, through the type. Not through the program: TIA Portal V21
        /// refuses both WriteScl's generation ("Error when calling method 'GenerateBlocksFromSource'") and a document import with
        /// Override ("an object with the name 'FC_Kit' already exists in the plc") over an instance in work, measured 2026-11-24
        /// by this test's first two runs.
        /// </remarks>
        private void AddASecondVersionThroughTheInstance(string library)
        {
            Applied(McpServer.EditLibraryType(Block, Plc));
            Applied(McpServer.CreateLibraryTypeVersionFromDocument(Plc, Block, BlockDocument("two"), replaceInWork: true));
            Assert.AreEqual(0, McpServer.CompileSoftware(Plc).ErrorCount);
            Applied(McpServer.ReleaseLibraryType(Block));
            Applied(McpServer.UpdateGlobalLibraryTypes(library, BlockOnly));
        }

        private void OpenAFreshProject()
        {
            Applied(McpServer.SaveProject());
            AssemblyHooks.SharedPortal.CloseProject();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.FaceplateArchivePath, Path.Combine(_testDirectory, "second"));
        }

        private static T Applied<T>(T response)
            where T : ResponseMessage
        {
            Assert.AreNotEqual(false, response.Meta?["success"]?.GetValue<bool>(), response.Message);

            return response;
        }

        private string BlockDocument(string version)
        {
            var folder = Path.Combine(_testDirectory, "documents", version);
            Directory.CreateDirectory(folder);
            var text = "{\r\n    S7_EditorMode := \"SCL\";\r\n    S7_Optimized := \"TRUE\";\r\n    S7_Version := \"0.1\"\r\n}\r\nFUNCTION \"" + Block + "\" : Void\r\n    { S7_Language := \"SCL\" }\r\n    NETWORK\r\n        // version " + version + "\r\n    END_NETWORK\r\nEND_FUNCTION\r\n";
            File.WriteAllText(Path.Combine(folder, Block + ".s7dcl"), text, new UTF8Encoding(true));

            return folder;
        }
    }
}
