using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// WinCC Unified HMI tags: reading them with the HMI's connections, and creating internal tags and
    /// tags linked to PLC tags. See <see cref="UnifiedTagWriter"/> for what was measured.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Reads a WinCC Unified HMI's connections and tags.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software, e.g. <c>HMI_MTP700/HMI_RT_1</c>.</param>
        /// <returns><c>{ connections: [...], tags: [...], systemTags: [...] }</c>; a connection says whether it is integrated, and a tag carries the settings that differ from TIA Portal's defaults.</returns>
        /// <exception cref="PortalException">The path is not a WinCC Unified HMI.</exception>
        public JsonObject GetUnifiedTags(string hmiPath)
        {
            try
            {
                return UnifiedTagReader.Read(RequireUnifiedSoftware(hmiPath), CurrentProject!.LanguageSettings.EditingLanguage);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetUnifiedTags", hmiPath, string.Empty);
            }
        }

        /// <summary>Creates WinCC Unified HMI tags with their settings, and the tag tables and groups they go in, all of them or none.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="list">The tables and tags.</param>
        /// <returns>The tags created, read back.</returns>
        /// <exception cref="PortalException">
        /// A name or table is taken, a name is a system tag's, a table, connection or limit tag does not
        /// exist, a connection has no partner PLC, a linked tag's value does not fit the type TIA Portal gave
        /// it, or TIA Portal refused a tag; in every case nothing is left created.
        /// </exception>
        /// <remarks>Only new objects are made and nothing existing changes, so no backup is taken.</remarks>
        public JsonArray CreateUnifiedTags(string hmiPath, UnifiedTagList list)
        {
            _logger?.LogInformation("Creating {Count} Unified tag(s) on {HmiPath}...", list?.Tags.Count, hmiPath);

            try
            {
                var requested = list ?? throw new PortalException(PortalErrorCode.InvalidParams, "A tag list is required");
                var hmi = RequireUnifiedSoftware(hmiPath);
                var language = CurrentProject!.LanguageSettings.EditingLanguage;
                var writer = new UnifiedTagWriter(hmi, language);

                writer.Check(requested);
                var created = writer.Create(requested);

                return new JsonArray(created.Select(entry => (JsonNode)UnifiedTagReader.Describe(entry.Tag, entry.Table, language)).ToArray());
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "CreateUnifiedTags"), "CreateUnifiedTags", hmiPath, string.Empty);
            }
        }
    }
}
