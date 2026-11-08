using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>What a master copy tool accepts: the kind of a source, folder and master copy paths, and what one may be created from.</summary>
    /// <remarks>
    /// Measured on TIA Portal V20 (2026-11-01): <c>CreateFrom</c> with its default mode renames without a word
    /// (<c>Main_1</c>, <c>Main_2</c>), so an object already there is refused before anything is created, and
    /// <c>ThrowIfExists</c> is asked for besides. What a master copy holds is read from its content descriptions,
    /// so a caller never says what to create it as.
    /// </remarks>
    public static class MasterCopyRules
    {
        private const char Separator = '/';

        // TIA Portal V20's spelling of a name it renamed on creating from a master copy: MC_Running became MC_Running(1).
        private static readonly Regex RenamedName = new Regex(@"^(?<original>.+)\((?<number>\d+)\)$", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, MasterCopySourceKind> KindsByName = new Dictionary<string, MasterCopySourceKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["block"] = MasterCopySourceKind.Block,
            ["tagTable"] = MasterCopySourceKind.TagTable,
            ["plcType"] = MasterCopySourceKind.PlcType
        };

        /// <summary>Reads the kind of a source as a tool is given it.</summary>
        /// <param name="kind"><c>block</c>, <c>tagTable</c> or <c>plcType</c>, case aside.</param>
        /// <returns>The kind.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for anything else, naming what is accepted.</exception>
        public static MasterCopySourceKind ParseKind(string kind)
        {
            return KindsByName.TryGetValue((kind ?? string.Empty).Trim(), out var parsed)
                ? parsed
                : throw new PortalException(PortalErrorCode.InvalidParams, $"kind must be one of {string.Join(", ", KindsByName.Keys)}; got '{kind}'");
        }

        /// <summary>The name a tool uses for a kind, the one <see cref="ParseKind"/> reads.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns><c>block</c>, <c>tagTable</c> or <c>plcType</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> for a kind added later and not named here.</exception>
        public static string Name(MasterCopySourceKind kind)
        {
            return kind switch
            {
                MasterCopySourceKind.Block => "block",
                MasterCopySourceKind.TagTable => "tagTable",
                MasterCopySourceKind.PlcType => "plcType",
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised master copy source kind: {kind}")
            };
        }

        /// <summary>Splits a master copy folder path into its folder names.</summary>
        /// <param name="folderPath">e.g. <c>Motors/Pumps</c>; empty for the library's top folder.</param>
        /// <returns>The names, outermost first; none for the top folder.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for an empty name between separators, or <c>.</c> or <c>..</c>.</exception>
        public static IReadOnlyList<string> FolderSegments(string folderPath)
        {
            var trimmed = (folderPath ?? string.Empty).Trim().Trim(Separator);
            if (trimmed.Length == 0)
            {
                return Array.Empty<string>();
            }

            var segments = trimmed.Split(Separator).Select(segment => segment.Trim()).ToList();
            if (segments.Any(segment => segment.Length == 0 || segment == "." || segment == ".."))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{folderPath}' is not a master copy folder path: give folder names separated by '/', e.g. 'Motors/Pumps'");
            }

            return segments;
        }

        /// <summary>Splits a master copy path into its folder and its name.</summary>
        /// <param name="masterCopyPath">e.g. <c>Motors/FB_Motor</c>, or <c>FB_Motor</c> in the top folder.</param>
        /// <returns>The folder's names, outermost first, and the master copy's name.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the path is empty or not one.</exception>
        public static (IReadOnlyList<string> Folder, string Name) ParseMasterCopyPath(string masterCopyPath)
        {
            var segments = FolderSegments(masterCopyPath);
            if (segments.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "masterCopyPath is required, e.g. 'Motors/FB_Motor', as GetMasterCopies gives it");
            }

            return (segments.Take(segments.Count - 1).ToList(), segments[segments.Count - 1]);
        }

        /// <summary>Joins folder names and a name back into the path a tool is given.</summary>
        /// <param name="folder">The folder's names, outermost first.</param>
        /// <param name="name">The last name, or empty for the folder itself.</param>
        /// <returns>e.g. <c>Motors/FB_Motor</c>.</returns>
        public static string Join(IEnumerable<string> folder, string name)
        {
            return string.Join(Separator.ToString(), (folder ?? Enumerable.Empty<string>()).Concat(string.IsNullOrEmpty(name) ? Enumerable.Empty<string>() : new[] { name }));
        }

        /// <summary>A master copy folder, as a message names it.</summary>
        /// <param name="names">The folder's names, outermost first; none for the top folder.</param>
        /// <returns>e.g. <c>The master copy folder 'Motors'</c>.</returns>
        public static string FolderPlace(IReadOnlyList<string> names)
        {
            return names == null || names.Count == 0 ? "The project library's top master copy folder" : $"The master copy folder '{Join(names, string.Empty)}'";
        }

        /// <summary>Names as a message lists them.</summary>
        /// <param name="names">The names.</param>
        /// <returns>They, separated by commas, or <c>nothing</c>.</returns>
        public static string List(IEnumerable<string> names)
        {
            var list = (names ?? Enumerable.Empty<string>()).ToList();

            return list.Count == 0 ? "nothing" : string.Join(", ", list);
        }

        /// <summary>Throws when a place already holds an object of the name about to be created there.</summary>
        /// <param name="name">The name.</param>
        /// <param name="held">Every name already there.</param>
        /// <param name="place">The place, as the refusal names it, e.g. <c>The program 'PLC_1'</c>.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when the name is there, case aside, as TIA Portal compares them.</exception>
        public static void CheckNotPresent(string name, IEnumerable<string> held, string place)
        {
            if ((held ?? Enumerable.Empty<string>()).Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"{place} already holds '{name}'; nothing was created. TIA Portal would otherwise rename the new one without a word.");
            }
        }

        /// <summary>The names TIA Portal renamed on creating from a master copy: each one the program held before, with a number in brackets.</summary>
        /// <param name="created">The names of what was just created.</param>
        /// <param name="before">Every name the program held before, compared case aside.</param>
        /// <returns>Each renamed name with the name it was renamed from; none when nothing was renamed.</returns>
        /// <remarks>
        /// Measured 2026-11-08 on TIA Portal V20: a tag table's tags are not named in its master copy's content description, and
        /// a tag the program already holds is created as <c>MC_Running(1)</c> without a word. A table bringing a tag literally
        /// named like that is refused too: failing closed on an unlikely name is cheaper than a silent rename.
        /// </remarks>
        public static IReadOnlyList<(string Renamed, string Original)> RenamedOnCreate(IEnumerable<string> created, IEnumerable<string> before)
        {
            var held = new HashSet<string>(before ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            return (created ?? Enumerable.Empty<string>())
                .Select(name => (Renamed: name, Match: RenamedName.Match(name)))
                .Where(entry => entry.Match.Success && held.Contains(entry.Match.Groups["original"].Value))
                .Select(entry => (entry.Renamed, entry.Match.Groups["original"].Value))
                .ToList();
        }

        /// <summary>The one kind a master copy can be created as, from what it holds.</summary>
        /// <param name="masterCopyName">The master copy, as the refusal names it.</param>
        /// <param name="contents">Each item it holds: its type's name, and its kind, or null for one these tools do not create.</param>
        /// <returns>The kind of its one item.</returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> when it holds nothing, more than one item, or an item of another kind:
        /// creating part of what it holds would leave the rest behind without a word.
        /// </exception>
        public static MasterCopySourceKind RequireSingleKind(string masterCopyName, IReadOnlyList<(string TypeName, MasterCopySourceKind? Kind)> contents)
        {
            var items = contents ?? Array.Empty<(string TypeName, MasterCopySourceKind? Kind)>();
            if (items.Count != 1)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The master copy '{masterCopyName}' holds {items.Count} item(s) ({string.Join(", ", items.Select(item => item.TypeName))}); this tool creates from a master copy holding exactly one block, tag table or PLC data type");
            }

            return items[0].Kind ?? throw new PortalException(PortalErrorCode.InvalidState, $"The master copy '{masterCopyName}' holds a {items[0].TypeName}; this tool creates a block, a tag table or a PLC data type from one");
        }
    }
}
