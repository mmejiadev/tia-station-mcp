using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>What a SIMATIC SD document must be before a library type or a new version is made from it.</summary>
    /// <remarks>
    /// Measured 2026-11-08 on TIA Portal V20: a new type makes its first version in work and an instance in the program,
    /// and a new version moves the program's instance onto it. A name the program holds is refused, with a version in work
    /// a new one is refused and <c>Override</c> replaces its content, and code that does not compile is taken without a word.
    /// </remarks>
    public static class LibraryTypeDocumentRules
    {
        /// <summary>The ending of a SIMATIC SD document.</summary>
        public const string DocumentExtension = ".s7dcl";

        private static readonly string[] DeclarationKeywords = { "FUNCTION", "FUNCTION_BLOCK", "TYPE" };

        /// <summary>Throws unless the name can be a document's in a folder.</summary>
        /// <param name="documentName">The name, without its extension.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a blank name, a path, or a name with the extension.</exception>
        public static void CheckDocumentName(string documentName)
        {
            if (string.IsNullOrWhiteSpace(documentName) || documentName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{documentName}' cannot name a document in the folder; the folder is importPath");
            }

            if (documentName.EndsWith(DocumentExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Give the document's name without '{DocumentExtension}'; got '{documentName}'");
            }
        }

        /// <summary>Reads what a document declares.</summary>
        /// <param name="documentName">The document's name, which must be what it declares: it names the type and its instance.</param>
        /// <param name="text">The document.</param>
        /// <returns>What it makes, and the name it declares.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a document declaring anything else, or another name.</exception>
        public static (LibraryTypeDocumentKind Kind, string Name) Read(string documentName, string text)
        {
            var position = SkipHeader(text ?? string.Empty);
            var keyword = ReadWord(text ?? string.Empty, ref position);
            var kind = KindOf(documentName, keyword);
            var name = ReadName(text!, ref position);

            if (!string.Equals(name, documentName, StringComparison.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The document '{documentName}' declares '{name}'; the two must match, as the name is the type's and its instance's");
            }

            if (text!.Split('\n').Count(IsDeclaration) > 1)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The document '{documentName}' declares more than one thing; a library type is made of one");
            }

            return (kind, name);
        }

        /// <summary>Throws unless neither the program nor the project library holds the name.</summary>
        /// <param name="name">The new type's name.</param>
        /// <param name="inProgram">The program's block and data type names.</param>
        /// <param name="inLibrary">The project library's type names.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when either holds it, case aside.</exception>
        public static void CheckNameFree(string name, IEnumerable<string> inProgram, IEnumerable<string> inLibrary)
        {
            if (inProgram.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The program already holds a block or data type '{name}', and a new type makes an instance of that name; nothing was created");
            }

            if (inLibrary.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The project library already has a type '{name}'; a new version of it is CreateLibraryTypeVersionFromDocument");
            }
        }

        /// <summary>Throws unless a version can be made as asked.</summary>
        /// <param name="typeName">The type.</param>
        /// <param name="versions">Its versions.</param>
        /// <param name="replaceInWork">Whether the version in work is to be replaced rather than a new one made.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when there is a version in work and replacing it was not asked for, or the other way round.</exception>
        public static void CheckVersion(string typeName, IReadOnlyList<LibraryVersionInfo> versions, bool replaceInWork)
        {
            var hasInWork = versions.Any(version => !version.IsReleased);

            if (hasInWork && !replaceInWork)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{typeName}' has a version in work, and TIA Portal holds one at a time. ReleaseLibraryType releases it; or ask to replace what it holds");
            }

            if (!hasInWork && replaceInWork)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{typeName}' has no version in work to replace; ask for a new version instead");
            }
        }

        private static LibraryTypeDocumentKind KindOf(string documentName, string keyword)
        {
            switch (keyword.ToUpperInvariant())
            {
                case "FUNCTION":
                case "FUNCTION_BLOCK":
                    return LibraryTypeDocumentKind.Block;
                case "TYPE":
                    return LibraryTypeDocumentKind.PlcType;
                default:
                    throw new PortalException(PortalErrorCode.InvalidParams, $"The document '{documentName}' declares '{keyword}'; a library type is made of a function, a function block or a PLC data type");
            }
        }

        /// <remarks>A member named like a keyword is followed by a colon; a data type's own name comes on the next line, or after it.</remarks>
        private static bool IsDeclaration(string line)
        {
            var position = SkipWhitespace(line, 0);
            var word = ReadWord(line, ref position);

            return DeclarationKeywords.Contains(word, StringComparer.OrdinalIgnoreCase)
                && !line.Substring(position).TrimStart().StartsWith(":", StringComparison.Ordinal);
        }

        /// <remarks>Past the attribute blocks in braces and the comments that may open a document.</remarks>
        private static int SkipHeader(string text)
        {
            var position = SkipWhitespace(text, 0);
            var previous = -1;

            while (position != previous && position < text.Length)
            {
                previous = position;
                position = SkipWhitespace(text, AfterHeaderItem(text, position));
            }

            return position;
        }

        private static int AfterHeaderItem(string text, int position)
        {
            if (text[position] == '{')
            {
                return AfterAttributes(text, position);
            }

            if (string.CompareOrdinal(text, position, "//", 0, 2) == 0)
            {
                return After(text, position, "\n");
            }

            if (string.CompareOrdinal(text, position, "/*", 0, 2) == 0)
            {
                return After(text, position, "*/");
            }

            return string.CompareOrdinal(text, position, "(*", 0, 2) == 0 ? After(text, position, "*)") : position;
        }

        /// <remarks>A closing brace inside a quoted value does not end the block.</remarks>
        private static int AfterAttributes(string text, int position)
        {
            var isQuoted = false;

            for (var index = position; index < text.Length; index++)
            {
                isQuoted ^= text[index] == '"';

                if (text[index] == '}' && !isQuoted)
                {
                    return index + 1;
                }
            }

            return text.Length;
        }

        private static int After(string text, int position, string closing)
        {
            var end = text.IndexOf(closing, position, StringComparison.Ordinal);

            return end < 0 ? text.Length : end + closing.Length;
        }

        private static int SkipWhitespace(string text, int position)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            return position;
        }

        private static string ReadWord(string text, ref int position)
        {
            var start = position;
            while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_'))
            {
                position++;
            }

            return text.Substring(start, position - start);
        }

        private static string ReadName(string text, ref int position)
        {
            position = SkipWhitespace(text, position);
            if (position >= text.Length || text[position] != '"')
            {
                return ReadWord(text, ref position);
            }

            var end = text.IndexOf('"', position + 1);

            return end < 0 ? string.Empty : text.Substring(position + 1, end - position - 1);
        }
    }
}
