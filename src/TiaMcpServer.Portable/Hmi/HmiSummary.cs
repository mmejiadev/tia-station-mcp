using System.Collections.Generic;

namespace TiaMcpServer.Hmi
{
    /// <summary>What a classic WinCC panel holds, by name.</summary>
    /// <remarks>
    /// Names and not contents, because classic WinCC exposes no object model for what is drawn on a
    /// screen: its buttons and fields exist only in the exported XML. This is the index to that export.
    /// Screens and tag tables in folders are written as <c>Folder/Name</c>.
    /// </remarks>
    public sealed class HmiSummary
    {
        /// <summary>Creates a summary.</summary>
        /// <param name="screens">Screens, by full path.</param>
        /// <param name="templates">Screen templates, by full path.</param>
        /// <param name="tagTables">HMI tag tables, by full path, each with how many tags it holds.</param>
        /// <param name="textLists">Text lists, by name.</param>
        public HmiSummary(
            IReadOnlyList<string> screens,
            IReadOnlyList<string> templates,
            IReadOnlyList<string> tagTables,
            IReadOnlyList<string> textLists)
        {
            Screens = screens;
            Templates = templates;
            TagTables = tagTables;
            TextLists = textLists;
        }

        /// <summary>Screens, by full path.</summary>
        public IReadOnlyList<string> Screens { get; }

        /// <summary>Screen templates, by full path.</summary>
        public IReadOnlyList<string> Templates { get; }

        /// <summary>HMI tag tables, by full path, each followed by how many tags it holds.</summary>
        public IReadOnlyList<string> TagTables { get; }

        /// <summary>Text lists, by name.</summary>
        public IReadOnlyList<string> TextLists { get; }
    }
}
