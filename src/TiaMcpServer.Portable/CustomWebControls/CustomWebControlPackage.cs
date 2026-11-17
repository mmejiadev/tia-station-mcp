using System;
using System.IO;
using System.IO.Compression;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Builds the package TIA Portal reads a Custom Web Control from: <c>{GUID}.zip</c>.</summary>
    /// <remarks>
    /// Measured 2026-11-15 on TIA Portal V21: a zip holding <c>manifest.json</c>, <c>control/index.html</c> and
    /// <c>control/libs/webcc.min.js</c>, its entries named with forward slashes, placed in the project's
    /// <c>UserFiles\CustomControls</c> as <c>{GUID}.zip</c>, is recognised when the project is next opened. WebCC is
    /// Siemens' library and is not carried by this server: the caller passes the copy from the local TIA Portal.
    /// </remarks>
    public static class CustomWebControlPackage
    {
        /// <summary>The manifest's entry.</summary>
        public const string ManifestEntry = "manifest.json";

        /// <summary>The page's entry.</summary>
        public const string PageEntry = "control/index.html";

        /// <summary>WebCC's entry.</summary>
        public const string LibraryEntry = "control/" + CustomWebControlPageWriter.LibraryPath;

        /// <summary>The package's file name for a control.</summary>
        /// <param name="identity">The control.</param>
        /// <returns>E.g. <c>{7D554746-B469-4B99-9985-8F0A03E2BC41}.zip</c>.</returns>
        public static string FileName(CustomWebControlIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return "{" + identity.PackageId.ToString("D").ToUpperInvariant() + "}.zip";
        }

        /// <summary>Builds the package.</summary>
        /// <param name="definition">The checked definition.</param>
        /// <param name="webcc">WebCC, <c>webcc.min.js</c>, as the local TIA Portal ships it.</param>
        /// <returns>The zip.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when no WebCC is given.</exception>
        public static byte[] Build(CustomWebControlDefinition definition, byte[] webcc)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (webcc == null || webcc.Length == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "WebCC (webcc.min.js) was not found in the local TIA Portal, and a Custom Web Control does not start without it");
            }

            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                Add(zip, ManifestEntry, CustomWebControlManifestWriter.Write(definition));
                Add(zip, PageEntry, CustomWebControlPageWriter.Write(definition));
                Add(zip, LibraryEntry, webcc);
            }

            return stream.ToArray();
        }

        private static void Add(ZipArchive zip, string name, byte[] content)
        {
            using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
            entry.Write(content, 0, content.Length);
        }
    }
}
