using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Reads a Custom Web Control's package: its manifest, and the WebCC library it carries.</summary>
    /// <remarks>
    /// Siemens' packages in TIA Portal V21 put WebCC at <c>control/libs/webcc.min.js</c> or <c>control/static/webcc.min.js</c>
    /// and their manifests may start with a byte order mark (measured 2026-11-15), so both are allowed for. A property whose
    /// type is an object, an array or a <c>$ref</c> other than a colour is listed with no kind: this server does not set it.
    /// </remarks>
    public static class CustomWebControlPackageReader
    {
        private const string LibraryFileName = "webcc.min.js";

        /// <summary>The largest manifest read, in bytes: Siemens' are a few kilobytes.</summary>
        public const long MaximumManifestBytes = 1024 * 1024;

        /// <summary>The largest WebCC library read, in bytes: Siemens' is well under one megabyte.</summary>
        public const long MaximumLibraryBytes = 8 * 1024 * 1024;

        /// <summary>Where Siemens' packages keep WebCC, in the order it is looked for (measured 2026-11-15).</summary>
        /// <remarks>
        /// <c>control/libs</c> first: the copy there, with Siemens' version header, is the one every control drew with in TIA
        /// Portal (measured 2026-11-16). Controls built with the copy in AuditViewer's <c>control/static</c> - the same
        /// version, packed otherwise - and a Content-Security-Policy drew blank; the two were not told apart (see
        /// <see cref="CustomWebControlPageWriter"/>). A library anywhere else is not taken.
        /// </remarks>
        public static readonly IReadOnlyList<string> LibraryEntries = new[] { CustomWebControlPackage.LibraryEntry, "control/static/" + LibraryFileName };

        /// <summary>Reads what a package's manifest says.</summary>
        /// <param name="zip">The package.</param>
        /// <returns>Its summary.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when it holds no readable manifest.</exception>
        public static CustomWebControlSummary ReadManifest(Stream zip)
        {
            using var archive = Open(zip);
            var entry = archive.Entries.FirstOrDefault(found => string.Equals(found.FullName, CustomWebControlPackage.ManifestEntry, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(PortalErrorCode.InvalidState, "The package holds no manifest.json at its root");

            using var document = ParseManifest(entry);
            var control = Member(document.RootElement, "control");
            var identity = Member(control, "identity");
            var api = Member(Member(control, "contracts"), "api");

            var space = Member(Member(Member(control, "environment"), "prerequisites"), "renderingspace");

            return new CustomWebControlSummary(
                (Text(identity, "name"), Text(identity, "displayname")),
                Names(api, "properties").Select(property => (property.Name, KindOf(property.Value))).ToList(),
                Names(api, "events").Select(fired => fired.Name).ToList(),
                (Pixels(space, "defaultwidth"), Pixels(space, "defaultheight")));
        }

        /// <summary>Takes WebCC out of a package, from one place only.</summary>
        /// <param name="zip">The package.</param>
        /// <param name="entryPath">One of <see cref="LibraryEntries"/>.</param>
        /// <returns>The library's bytes; null when the package has none there.</returns>
        /// <exception cref="ArgumentException">The path is not one of <see cref="LibraryEntries"/>: WebCC is taken only where Siemens keeps it.</exception>
        public static byte[]? ReadLibraryAt(Stream zip, string entryPath)
        {
            if (!LibraryEntries.Contains(entryPath ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"WebCC is taken only from {string.Join(" or ", LibraryEntries)}", nameof(entryPath));
            }

            using var archive = Open(zip);
            var entry = archive.Entries.FirstOrDefault(found => string.Equals(found.FullName, entryPath, StringComparison.OrdinalIgnoreCase));

            return entry == null ? null : ReadBounded(entry, MaximumLibraryBytes);
        }

        private static ZipArchive Open(Stream zip)
        {
            try
            {
                return new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
            }
            catch (InvalidDataException failure)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Not a readable zip: {failure.Message}", null, failure);
            }
        }

        private static JsonDocument ParseManifest(ZipArchiveEntry entry)
        {
            using var reader = new StreamReader(new MemoryStream(ReadBounded(entry, MaximumManifestBytes)), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            try
            {
                return JsonDocument.Parse(reader.ReadToEnd());
            }
            catch (JsonException failure)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The package's manifest.json is not JSON: {failure.Message}", null, failure);
            }
        }

        /// <remarks>
        /// The declared length is checked first and the bytes read are counted too, as a damaged or crafted zip can declare
        /// less than it holds: a package in a project folder is not trusted to be what it says.
        /// </remarks>
        private static byte[] ReadBounded(ZipArchiveEntry entry, long maximum)
        {
            if (entry.Length > maximum)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"{entry.FullName} is {entry.Length} bytes; at most {maximum} are read");
            }

            using var source = entry.Open();
            using var copy = new MemoryStream();
            var buffer = new byte[81920];
            int read;

            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (copy.Length + read > maximum)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"{entry.FullName} holds more than its declared {entry.Length} bytes");
                }

                copy.Write(buffer, 0, read);
            }

            return copy.ToArray();
        }

        private static CustomWebControlPropertyKind? KindOf(JsonElement property)
        {
            if (property.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (property.TryGetProperty("$ref", out var reference))
            {
                return reference.ValueKind == JsonValueKind.String && reference.GetString() == CustomWebControlPropertyKinds.ColorReference
                    ? CustomWebControlPropertyKind.Color
                    : (CustomWebControlPropertyKind?)null;
            }

            return property.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
                ? TryParse(type.GetString())
                : null;
        }

        private static CustomWebControlPropertyKind? TryParse(string? type)
        {
            return type switch
            {
                "boolean" => CustomWebControlPropertyKind.Boolean,
                "string" => CustomWebControlPropertyKind.Text,
                "number" => CustomWebControlPropertyKind.Number,
                "integer" => CustomWebControlPropertyKind.WholeNumber,
                _ => null
            };
        }

        private static IEnumerable<(string Name, JsonElement Value)> Names(JsonElement owner, string name)
        {
            var found = Member(owner, name);

            return found.ValueKind == JsonValueKind.Object
                ? found.EnumerateObject().Select(member => (member.Name, member.Value.Clone())).ToList()
                : Enumerable.Empty<(string, JsonElement)>();
        }

        private static JsonElement Member(JsonElement owner, string name)
        {
            return owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out var value) ? value : default;
        }

        private static int Pixels(JsonElement owner, string name)
        {
            var value = Member(owner, name);

            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var pixels) && pixels > 0 ? pixels : 0;
        }

        private static string Text(JsonElement owner, string name)
        {
            var value = Member(owner, name);

            return value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
        }
    }
}
