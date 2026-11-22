using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.Siemens
{
    /// <remarks>Changing some rows of a PLC tag table; see <see cref="TagTableEditor"/>.</remarks>
    public partial class Portal
    {
        /// <summary>Changes some rows of a PLC tag table, leaving the others as they are.</summary>
        /// <param name="softwarePath">Full path to the PLC software, e.g. <c>PLC_1</c>.</param>
        /// <param name="edit">The tags to remove, change in place and add.</param>
        /// <param name="backupDirectory">Where the program's tag tables are exported first.</param>
        /// <returns><c>{ table, removed, set, added, tags }</c>: what was done, and the table's tags after it.</returns>
        /// <exception cref="PortalException">
        /// The table or a tag is missing, a name is in use, or TIA Portal refused a type or an address - what was done of
        /// the edit is then undone.
        /// </exception>
        public JsonObject EditTagTable(string softwarePath, TagTableEdit edit, string backupDirectory)
        {
            _logger?.LogInformation("Editing tag table {Table} in {SoftwarePath}...", edit?.Table, softwarePath);

            try
            {
                var requested = edit ?? throw new PortalException(PortalErrorCode.InvalidParams, "An edit is required");
                var software = RequireSoftwareForWrite(softwarePath, backupDirectory);
                var table = RequireTable(software, requested.Table);
                var editor = new TagTableEditor(table, requested, CurrentProject!.LanguageSettings.EditingLanguage);

                editor.Check();
                var tags = editor.Apply();

                return new JsonObject
                {
                    ["table"] = requested.Table,
                    ["removed"] = new JsonArray(requested.Remove.Select(name => (JsonNode)name).ToArray()),
                    ["set"] = new JsonArray(requested.Set.Select(row => (JsonNode)row.Name).ToArray()),
                    ["added"] = new JsonArray(requested.Add.Select(row => (JsonNode)row.Name).ToArray()),
                    ["tags"] = new JsonArray(tags.Select(name => (JsonNode)name).ToArray())
                };
            }
            catch (Exception ex)
            {
                throw DecorateTagFailure(ex, softwarePath, edit?.Table ?? string.Empty, "EditTagTable");
            }
        }
    }
}
