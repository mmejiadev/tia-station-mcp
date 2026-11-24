using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Comparing two versions of a library type. It writes nothing but a scratch folder in %TEMP%, removed afterwards.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CompareLibraryTypeVersions"), Description("Compare two versions of a library type, in the open project's library or a global library file (opened read-only, never changed). Answers with what TIA Portal compares - each version's number, default, author, comment, time and GUID - and with what they hold: both are exported as SIMATIC SD documents and compared document by document, line endings and trailing blanks aside, giving contentIdentical, each document's state (identical, different, onlyInVersion, onlyInOtherVersion), the first line where they part, and both texts of a document that differs. TIA Portal's own comparison looks at nothing of the content, so two versions it calls different may hold the same. A type TIA Portal exports no document of - a faceplate, and on V21 any type in a global library (CopyLibraryTypes brings it into the project to compare its content) - is compared on what describes it only, and contentNote says why.")]
        public static ResponseLibraryTypes CompareLibraryTypeVersions(
            [Description("typeName: the type, exactly as GetLibraryTypes names it")] string typeName,
            [Description("version: the first version, e.g. '0.0.1'")] string version,
            [Description("otherVersion: the version to compare it with, e.g. '0.0.2'")] string otherVersion,
            [Description("globalLibraryPath: a global library file, e.g. 'C:\\Libraries\\Motors\\Motors.al21'; empty for the open project's library")] string globalLibraryPath = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var compared = Portal.CompareLibraryTypeVersions((typeName ?? string.Empty).Trim(), LibraryVersionNumber.Parse(version, "version"), LibraryVersionNumber.Parse(otherVersion, "otherVersion"), globalLibraryPath);

                return new ResponseLibraryTypes(compared)
                {
                    Message = DescribeComparison(compared, $"{version} and {otherVersion} of '{typeName}'"),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to compare versions of '{typeName}'");
            }
        }

        private static string DescribeComparison(JsonObject compared, string versions)
        {
            var identical = compared["contentIdentical"]?.GetValue<bool>();

            return identical == null
                ? $"{versions} compared on what describes them; TIA Portal exports nothing of their content"
                : $"{versions} hold {(identical.Value ? "the same content" : "different content")}";
        }
    }
}
