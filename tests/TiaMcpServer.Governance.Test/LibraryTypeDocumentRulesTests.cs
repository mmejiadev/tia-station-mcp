using System;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Measured 2026-11-08 on TIA Portal V20.</remarks>
    [TestClass]
    public sealed class LibraryTypeDocumentRulesTests
    {
        private const string Function = "{\r\n    S7_EditorMode := \"SCL\";\r\n    S7_Version := \"0.1\"\r\n}\r\nFUNCTION \"FC_One\" : Void\r\n    { S7_Language := \"SCL\" }\r\nEND_FUNCTION\r\n";

        private const string DataType = "TYPE\r\n    UDT_One : STRUCT\r\n        Done : Bool;\r\n    END_STRUCT;\r\nEND_TYPE\r\n";

        private static readonly string[] Nothing = Array.Empty<string>();

        private static readonly string[] HoldsOne = { "FC_One" };

        private static readonly LibraryVersionInfo[] OnlyReleased = { new LibraryVersionInfo(new Version(0, 0, 1), true) };

        private static readonly LibraryVersionInfo[] OneInWork = { new LibraryVersionInfo(new Version(0, 0, 1), true), new LibraryVersionInfo(new Version(0, 0, 2), false) };

        [TestMethod]
        public void Read_AFunctionWithAttributesAbove_IsABlockOfItsName()
        {
            var read = LibraryTypeDocumentRules.Read("FC_One", Function);

            Assert.AreEqual(LibraryTypeDocumentKind.Block, read.Kind);
            Assert.AreEqual("FC_One", read.Name);
        }

        [TestMethod]
        public void Read_AFunctionBlock_IsABlock()
        {
            Assert.AreEqual(LibraryTypeDocumentKind.Block, LibraryTypeDocumentRules.Read("FB_One", "FUNCTION_BLOCK \"FB_One\"\r\nEND_FUNCTION_BLOCK\r\n").Kind);
        }

        [TestMethod]
        public void Read_ADataTypeNamedOnItsOwnLine_IsAPlcType()
        {
            var read = LibraryTypeDocumentRules.Read("UDT_One", DataType);

            Assert.AreEqual(LibraryTypeDocumentKind.PlcType, read.Kind);
            Assert.AreEqual("UDT_One", read.Name);
        }

        [TestMethod]
        public void Read_ACommentAboveTheDeclaration_IsPassed()
        {
            Assert.AreEqual(LibraryTypeDocumentKind.PlcType, LibraryTypeDocumentRules.Read("UDT_One", "// note\r\n" + DataType).Kind);
        }

        [TestMethod]
        public void Read_ABlockCommentAboveTheDeclaration_IsPassed()
        {
            Assert.AreEqual("UDT_One", LibraryTypeDocumentRules.Read("UDT_One", "/* note */\r\n(* other *)\r\n" + DataType).Name);
        }

        [TestMethod]
        public void Read_ABraceInsideAQuotedAttribute_DoesNotEndTheBlock()
        {
            Assert.AreEqual("FC_One", LibraryTypeDocumentRules.Read("FC_One", "{ S7_X := \"a}b\" }\r\nFUNCTION \"FC_One\" : Void\r\nEND_FUNCTION\r\n").Name);
        }

        [TestMethod]
        public void Read_TwoDeclarations_AreRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryTypeDocumentRules.Read("FC_One", Function + "FUNCTION \"FC_Two\" : Void\r\nEND_FUNCTION\r\n"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        /// <remarks>A struct member is a line starting with a name, and it may be called Type.</remarks>
        [TestMethod]
        public void Read_AMemberNamedLikeAKeyword_IsNotASecondDeclaration()
        {
            var read = LibraryTypeDocumentRules.Read("UDT_One", "TYPE\r\n    UDT_One : STRUCT\r\n        Type : Int;\r\n    END_STRUCT;\r\nEND_TYPE\r\n");

            Assert.AreEqual("UDT_One", read.Name);
        }

        [TestMethod]
        [DataRow("ORGANIZATION_BLOCK \"One\"")]
        [DataRow("DATA_BLOCK \"One\"")]
        [DataRow("")]
        [DataRow("{ S7_Version := \"0.1\"")]
        public void Read_WhatCannotBeALibraryType_IsRefused(string text)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryTypeDocumentRules.Read("One", text));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void Read_ADocumentDeclaringAnotherName_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryTypeDocumentRules.Read("FC_Two", Function));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "FC_One", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Read_TheNameInAnotherCase_Passes()
        {
            Assert.AreEqual("FC_One", LibraryTypeDocumentRules.Read("fc_one", Function).Name);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("..\\FC_One")]
        [DataRow("Folder/FC_One")]
        [DataRow("FC_One.s7dcl")]
        public void CheckDocumentName_WhatIsNotAPlainName_IsRefused(string name)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryTypeDocumentRules.CheckDocumentName(name));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void CheckDocumentName_APlainName_Passes()
        {
            LibraryTypeDocumentRules.CheckDocumentName("FC_One");
        }

        [TestMethod]
        public void CheckNameFree_ANameTheProgramHoldsCaseAside_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryTypeDocumentRules.CheckNameFree("fc_one", HoldsOne, Nothing));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void CheckNameFree_ANameTheLibraryHolds_PointsAtANewVersion()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryTypeDocumentRules.CheckNameFree("FC_One", Nothing, HoldsOne));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "CreateLibraryTypeVersionFromDocument", StringComparison.Ordinal);
        }

        [TestMethod]
        public void CheckNameFree_ANewName_Passes()
        {
            LibraryTypeDocumentRules.CheckNameFree("FC_Two", HoldsOne, HoldsOne);
        }

        [TestMethod]
        public void CheckVersion_AVersionInWorkNotAskedToReplace_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryTypeDocumentRules.CheckVersion("FC_One", OneInWork, replaceInWork: false));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void CheckVersion_ReplacingWithNoneInWork_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryTypeDocumentRules.CheckVersion("FC_One", OnlyReleased, replaceInWork: true));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void CheckVersion_ANewVersionOfAReleasedType_Passes()
        {
            LibraryTypeDocumentRules.CheckVersion("FC_One", OnlyReleased, replaceInWork: false);
        }

        [TestMethod]
        public void CheckVersion_ReplacingTheVersionInWork_Passes()
        {
            LibraryTypeDocumentRules.CheckVersion("FC_One", OneInWork, replaceInWork: true);
        }
    }
}
