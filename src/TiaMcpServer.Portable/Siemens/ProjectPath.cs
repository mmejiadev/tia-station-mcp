using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A path in the project structure, read once and understood the same way everywhere.
    /// </summary>
    /// <remarks>
    /// Paths are how this server addresses everything: <c>Group/Subgroup/Name</c>, never a bare
    /// name, because a bare name is ambiguous. That rule is in CLAUDE.md; what was not anywhere was
    /// an agreed reading of it, and the code had grown three.
    ///
    /// <list type="bullet">
    /// <item><c>Split('/')</c>, keeping empty segments, in the software lookup</item>
    /// <item><c>Split('/', RemoveEmptyEntries)</c>, dropping them, in the device and group lookups</item>
    /// <item><c>Contains("/")</c> with <c>LastIndexOf</c> and two <c>Substring</c> calls, in the
    /// block, type and document lookups</item>
    /// </list>
    ///
    /// So <c>Device//PLC</c> was "not found" from one tool and the same as <c>Device/PLC</c> from
    /// another, and none of it was tested, because reaching any of it meant starting TIA Portal.
    /// That is audit finding F5 in one file: the logic that decides which block gets overwritten
    /// living where it cannot be checked.
    ///
    /// The reading below is the forgiving one, deliberately. A doubled or trailing slash is a typing
    /// artefact and the caller means the obvious thing; being strict there turns a copy-paste into
    /// "no such block", which sends them looking at the project instead of at the string. An empty
    /// path is the one thing refused, because that is not a typo, it is a missing argument.
    /// </remarks>
    public sealed class ProjectPath
    {
        private const char Separator = '/';

        private ProjectPath(IReadOnlyList<string> segments)
        {
            Segments = segments;
        }

        /// <summary>Reads the path of one object: a block, a type, a device or a PLC software.</summary>
        /// <param name="path">The path as the caller wrote it.</param>
        /// <returns>The path, with empty segments dropped and each name trimmed.</returns>
        /// <exception cref="PortalException">The path names nothing.</exception>
        public static ProjectPath Parse(string path)
        {
            var segments = SegmentsOf(path);

            if (segments.Count == 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "A path is required, and this one is empty. Paths are 'Group/Subgroup/Name'; " +
                    "a bare name is allowed only for something that sits at the top level.");
            }

            return new ProjectPath(segments);
        }

        /// <summary>Reads the path of a group, where naming nothing means the root.</summary>
        /// <param name="groupPath">The path as the caller wrote it, or empty for the root.</param>
        /// <returns>One name per level below the root. Empty when the root itself is meant.</returns>
        /// <remarks>
        /// Separate from <see cref="Parse"/> because the two really are different questions. Every
        /// program has a root block group and a root type group, and addressing them is what an
        /// empty group path means — while an empty path to a *block* names no block at all.
        /// </remarks>
        public static IReadOnlyList<string> GroupSegments(string groupPath)
        {
            return SegmentsOf(groupPath);
        }

        /// <summary>Builds a path from a parent path and a name.</summary>
        /// <param name="parent">The parent path, or empty when the name sits at the top level.</param>
        /// <param name="name">The name to append.</param>
        /// <returns>The joined path.</returns>
        public static string Join(string parent, string name)
        {
            return string.IsNullOrEmpty(parent) ? name : $"{parent}{Separator}{name}";
        }

        /// <summary>Each name along the path, outermost first.</summary>
        public IReadOnlyList<string> Segments { get; }

        /// <summary>The name of the object itself: the last segment.</summary>
        public string Name => Segments[Segments.Count - 1];

        /// <summary>The path of what holds it, or empty when it sits at the top level.</summary>
        public string Parent => string.Join(Separator.ToString(), Segments.Take(Segments.Count - 1));

        /// <summary>Whether the object sits at the top level, with nothing above it.</summary>
        public bool IsTopLevel => Segments.Count == 1;

        /// <summary>The path as this class reads it, which is what any message should quote.</summary>
        /// <returns>The canonical path.</returns>
        public override string ToString()
        {
            return string.Join(Separator.ToString(), Segments);
        }

        private static IReadOnlyList<string> SegmentsOf(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Array.Empty<string>();
            }

            return path
                .Split(Separator)
                .Select(segment => segment.Trim())
                .Where(segment => segment.Length > 0)
                .ToList();
        }
    }
}
