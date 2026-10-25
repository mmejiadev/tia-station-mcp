using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Checking WinCC Unified scripts. Read only: nothing is written and no script is run.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CheckUnifiedScripts"), Description("Check the scripts of a WinCC Unified screen, or of every screen of the HMI, for faults TIA Portal compiles without a word: event scripts, script animations and their global definitions. Rules: assignment-in-condition (if (a = b) assigns), local-variable-write (a variable taken from Tags(\"X\") reassigned, which leaves the tag as it was), bit-function (SetBitInTag, ResetBitInTag or InvertBitInTag on a Bool: they set or clear a bit and take no value, so SetBitInTag(tag, 0) switches nothing off; or a bit past the tag's width), not-javascript (not(...), 'and', 'or' from other languages), missing-tag (Tags(\"X\") or SetTagValue(\"X\", ...) on a tag the HMI does not have). System function names are not checked: TIA Portal V20 ships no readable list of the Unified ones. Each finding gives the screen, item, event or animated property, script line, rule, severity (error or warning) and how to write it instead. Scripts are read as text, never run. Without screenName every screen is checked. The HMI path is the Unified software's, e.g. 'PC-System_1/HMI_RT_1'.")]
        public static ResponseUnifiedScriptCheck CheckUnifiedScripts(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath,
            [Description("screenName: one screen to check; leave empty to check every screen")] string screenName = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.CheckUnifiedScripts(hmiPath, screenName);
                var findings = content["findings"]!.AsArray();
                var errors = findings.Count(finding => finding!["severity"]!.GetValue<string>() == "error");

                return new ResponseUnifiedScriptCheck(content)
                {
                    Message = $"'{hmiPath}': {content["scripts"]} script(s) on {content["screens"]} screen(s) checked; {errors} error(s), {findings.Count - errors} warning(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to check the scripts of '{hmiPath}'");
            }
        }
    }
}
