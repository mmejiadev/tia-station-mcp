using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Reads <c>.tia-mcp/policy.json</c>.
    /// </summary>
    /// <remarks>
    /// The file is per project, because what a session may touch is a property of the project and
    /// not of the machine or the person. Shape:
    ///
    /// <code>
    /// {
    ///   "study":    { "allow": ["PLC_0/*"], "deny": ["PLC_0/Safety/*"] },
    ///   "workshop": { "allow": ["PLC_0/Blocks/FB_Station"], "deny": [] }
    /// }
    /// </code>
    ///
    /// A missing file denies everything rather than defaulting to something workable. That is
    /// inconvenient exactly once, when it is set up, and correct every time after.
    /// </remarks>
    internal static class WritePolicyFile
    {
        /// <summary>
        /// Comments and trailing commas are accepted.
        /// </summary>
        /// <remarks>
        /// Strict JSON is the wrong dialect for a file whose whole purpose is a decision someone
        /// took about what a machine may touch. The reason a target is on the list belongs next to
        /// the target, and a policy that refused to load because of a comment would be edited by
        /// deleting the reason.
        /// </remarks>
        private static readonly JsonSerializerOptions Lenient = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private const string StudySection = "study";
        private const string WorkshopSection = "workshop";
        private const string AllowKey = "allow";
        private const string DenyKey = "deny";

        internal static WritePolicy Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return WritePolicy.DenyEverything();
            }

            try
            {
                // Read as plain dictionaries rather than through a DTO: the shape is two levels of
                // string-to-list and a type declared only for the deserializer to fill is one more
                // thing to keep in step with the file it mirrors.
                var sections = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(
                    File.ReadAllText(path),
                    Lenient);

                return new WritePolicy(BuildRules(sections));
            }
            catch (Exception exception) when (!(exception is PortalException))
            {
                // A policy that cannot be read is not a policy that allows everything. Failing
                // here refuses the whole session rather than letting it run unprotected.
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The write policy at '{path}' could not be read: {exception.Message}. " +
                    "Refusing to run without one rather than running unprotected.",
                    null,
                    exception);
            }
        }

        private static Dictionary<OperationMode, ModeRules> BuildRules(
            Dictionary<string, Dictionary<string, List<string>>>? sections)
        {
            var rules = new Dictionary<OperationMode, ModeRules>();

            if (sections == null)
            {
                return rules;
            }

            // Case-insensitive so "Study" and "study" both work: a policy silently ignored because
            // of a capital letter would look exactly like a policy that denies everything.
            var byName = new Dictionary<string, Dictionary<string, List<string>>>(sections, StringComparer.OrdinalIgnoreCase);

            AddSection(rules, byName, StudySection, OperationMode.Study);
            AddSection(rules, byName, WorkshopSection, OperationMode.Workshop);

            return rules;
        }

        private static void AddSection(
            Dictionary<OperationMode, ModeRules> rules,
            Dictionary<string, Dictionary<string, List<string>>> sections,
            string name,
            OperationMode mode)
        {
            if (!sections.TryGetValue(name, out var section) || section == null)
            {
                return;
            }

            var lists = new Dictionary<string, List<string>>(section, StringComparer.OrdinalIgnoreCase);

            rules[mode] = new ModeRules(mode, ListOrEmpty(lists, AllowKey), ListOrEmpty(lists, DenyKey));
        }

        private static List<string> ListOrEmpty(Dictionary<string, List<string>> lists, string key)
        {
            return lists.TryGetValue(key, out var list) && list != null ? list : new List<string>();
        }
    }
}
