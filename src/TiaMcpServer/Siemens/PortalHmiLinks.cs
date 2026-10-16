using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.SW;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The check every HMI write makes before it imports: that each name the document links to
    /// exists. See <see cref="HmiDocumentLinks"/> for why — a missing connection ended the project
    /// session (measured 2026-10-14) — and for how links are sorted into kinds.
    ///
    /// Fail closed: a kind of link this class does not know how to check is refused, not passed. Data
    /// types are the one kind let through, being a fixed set no document can make up. PLC tags are
    /// checked against every PLC tag table of the project; a link to a data block member is therefore
    /// refused, since nothing here reads data blocks.
    /// </remarks>
    public partial class Portal
    {
        private static readonly HashSet<string> UncheckedLinkKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            HmiDocumentLinks.DataTypeKind
        };

        /// <summary>Refuses a document that links to anything that does not exist.</summary>
        /// <param name="hmi">The panel.</param>
        /// <param name="xml">The document about to be imported.</param>
        /// <param name="panelExports">The panel's export, just written by the backup: the evidence for what the API does not list.</param>
        /// <exception cref="PortalException">Something linked to is missing; the message names every one.</exception>
        private void RequireKnownLinks(HmiTarget hmi, string xml, IReadOnlyList<string> panelExports)
        {
            var links = HmiDocumentLinks.Read(xml);
            var known = KnownByKind(hmi, HmiDocumentLinks.NameOf(xml), panelExports);

            var missing = links.Kinds
                .Where(kind => !UncheckedLinkKinds.Contains(kind))
                .SelectMany(kind => known.TryGetValue(kind, out var names)
                    ? links.Named(kind).Where(name => !names().Contains(name)).Select(name => $"{kind} '{name}'")
                    : new[] { $"links of kind '{kind}', which are not checked and so not imported" })
                .ToList();

            if (missing.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Nothing was imported: it links to what does not exist: {string.Join(", ", missing)}. " +
                    "A link to something missing can end the TIA Portal session, so it is refused here. " +
                    "Create HMI tags with GenerateHmiTags, text lists with GenerateHmiTextList, and screens before the buttons that open them. " +
                    "A connection counts as there once a tag uses it: on a panel with no tag yet, link one PLC tag to it in TIA Portal first.");
            }
        }

        /// <summary>What exists, per kind of link, each read only when a document links to that kind.</summary>
        private Dictionary<string, Func<HashSet<string>>> KnownByKind(HmiTarget hmi, string ownName, IReadOnlyList<string> panelExports)
        {
            return new Dictionary<string, Func<HashSet<string>>>(StringComparer.Ordinal)
            {
                [HmiDocumentLinks.TagKind] = () => Names(ReadHmiTags(hmi.TagFolder).Keys),
                [HmiDocumentLinks.ScreenKind] = () => Names(ListScreens(hmi.ScreenFolder, string.Empty).Select(path => path.Split('/').Last()).Append(ownName)),
                [HmiDocumentLinks.TemplateKind] = () => Names(ListTemplates(hmi.ScreenTemplateFolder, string.Empty).Select(path => path.Split('/').Last())),
                [HmiDocumentLinks.TextListKind] = () => Names(hmi.TextLists.Select(list => list.Name)),
                [HmiDocumentLinks.GraphicListKind] = () => Names(hmi.GraphicLists.Select(list => list.Name)),
                [HmiDocumentLinks.CycleKind] = () => Names(hmi.Cycles.Select(cycle => cycle.Name)),
                [HmiDocumentLinks.ConnectionKind] = () => Names(hmi.Connections.Select(connection => connection.Name).Concat(HmiDocumentLinks.UsedBy(panelExports, HmiDocumentLinks.ConnectionKind))),
                [HmiDocumentLinks.PictureKind] = () => Names(HmiDocumentLinks.UsedBy(panelExports, HmiDocumentLinks.PictureKind)),
                [HmiDocumentLinks.ControllerTagKind] = () => Names(PlcTagNames())
            };
        }

        /// <summary>The name of every tag of every PLC program in the project.</summary>
        private IEnumerable<string> PlcTagNames()
        {
            return SoftwareEntries()
                .Select(entry => entry.Software)
                .OfType<PlcSoftware>()
                .SelectMany(software => ReadAllTags(software.TagTableGroup))
                .Select(tag => tag.Name);
        }

        /// <summary>The panel's export in the backup directory, read once, without the generated folder.</summary>
        /// <remarks>
        /// For a connection the export is the only evidence there is: an integrated connection — the
        /// kind a class project has — is not in <c>HmiTarget.Connections</c> (measured 2026-10-13; the
        /// Openness help says integrated connections are not exported), but the tags that use it are.
        /// For a graphic it is the only one too: the project's graphics are not reachable through the
        /// API, but the screens that show them are. And a classic screen's number is not readable at all
        /// (see <see cref="HmiScreenNumbers"/>). The generated folder holds what is being checked.
        /// </remarks>
        private static IReadOnlyList<string> PanelExports(string backupDirectory)
        {
            if (!Directory.Exists(backupDirectory))
            {
                return Array.Empty<string>();
            }

            var generated = Path.Combine(backupDirectory, GeneratedDirectoryName);

            return Directory.GetFiles(backupDirectory, "*.xml", SearchOption.AllDirectories)
                .Where(file => !file.StartsWith(generated, StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText)
                .ToList();
        }

        private static HashSet<string> Names(IEnumerable<string> names)
        {
            return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Turns a failed HMI write into the exception to report, closing the session when TIA Portal ended it.</summary>
        /// <param name="ex">What was thrown.</param>
        /// <param name="operation">The method that failed.</param>
        /// <returns>The exception to decorate and throw.</returns>
        private PortalException AsHmiWriteFailure(Exception ex, string operation)
        {
            return ForgetEndedSession(ex, operation)
                ?? ex as PortalException
                ?? new PortalException(PortalErrorCode.WriteFailed, $"{operation} failed: {ex.Message}", null, ex);
        }

        /// <summary>Forgets the project when TIA Portal ended its session, and says so.</summary>
        /// <param name="ex">What was thrown.</param>
        /// <param name="operation">The method that failed.</param>
        /// <returns>The exception to report when the session ended; null when it did not.</returns>
        /// <remarks>
        /// After a <see cref="NonRecoverableException"/> the project object is disposed, and every later
        /// call failed with "Access to a disposed object" until it was opened again (measured
        /// 2026-10-14). Forgetting it makes the next call say plainly that no project is open.
        /// </remarks>
        private PortalException? ForgetEndedSession(Exception ex, string operation)
        {
            if (!(ex is NonRecoverableException || ex is EngineeringObjectDisposedException))
            {
                return null;
            }

            CurrentProject = null;
            _session = null;

            return new PortalException(
                PortalErrorCode.WriteFailed,
                $"{operation} failed and TIA Portal ended the project session: open the project again; what was not saved is lost. Reason: {ex.Message}",
                null,
                ex);
        }
    }
}
