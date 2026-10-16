using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.Spec.Tests
{
    /// <remarks>
    /// The links an import is checked for before TIA Portal sees it. A missing one ended the project
    /// session on 2026-10-14, so a link this reader misses is a link nobody checks.
    /// </remarks>
    [TestClass]
    public sealed class HmiDocumentLinksTests
    {
        private static string Asset(string name)
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "hmi", name));
        }

        [TestMethod]
        public void Read_TeachersPrincipal_FindsEveryTagItLinksTo()
        {
            var links = HmiDocumentLinks.Read(Asset("principal-reference.xml"));

            CollectionAssert.AreEquivalent(new[] { "CONT1", "Marxa", "Paro", "KM1", "alarma" }, links.Named(HmiDocumentLinks.TagKind).ToArray());
        }

        /// <remarks>The overview's symbolic field links a text list, and its change event a tag through a parameter.</remarks>
        [TestMethod]
        public void Read_ClassOverview_FindsTheTextListAndTheParameterTag()
        {
            var links = HmiDocumentLinks.Read(Asset("overview-reference.xml"));

            CollectionAssert.Contains(links.Named(HmiDocumentLinks.TextListKind).ToArray(), "TextList_ScreenNames");
            CollectionAssert.Contains(links.Named(HmiDocumentLinks.TagKind).ToArray(), "Tag_ScreenNumber");
        }

        [TestMethod]
        public void Read_ActivateScreenButton_FindsTheScreenNotATag()
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""Go"", ""press"": ""activateScreen"", ""screen"": ""Trends"" } ] }");

            var links = HmiDocumentLinks.Read(HmiScreenDocument.Build(screen));

            CollectionAssert.AreEqual(new[] { "Trends" }, links.Named(HmiDocumentLinks.ScreenKind).ToArray());
            Assert.AreEqual(0, links.Named(HmiDocumentLinks.TagKind).Count);
        }

        [TestMethod]
        public void Read_GeneratedTagTable_FindsItsConnection()
        {
            var table = HmiTagTableDocument.Build("PLC tags", "HMI_Connection_1", new[] { new HmiTagLink("Marxa", "Marxa") });

            var links = HmiDocumentLinks.Read(table);

            CollectionAssert.AreEqual(new[] { "HMI_Connection_1" }, links.Named(HmiDocumentLinks.ConnectionKind).ToArray());
        }

        /// <remarks>The overview's logo is a graphic of the project, linked like any other object.</remarks>
        [TestMethod]
        public void Read_ClassOverview_FindsTheLogoAsAPicture()
        {
            var links = HmiDocumentLinks.Read(Asset("overview-reference.xml"));

            Assert.AreEqual(1, links.Named(HmiDocumentLinks.PictureKind).Count);
        }

        /// <remarks>A role nobody named is kept, so that the caller can refuse what it cannot check.</remarks>
        [TestMethod]
        public void Read_UnknownRole_IsKeptUnderItsOwnName()
        {
            var links = HmiDocumentLinks.Read(@"<Document><Hmi.Screen.Screen ID=""0""><AttributeList><Name>S</Name></AttributeList><LinkList><Mystery TargetID=""@OpenLink""><Name>X</Name></Mystery></LinkList></Hmi.Screen.Screen></Document>");

            CollectionAssert.AreEqual(new[] { "Mystery" }, links.Kinds.ToArray());
        }

        [TestMethod]
        public void NameOf_Screen_IsTheScreensName()
        {
            Assert.AreEqual("principal", HmiDocumentLinks.NameOf(Asset("principal-reference.xml")));
        }
    }
}
