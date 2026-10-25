using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTextLists;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The two YAML files of a WinCC Unified text list. The export below is the one TIA Portal V20 wrote
    /// for the class project 0967-unified-01 on 2026-10-25, cut to three entries: its first entry, value
    /// 0, has no value fields at all.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedTextListYamlTests
    {
        private const string ClassList = "#Version: 2.0\r\n\r\nTextListContainers:\r\n  DeviceTextList:\r\n    ResourceListType: TextList\r\n    ResourceLists:\r\n"
            + "      MODES_DE_FUNCIONAMENT:\r\n        Entries:\r\n"
            + "          Text_list_entry_0:\r\n            Text: MyTextLibrary.Text_0\r\n"
            + "          Text_list_entry_1:\r\n            Value: 1\r\n            FromValue: 1\r\n            ToValue: 1\r\n            Text: MyTextLibrary.Text_1\r\n"
            + "          Text_list_entry_2:\r\n            Value: 2\r\n            FromValue: 2\r\n            ToValue: 2\r\n            Text: MyTextLibrary.Text_2\r\n";

        private const string ClassLibrary = "#Version: 2.0\r\n\r\nTextLibraries:\r\n  MyTextLibrary:\r\n    Type: Text\r\n    Languages:\r\n    - en-US\r\n    DefaultLanguage: en-US\r\n    Entries:\r\n"
            + "      Text_0:\r\n        Text:\r\n        - A1\r\n      Text_1:\r\n        Text:\r\n        - F1\r\n      Text_2:\r\n        Text:\r\n        - A2\r\n";

        [TestMethod]
        public void Read_ClassExport_EntryWithoutValuesIsZero()
        {
            var list = UnifiedTextListYaml.Read(ClassList, ClassLibrary, "en-US").Single();

            Assert.AreEqual("MODES_DE_FUNCIONAMENT", list.Name);
            CollectionAssert.AreEqual(new[] { 0L, 1L, 2L }, list.Entries.Select(entry => entry.From).ToArray());
            CollectionAssert.AreEqual(new[] { "A1", "F1", "A2" }, list.Entries.Select(entry => entry.Text).ToArray());
        }

        [TestMethod]
        public void Write_ThenRead_GivesTheSameList()
        {
            var list = UnifiedTextListReader.Parse(@"{ ""name"": ""States"", ""entries"": [ { ""value"": 0, ""text"": ""Stopped"" }, { ""value"": -5, ""text"": ""It's on: 'Ñà'"" }, { ""value"": 100000, ""text"": ""\""Fault\"" #1"" } ] }");

            var (listFile, libraryFile) = UnifiedTextListYaml.Write(list, "es-ES");
            var read = UnifiedTextListYaml.Read(listFile, libraryFile, "es-ES").Single();

            Assert.AreEqual("States", read.Name);
            CollectionAssert.AreEqual(list.Entries.Select(entry => entry.Text).ToArray(), read.Entries.Select(entry => entry.Text).ToArray());
            CollectionAssert.AreEqual(list.Entries.Select(entry => entry.From).ToArray(), read.Entries.Select(entry => entry.To).ToArray());
        }

        /// <remarks>An entry with only <c>Value</c> was imported with no value (measured 2026-10-25), so every value is written as FromValue and ToValue.</remarks>
        [TestMethod]
        public void Write_Value_IsWrittenAsFromAndTo()
        {
            var (listFile, _) = UnifiedTextListYaml.Write(UnifiedTextListReader.Parse(@"{ ""name"": ""L"", ""entries"": [ { ""value"": 7, ""text"": ""A"" } ] }"), "en-US");

            StringAssert.Contains(listFile, "FromValue: 7\r\n            ToValue: 7", System.StringComparison.Ordinal);
        }

        /// <remarks>What TIA Portal exports for a list it kept no entry of: the name and nothing else.</remarks>
        [TestMethod]
        public void Read_ListWithNothingAfterItsName_HasNoEntries()
        {
            var emptied = ClassList.Substring(0, ClassList.IndexOf("      MODES_DE_FUNCIONAMENT:", System.StringComparison.Ordinal)) + "      Ranged: \r\n";

            var list = UnifiedTextListYaml.Read(emptied, ClassLibrary, "en-US").Single();

            Assert.AreEqual("Ranged", list.Name);
            Assert.AreEqual(0, list.Entries.Count);
        }

        [TestMethod]
        public void Read_RangeMadeInTheEditor_IsARange()
        {
            var range = ClassList.Replace("Value: 2\r\n            FromValue: 2\r\n            ToValue: 2", "Value: 10\r\n            FromValue: 10\r\n            ToValue: 20");

            var entry = UnifiedTextListYaml.Read(range, ClassLibrary, "en-US").Single().Entries[2];

            Assert.AreEqual(10L, entry.From);
            Assert.AreEqual(20L, entry.To);
            Assert.IsFalse(entry.IsSingleValue);
        }

        [TestMethod]
        [DataRow("ca-ES", "B", DisplayName = "the language asked for")]
        [DataRow("ja-JP", "A", DisplayName = "the default when it has not the one asked for")]
        public void Read_TwoLanguages_PicksTheTextOfTheLanguage(string language, string expected)
        {
            var library = ClassLibrary.Replace("    - en-US\r\n", "    - en-US\r\n    - ca-ES\r\n").Replace("        - A1\r\n", "        - A\r\n        - B\r\n");

            var text = UnifiedTextListYaml.Read(ClassList, library, language).Single().Entries[0].Text;

            Assert.AreEqual(expected, text);
        }

        /// <remarks>Read on V20 only: a file in another shape is refused, never read by guess.</remarks>
        [TestMethod]
        [DataRow("TextListContainers: [ 1, 2 ]", DisplayName = "containers not a mapping")]
        [DataRow("Something: else", DisplayName = "no containers")]
        [DataRow("TextListContainers:\r\n  DeviceTextList:\r\n    ResourceListType: TextList\r\n    ResourceLists:\r\n      L:\r\n        Entries:\r\n          E:\r\n            FromValue: one\r\n            Text: MyTextLibrary.Text_0\r\n", DisplayName = "a value that is not a number")]
        [DataRow("TextListContainers:\r\n  DeviceTextList:\r\n    ResourceListType: TextList\r\n    ResourceLists:\r\n      L:\r\n        Entries:\r\n          E:\r\n            Text: MyTextLibrary.Missing\r\n", DisplayName = "a text the library does not hold")]
        [DataRow("TextListContainers: [", DisplayName = "not YAML")]
        public void Read_UnknownShape_ThrowsInvalidState(string listFile)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedTextListYaml.Read(listFile, ClassLibrary, "en-US"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }
    }
}
