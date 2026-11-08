using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.Scripts;
using Siemens.Engineering.HmiUnified.UI.Enum;
using Siemens.Engineering.HmiUnified.UI.Widgets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScripts;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The global script modules of a WinCC Unified HMI, the Scripts folder of its project tree. Openness V20 has no property
    /// holding a module's code: it comes and goes as files, measured 2026-11-08. See <see cref="UnifiedScriptModuleRules"/>.
    /// </remarks>
    public partial class Portal
    {
        private const string ScriptModuleScratchPrefix = "TiaMcp_ModuleCheck_";

        /// <summary>Reads the global script modules of a WinCC Unified HMI, with their code.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <returns><c>{ modules: [ { name, code } ] }</c>.</returns>
        /// <exception cref="PortalException">The path is not a WinCC Unified HMI.</exception>
        public JsonObject GetUnifiedScriptModules(string hmiPath)
        {
            try
            {
                var software = RequireUnifiedSoftware(hmiPath);

                return new JsonObject
                {
                    ["modules"] = new JsonArray(software.Scripts.Select(module => (JsonNode)new JsonObject { ["name"] = module.Name, ["code"] = ReadScriptModule(module) }).ToArray())
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetUnifiedScriptModules", hmiPath, string.Empty);
            }
        }

        /// <summary>Writes a global script module to a WinCC Unified HMI.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="module">The module's name and code, and whether it may replace one of that name.</param>
        /// <param name="backupDirectory">Where every module of the HMI is exported before anything changes.</param>
        /// <returns><c>{ name, replaced, findings }</c>; <c>findings</c> are the checker's warnings.</returns>
        /// <exception cref="PortalException">See <see cref="UnifiedScriptModuleRules"/>; or the code is broken, or TIA Portal did not take it.</exception>
        public JsonObject WriteUnifiedScriptModule(string hmiPath, UnifiedScriptModuleWrite module, string backupDirectory)
        {
            _logger?.LogInformation("Writing script module {Module} to {Hmi}...", module?.Name, hmiPath);

            try
            {
                var software = RequireUnifiedSoftware(hmiPath);
                var existing = software.Scripts.Select(held => held.Name).ToList();
                UnifiedScriptModuleRules.Check(module!, existing);
                var findings = CheckScriptModuleCode(software, module!);
                var isReplacing = UnifiedScriptModuleRules.Holds(existing, module!.Name);
                var named = NamedAsHeld(module, existing);

                SaveScriptModulesBackup(software, backupDirectory);
                ImportScriptModule(software, named, backupDirectory, isReplacing);

                return new JsonObject
                {
                    ["name"] = named.Name,
                    ["replaced"] = isReplacing,
                    ["findings"] = new JsonArray(findings.Select(finding => (JsonNode)$"line {finding.Line}: {finding.Rule}: {finding.Message}").ToArray())
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "WriteUnifiedScriptModule"), "WriteUnifiedScriptModule", hmiPath, module?.Name ?? string.Empty);
            }
        }

        /// <remarks>In the spelling the HMI already holds, so that a replace asked for in another case replaces that module.</remarks>
        private static UnifiedScriptModuleWrite NamedAsHeld(UnifiedScriptModuleWrite module, IEnumerable<string> existing)
        {
            var held = existing.FirstOrDefault(name => string.Equals(name, module.Name, StringComparison.OrdinalIgnoreCase));

            return new UnifiedScriptModuleWrite(held ?? module.Name, module.Code, module.Replace);
        }

        /// <summary>Refuses code TIA Portal would import and compile without a word.</summary>
        /// <returns>The checker's warnings; its errors and any syntax error refuse the write.</returns>
        private static IReadOnlyList<UnifiedScriptFinding> CheckScriptModuleCode(UnifiedHmiSoftware software, UnifiedScriptModuleWrite module)
        {
            var findings = UnifiedScriptChecker.Check(module.Code, new UnifiedScriptContext(TagTypes(software)));
            var errors = findings.Where(finding => finding.Severity == UnifiedScriptSeverity.Error).ToList();
            var syntax = SyntaxErrorsOf(software, module.Code);

            if (errors.Count > 0 || syntax.Count > 0)
            {
                var reasons = syntax.Concat(errors.Select(finding => $"line {finding.Line}: {finding.Message}"));

                throw new PortalException(PortalErrorCode.InvalidParams, $"The code of '{module.Name}' is not valid, and TIA Portal would have compiled it without an error: {string.Join("; ", reasons)}");
            }

            return findings;
        }

        /// <remarks>
        /// A script is only checked inside an event handler, so a throw-away screen holds one for the moment of the check,
        /// and the code goes in as the handler's body: measured 2026-11-08 that a module's own import does not check it.
        /// </remarks>
        private static List<string> SyntaxErrorsOf(UnifiedHmiSoftware software, string code)
        {
            var scratch = software.Screens.Create(ScriptModuleScratchPrefix + Guid.NewGuid().ToString("N"));

            try
            {
                var script = scratch.ScreenItems.Create<HmiButton>("Check").EventHandlers.Create(HmiButtonEventType.Tapped).Script;
                script.ScriptCode = UnifiedScriptModuleRules.SyntaxCheckBody(code);

                // A check that gave no answer is not a check that passed.
                var errors = script.SyntaxCheck()?.Errors ?? throw new PortalException(PortalErrorCode.WriteFailed, "TIA Portal's syntax check gave no answer; nothing was written");

                return errors.ToList();
            }
            finally
            {
                scratch.Delete();
            }
        }

        private void SaveScriptModulesBackup(UnifiedHmiSoftware software, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the modules are exported before one changes");
            }

            Directory.CreateDirectory(backupDirectory);
            foreach (var module in software.Scripts)
            {
                module.Export(new DirectoryInfo(Path.GetFullPath(backupDirectory)));

                if (!File.Exists(Path.Combine(backupDirectory, module.Name + UnifiedScriptModuleRules.ScriptExtension)))
                {
                    throw new PortalException(PortalErrorCode.ExportFailed, $"The backup of script module '{module.Name}' was not written; nothing was changed");
                }
            }
        }

        /// <remarks>Read back after: <c>Import</c> answers only true or false, and what matters is that the module holds this code.</remarks>
        private void ImportScriptModule(UnifiedHmiSoftware software, UnifiedScriptModuleWrite module, string backupDirectory, bool isReplacing)
        {
            if (ImportScriptModuleFiles(software, module.Name, module.Code) && HoldsScriptModule(software, module.Name, module.Code))
            {
                return;
            }

            var undone = UndoScriptModule(software, module.Name, backupDirectory, isReplacing);

            throw new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal did not keep the script module '{module.Name}' as written: {undone}. The modules before are in the backup registry.");
        }

        private bool ImportScriptModuleFiles(UnifiedHmiSoftware software, string name, string code)
        {
            var directory = NewScriptModuleDirectory();

            try
            {
                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(Path.Combine(directory, name + UnifiedScriptModuleRules.DescriptionExtension), UnifiedScriptModuleRules.Description(name), encoding);
                File.WriteAllText(Path.Combine(directory, name + UnifiedScriptModuleRules.ScriptExtension), code, encoding);

                return software.Scripts.Import(new DirectoryInfo(directory));
            }
            finally
            {
                DeleteScratchDirectory(directory);
            }
        }

        private bool HoldsScriptModule(UnifiedHmiSoftware software, string name, string code)
        {
            var held = FindScriptModule(software, name);

            return held != null && UnifiedScriptModuleRules.HoldsCode(ReadScriptModule(held), code);
        }

        private static HmiScriptModule? FindScriptModule(UnifiedHmiSoftware software, string name)
        {
            return software.Scripts.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <returns>What became of it, for the refusal to say; a failure here must not replace the failure being reported.</returns>
        /// <remarks>Openness V20 offers a module no <c>Delete</c>, read by reflection 2026-11-08: one that is new can only be replaced, or deleted by hand.</remarks>
        private string UndoScriptModule(UnifiedHmiSoftware software, string name, string backupDirectory, bool isReplacing)
        {
            if (!isReplacing)
            {
                return "what it holds stays in the HMI, as Openness cannot delete a module: replace it, or delete it by hand in TIA Portal";
            }

            try
            {
                var before = File.ReadAllText(Path.Combine(backupDirectory, name + UnifiedScriptModuleRules.ScriptExtension), Encoding.UTF8);

                return ImportScriptModuleFiles(software, name, before) && HoldsScriptModule(software, name, before)
                    ? "the module was imported back from the backup and reads as it did"
                    : "importing the backup back did not restore it";
            }
            catch (Exception failure) when (failure is EngineeringException || failure is IOException || failure is UnauthorizedAccessException)
            {
                return $"undoing it failed too ({failure.Message})";
            }
        }

        private string ReadScriptModule(HmiScriptModule module)
        {
            var directory = NewScriptModuleDirectory();

            try
            {
                module.Export(new DirectoryInfo(directory));
                var file = Directory.GetFiles(directory, "*" + UnifiedScriptModuleRules.ScriptExtension).Single();

                return File.ReadAllText(file, Encoding.UTF8);
            }
            finally
            {
                DeleteScratchDirectory(directory);
            }
        }

        private static string NewScriptModuleDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(directory);

            return directory;
        }
    }
}
