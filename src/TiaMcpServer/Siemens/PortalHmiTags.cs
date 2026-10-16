using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Hmi.Tag;
using Siemens.Engineering.SW.Tags;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// HMI tags generated from a PLC program: one per memory tag, named as the PLC tag, the way the
    /// class projects are built by hand. See <see cref="HmiTagSelection"/> for what is chosen and
    /// <see cref="HmiTagTableDocument"/> for what is written.
    /// </remarks>
    public partial class Portal
    {
        private const string GeneratedDirectoryName = "generated";

        /// <summary>
        /// Gives every memory tag of a PLC program an HMI tag on a classic WinCC panel, in one table.
        /// </summary>
        /// <param name="request">The PLC program, the panel, the table and the connection.</param>
        /// <param name="backupDirectory">
        /// The directory the backup registry allocated for this change. The panel is exported there
        /// first, and the generated table is kept in its <c>generated</c> folder.
        /// </param>
        /// <returns>
        /// What was written and what was left out. Nothing is written when every memory tag already
        /// has an HMI tag in another table.
        /// </returns>
        /// <exception cref="PortalException">
        /// A path does not resolve; the target table holds tags the generated one would delete; the
        /// connection is not one the panel's tags already use; the backup was incomplete so nothing
        /// was written; or TIA Portal rejected the table.
        /// </exception>
        public HmiTagPlan GenerateHmiTags(HmiTagGenerationRequest request, string backupDirectory)
        {
            _logger?.LogInformation("Generating HMI tags for {HmiPath} from {PlcSoftwarePath}...", request.HmiPath, request.PlcSoftwarePath);

            try
            {
                RequireTableAndConnection(request);

                var software = RequireSoftware(request.PlcSoftwarePath);
                var hmi = RequireHmiTarget(request.HmiPath);
                var plan = HmiTagSelection.ForMemoryTags(ReadAllTags(software.TagTableGroup), ReadHmiTags(hmi.TagFolder), request.TableName);

                RequireNothingDisplaced(plan, request.TableName);

                if (plan.Links.Count == 0)
                {
                    return plan;
                }

                var exports = SaveHmiBackup(hmi, backupDirectory);

                var table = WriteGenerated(request.TableName, HmiTagTableDocument.Build(request.TableName, request.ConnectionName, plan.Links), backupDirectory);
                RequireKnownLinks(hmi, File.ReadAllText(table.FullName), exports);
                RequireImported(hmi.TagFolder.TagTables.Import(table, ImportOptions.Override).Select(imported => imported.Name), request.TableName);

                return plan;
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "GenerateHmiTags"), "GenerateHmiTags", request.HmiPath, request.TableName);
            }
        }

        private static void RequireTableAndConnection(HmiTagGenerationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TableName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "tableName is required");
            }

            if (string.IsNullOrWhiteSpace(request.ConnectionName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "connectionName is required");
            }
        }

        /// <remarks>
        /// Importing replaces a table whole. A tag somebody added by hand to the target table — the
        /// screen-number tag the teacher's default table holds, say — would be deleted without a
        /// word, so a table holding anything this generator would not write again is refused.
        /// </remarks>
        private static void RequireNothingDisplaced(HmiTagPlan plan, string tableName)
        {
            if (plan.Displaced.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"HMI tag table '{tableName}' holds tags that are not generated from memory tags, and regenerating it would delete them: " +
                    $"{string.Join(", ", plan.Displaced)}. Generate into another table.");
            }
        }

        private static List<TagInfo> ReadAllTags(PlcTagTableGroup group)
        {
            var tags = group.TagTables.SelectMany(table => new TagTableReader().ReadEntries(table)).ToList();

            foreach (var subgroup in group.Groups)
            {
                tags.AddRange(ReadAllTags(subgroup));
            }

            return tags;
        }

        private static Dictionary<string, string> ReadHmiTags(TagFolder folder)
        {
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in folder.TagTables)
            {
                foreach (var tag in table.Tags)
                {
                    tags[tag.Name] = table.Name;
                }
            }

            foreach (var subfolder in folder.Folders)
            {
                foreach (var entry in ReadHmiTags(subfolder))
                {
                    tags[entry.Key] = entry.Value;
                }
            }

            return tags;
        }
    }
}
