using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.Compare;
using Siemens.Engineering.Library.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Comparing two versions of a library type. Measured 2026-11-24 on TIA Portal V21: <c>LibraryTypeVersion.CompareTo</c> gives
    /// only what describes each version (GUID, number, default, author, comment, time, library), the same for two versions of
    /// different content; so the content is compared from each version exported as SIMATIC SD documents, which
    /// <c>ExportAsDocuments</c> writes for the format <c>SimaticSD</c> and refuses for <c>s7dcl</c>.
    /// </remarks>
    public partial class Portal
    {
        private const string SimaticSdFormat = "SimaticSD";

        /// <summary>Compares two versions of a library type: what describes them, and what they hold.</summary>
        /// <param name="typeName">The type, as GetLibraryTypes names it.</param>
        /// <param name="version">The first version.</param>
        /// <param name="otherVersion">The version to compare it with.</param>
        /// <param name="globalLibraryPath">A global library file, opened read-only; empty for the project library.</param>
        /// <returns>
        /// <c>{ library, type, kind, version, otherVersion, properties: [ { description, status, value, otherValue } ],
        /// contentIdentical, documents: [ { name, state, firstDifferentLine, text?, otherText? } ] }</c>; for a type TIA Portal
        /// exports no document of, <c>contentIdentical</c> is null and <c>contentNote</c> says why.
        /// </returns>
        /// <exception cref="PortalException">No such type or version, or no project open.</exception>
        public JsonObject CompareLibraryTypeVersions(string typeName, Version version, Version otherVersion, string globalLibraryPath)
        {
            try
            {
                if (version == null || otherVersion == null)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "version and otherVersion are both required");
                }

                if (string.IsNullOrWhiteSpace(globalLibraryPath))
                {
                    return CompareVersions(RequireLibraryType(typeName).Type, (version, otherVersion), ProjectLibraryName);
                }

                var (read, warning) = GlobalLibraryAccess.Use(RequirePortal(), globalLibraryPath, global =>
                    CompareVersions(RequireTypeIn(global.TypeFolder, typeName), (version, otherVersion), global.Path?.FullName));

                return WithWarning(read, warning);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CompareLibraryTypeVersions", PortalErrorCode.ExportFailed), "CompareLibraryTypeVersions", globalLibraryPath, typeName ?? string.Empty);
            }
        }

        private JsonObject CompareVersions(LibraryType type, (Version Left, Version Right) numbers, string? library)
        {
            var left = RequireVersionOf(type, numbers.Left);
            var right = RequireVersionOf(type, numbers.Right);

            var compared = new JsonObject
            {
                ["library"] = library,
                ["type"] = type.Name,
                ["kind"] = type.GetType().Name,
                ["version"] = LibraryVersionNumber.Format(left.VersionNumber),
                ["otherVersion"] = LibraryVersionNumber.Format(right.VersionNumber),
                ["properties"] = DescribeProperties(left.CompareTo(right))
            };

            AddContentComparison(compared, type, (left, right));

            return compared;
        }

        private void AddContentComparison(JsonObject compared, LibraryType type, (LibraryTypeVersion Left, LibraryTypeVersion Right) versions)
        {
            if (!MayExportDocuments(type))
            {
                NoteContentNotCompared(compared, $"TIA Portal exports no SIMATIC SD document of a {type.GetType().Name}");
                return;
            }

            var scratch = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                var left = ExportVersionDocuments(versions.Left, type.Name, Path.Combine(scratch, "left"));
                var right = ExportVersionDocuments(versions.Right, type.Name, Path.Combine(scratch, "right"));
                var documents = LibraryDocumentComparer.Compare(left, right);

                compared["contentIdentical"] = LibraryDocumentComparer.AreIdentical(documents);
                compared["documents"] = new JsonArray(documents.Select(document => (JsonNode)DescribeDocument(document, left, right)).ToArray());
            }
            catch (EngineeringTargetInvocationException refused)
            {
                // Only an export TIA Portal refuses is noted here; any other failure goes on to the decoration point.
                NoteContentNotCompared(compared, $"TIA Portal refused to export the versions' documents ({refused.Message.Trim()})");
            }
            finally
            {
                RemoveComparisonFolder(scratch);
            }
        }

        /// <remarks>
        /// Measured 2026-11-24 on V21: for a type in a global library <c>GetSupportedExportFormats</c> is refused ("Cannot get
        /// supported export formats for a type in the global library"), so there the export is tried and a refusal noted.
        /// </remarks>
        private static bool MayExportDocuments(LibraryType type)
        {
            try
            {
                return type.GetSupportedExportFormats().Cast<object>().Any(format => string.Equals(format?.ToString(), SimaticSdFormat, StringComparison.Ordinal));
            }
            catch (EngineeringTargetInvocationException)
            {
                // Not swallowed: the export that follows is tried, and its own refusal is reported in the answer.
                return true;
            }
        }

        private static void NoteContentNotCompared(JsonObject compared, string reason)
        {
            compared["contentIdentical"] = null;
            compared["contentNote"] = $"{reason}, so only what describes the two versions is compared";
        }

        /// <remarks>The scratch folder is short on purpose: TIA Portal refuses a path over 143 characters (measured 2026-11-02).</remarks>
        private static Dictionary<string, string> ExportVersionDocuments(LibraryTypeVersion version, string typeName, string directory)
        {
            Directory.CreateDirectory(directory);
            version.ExportAsDocuments(new DirectoryInfo(directory), typeName, SimaticSdFormat, LibraryExportOptions.None);

            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(file => file.Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar), File.ReadAllText, StringComparer.OrdinalIgnoreCase);
        }

        private static JsonObject DescribeDocument(DocumentComparison document, Dictionary<string, string> left, Dictionary<string, string> right)
        {
            var described = new JsonObject
            {
                ["name"] = document.Name,
                ["state"] = DocumentStateName(document.State),
                ["firstDifferentLine"] = document.FirstDifferentLine
            };

            if (document.State != DocumentComparisonState.Identical)
            {
                described["text"] = left.TryGetValue(document.Name, out var text) ? text : null;
                described["otherText"] = right.TryGetValue(document.Name, out var otherText) ? otherText : null;
            }

            return described;
        }

        private static string DocumentStateName(DocumentComparisonState state)
        {
            return state switch
            {
                DocumentComparisonState.Identical => "identical",
                DocumentComparisonState.Different => "different",
                DocumentComparisonState.OnlyLeft => "onlyInVersion",
                DocumentComparisonState.OnlyRight => "onlyInOtherVersion",
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised document comparison state: {state}")
            };
        }

        private static JsonArray DescribeProperties(DetailedCompareResult result)
        {
            return new JsonArray(result.Properties.Select(property => (JsonNode)new JsonObject
            {
                ["description"] = property.Description,
                ["status"] = PropertyStatusName(property.DetailCompareStatus),
                ["value"] = property.LeftValue?.ToString(),
                ["otherValue"] = property.RightValue?.ToString()
            }).ToArray());
        }

        private static string PropertyStatusName(DetailCompareStatus status)
        {
            return status switch
            {
                DetailCompareStatus.ObjectsIdentical => "identical",
                DetailCompareStatus.ObjectsDifferent => "different",
                DetailCompareStatus.NotCompared => "notCompared",
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal answered a comparison status this server does not know: {status}")
            };
        }

        private static LibraryTypeVersion RequireVersionOf(LibraryType type, Version number)
        {
            var normalised = LibraryVersionNumber.Normalise(number);

            return type.Versions.FirstOrDefault(version => LibraryVersionNumber.Normalise(version.VersionNumber) == normalised)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"'{type.Name}' has no version {LibraryVersionNumber.Format(normalised)}; it has {string.Join(", ", type.Versions.Select(version => LibraryVersionNumber.Format(version.VersionNumber)))}");
        }

        private static LibraryType RequireTypeIn(LibraryTypeFolder root, string typeName)
        {
            var types = LibraryTypeReader.ByName(root);

            return types.TryGetValue(typeName ?? string.Empty, out var found)
                ? found.Type
                : throw new PortalException(PortalErrorCode.NotFound, $"The global library has no type '{typeName}'. It holds: {string.Join(", ", types.Keys)}");
        }

        /// <remarks>The comparison is done by then, so a folder left in %TEMP% is logged rather than reported as a failed comparison.</remarks>
        private void RemoveComparisonFolder(string scratch)
        {
            if (!Directory.Exists(scratch))
            {
                return;
            }

            try
            {
                Directory.Delete(scratch, recursive: true);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                _logger?.LogWarning(failure, "The folder {Scratch} two library versions were exported to could not be removed", scratch);
            }
        }
    }
}
