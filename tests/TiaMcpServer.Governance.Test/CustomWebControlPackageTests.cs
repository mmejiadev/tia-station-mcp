using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using TiaMcpServer.CustomWebControls;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The package TIA Portal reads: its layout was measured on 2026-11-15 with a pilot light that TIA Portal recognised,
    /// placed, compiled and ran. A package that drifts from that layout is ignored by TIA Portal without a word.
    /// </remarks>
    [TestClass]
    public sealed class CustomWebControlPackageTests
    {
        private static readonly byte[] WebCc = Encoding.UTF8.GetBytes("/* webcc */");

        [TestMethod]
        public void Build_APackage_HoldsTheThreeEntriesWithForwardSlashes()
        {
            var zip = CustomWebControlPackage.Build(CustomWebControlCatalog.Get("PilotLight"), WebCc);

            using var archive = new ZipArchive(new MemoryStream(zip));
            CollectionAssert.AreEquivalent(new[] { "manifest.json", "control/index.html", "control/libs/webcc.min.js" }, archive.Entries.Select(entry => entry.FullName).ToArray());
        }

        [TestMethod]
        public void Build_NoWebCc_ThrowsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlPackage.Build(CustomWebControlCatalog.Get("PilotLight"), Array.Empty<byte>()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void ReadManifest_ABuiltPackage_ReadsBackItsNamePropertiesAndEvents()
        {
            var definition = CustomWebControlCatalog.Get("Motor");

            var summary = CustomWebControlPackageReader.ReadManifest(new MemoryStream(CustomWebControlPackage.Build(definition, WebCc)));

            Assert.AreEqual("TiaStation.Motor", summary.Name);
            CollectionAssert.AreEqual(definition.Properties.Select(property => property.Name).ToArray(), summary.Properties.Select(property => property.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "Clicked" }, summary.Events.ToArray());
        }

        [TestMethod]
        public void ReadManifest_AColourProperty_IsReadBackAsAColour()
        {
            var summary = CustomWebControlPackageReader.ReadManifest(new MemoryStream(CustomWebControlPackage.Build(CustomWebControlCatalog.Get("PilotLight"), WebCc)));

            Assert.AreEqual(CustomWebControlPropertyKind.Color, summary.Properties.Single(property => property.Name == "OnColor").Kind);
        }

        /// <remarks>Without it a control is placed at 640 x 320 with a title bar (measured 2026-11-15).</remarks>
        [TestMethod]
        public void ReadManifest_ABuiltPackage_ReadsBackTheDefaultSize()
        {
            var summary = CustomWebControlPackageReader.ReadManifest(new MemoryStream(CustomWebControlPackage.Build(CustomWebControlCatalog.Get("PilotLight"), WebCc)));

            Assert.AreEqual(120, summary.DefaultWidth);
            Assert.AreEqual(150, summary.DefaultHeight);
        }

        [TestMethod]
        public void ReadLibrary_ABuiltPackage_GivesBackWebCc()
        {
            var library = CustomWebControlPackageReader.ReadLibraryAt(new MemoryStream(CustomWebControlPackage.Build(CustomWebControlCatalog.Get("Valve"), WebCc)), CustomWebControlPackage.LibraryEntry);

            CollectionAssert.AreEqual(WebCc, library);
        }

        [TestMethod]
        public void Manifest_AControl_NamesItsPackageAsAGuidType()
        {
            var definition = CustomWebControlCatalog.Get("Tank");

            var manifest = CustomWebControlManifestWriter.WriteText(definition);

            StringAssert.Contains(manifest, "guid://" + definition.Identity.PackageId.ToString("D").ToUpperInvariant(), StringComparison.Ordinal);
            StringAssert.Contains(manifest, CustomWebControlPropertyKinds.ColorReference, StringComparison.Ordinal);
        }

        [TestMethod]
        public void Page_AControl_LoadsWebCcStartsItAndCallsRender()
        {
            var page = Encoding.UTF8.GetString(CustomWebControlPageWriter.Write(CustomWebControlCatalog.Get("Gauge")));

            StringAssert.Contains(page, "src=\"libs/webcc.min.js\"", StringComparison.Ordinal);
            StringAssert.Contains(page, "WebCC.start(", StringComparison.Ordinal);
            StringAssert.Contains(page, "render(WebCC.Properties)", StringComparison.Ordinal);
        }

        /// <remarks>Measured 2026-11-15/16: pages declaring a Content-Security-Policy drew blank in TIA Portal (with another WebCC copy), so none is written.</remarks>
        [TestMethod]
        public void Page_AControl_DeclaresNoContentSecurityPolicy()
        {
            var page = Encoding.UTF8.GetString(CustomWebControlPageWriter.Write(CustomWebControlCatalog.Get("Motor")));

            Assert.IsFalse(page.Contains("Content-Security-Policy"));
        }

        /// <remarks>A package in a project folder is not trusted: a manifest that would fill the memory is refused, not read.</remarks>
        [TestMethod]
        public void ReadManifest_AManifestOverTheLimit_ThrowsInvalidState()
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            using (var entry = archive.CreateEntry("manifest.json").Open())
            {
                var block = new byte[64 * 1024];
                for (var written = 0L; written <= CustomWebControlPackageReader.MaximumManifestBytes; written += block.Length)
                {
                    entry.Write(block, 0, block.Length);
                }
            }

            stream.Position = 0;
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlPackageReader.ReadManifest(stream));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <remarks>Only where Siemens keeps it: a webcc.min.js elsewhere in a package is not taken (security review of 2026-11-15).</remarks>
        [TestMethod]
        public void ReadLibrary_AWebCcOutsideTheKnownFolders_IsNotTaken()
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            using (var entry = archive.CreateEntry("somewhere/webcc.min.js").Open())
            {
                entry.Write(WebCc, 0, WebCc.Length);
            }

            var found = CustomWebControlPackageReader.LibraryEntries.Select(path => CustomWebControlPackageReader.ReadLibraryAt(new MemoryStream(stream.ToArray()), path));

            Assert.IsTrue(found.All(library => library == null));
        }

        [TestMethod]
        public void ReadLibraryAt_APathOutsideTheKnownFolders_ThrowsArgument()
        {
            var package = CustomWebControlPackage.Build(CustomWebControlCatalog.Get("Valve"), WebCc);

            Assert.ThrowsException<ArgumentException>(() => CustomWebControlPackageReader.ReadLibraryAt(new MemoryStream(package), "somewhere/webcc.min.js"));
        }

        [TestMethod]
        public void FileName_AControl_IsItsGuidInBracesAsAZip()
        {
            var identity = CustomWebControlCatalog.Get("Conveyor").Identity;

            Assert.AreEqual("{" + identity.PackageId.ToString("D").ToUpperInvariant() + "}.zip", CustomWebControlPackage.FileName(identity));
        }
    }
}
