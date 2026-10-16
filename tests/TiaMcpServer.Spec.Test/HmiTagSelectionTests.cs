using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec.Tests
{
    [TestClass]
    public sealed class HmiTagSelectionTests
    {
        private const string TargetTable = "PLC tags";

        private static readonly TagInfo[] ClassProgram =
        {
            new TagInfo("Marxa", TagInfo.TagKind, "Bool", "%M0.0"),
            new TagInfo("CONT1", TagInfo.TagKind, "Int", "%MW10"),
            new TagInfo("PolsadorMarxa", TagInfo.TagKind, "Bool", "%I0.0"),
            new TagInfo("KM1", TagInfo.TagKind, "Bool", "%Q0.0"),
            new TagInfo("MaxPieces", TagInfo.ConstantKind, "Int", "10")
        };

        private static Dictionary<string, string> NoHmiTags()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void ForMemoryTags_ClassProgram_LinksOnlyTheMemoryTags()
        {
            var plan = HmiTagSelection.ForMemoryTags(ClassProgram, NoHmiTags(), TargetTable);

            CollectionAssert.AreEqual(new[] { "Marxa", "CONT1" }, plan.Links.Select(link => link.ControllerTagName).ToArray());
        }

        [TestMethod]
        public void ForMemoryTags_ClassProgram_NamesEachHmiTagAfterItsPlcTag()
        {
            var plan = HmiTagSelection.ForMemoryTags(ClassProgram, NoHmiTags(), TargetTable);

            Assert.IsTrue(plan.Links.All(link => link.HmiTagName == link.ControllerTagName));
        }

        [TestMethod]
        public void ForMemoryTags_HmiTagAlreadyInAnotherTable_LeavesItOut()
        {
            var existing = NoHmiTags();
            existing["Marxa"] = "Default tag table";

            var plan = HmiTagSelection.ForMemoryTags(ClassProgram, existing, TargetTable);

            CollectionAssert.AreEqual(new[] { "CONT1" }, plan.Links.Select(link => link.HmiTagName).ToArray());
            CollectionAssert.AreEqual(new[] { "Marxa" }, plan.AlreadyPresent.ToArray());
        }

        /// <remarks>
        /// The safety rule of the generator: importing replaces the table whole, so a tag written
        /// there by hand would be deleted. The plan must name it so the write can be refused.
        /// </remarks>
        [TestMethod]
        public void ForMemoryTags_TargetTableHoldsAHandWrittenTag_ReportsItDisplaced()
        {
            var existing = NoHmiTags();
            existing["Tag_ScreenNumber"] = TargetTable;

            var plan = HmiTagSelection.ForMemoryTags(ClassProgram, existing, TargetTable);

            CollectionAssert.AreEqual(new[] { "Tag_ScreenNumber" }, plan.Displaced.ToArray());
        }

        /// <remarks>Running the generator twice must be allowed and must write the same table.</remarks>
        [TestMethod]
        public void ForMemoryTags_TargetTableHoldsOnlyGeneratedTags_DisplacesNothing()
        {
            var existing = NoHmiTags();
            existing["Marxa"] = TargetTable;
            existing["CONT1"] = TargetTable;

            var plan = HmiTagSelection.ForMemoryTags(ClassProgram, existing, TargetTable);

            Assert.AreEqual(0, plan.Displaced.Count);
            Assert.AreEqual(2, plan.Links.Count);
        }
    }
}
