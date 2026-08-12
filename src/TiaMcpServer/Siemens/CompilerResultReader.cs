using Siemens.Engineering.Compiler;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns the nested <see cref="CompilerResult"/> TIA Portal returns into a flat
    /// <see cref="CompilationReport"/>.
    /// </summary>
    /// <remarks>
    /// The messages arrive as a tree: a device holds messages for its software, which hold
    /// messages for each block, which hold the actual errors. Only the leaves carry a useful
    /// description, and only the branches carry the path, so a flat list built from either alone
    /// is useless. Walking the tree and joining the parent names is what makes an error say both
    /// what is wrong and where.
    /// </remarks>
    internal static class CompilerResultReader
    {
        private const string PathSeparator = "/";

        /// <summary>Reads a compiler result into a report.</summary>
        /// <param name="result">The result TIA Portal returned, or null if it returned nothing.</param>
        /// <returns>The flattened report.</returns>
        internal static CompilationReport Read(CompilerResult? result)
        {
            if (result == null)
            {
                return new CompilationReport(
                    CompilationSeverity.Error,
                    errorCount: 1,
                    warningCount: 0,
                    new[] { new CompilationMessage(CompilationSeverity.Error, string.Empty, "TIA Portal returned no compiler result") });
            }

            var messages = new List<CompilationMessage>();
            Collect(result.Messages, string.Empty, messages);

            return new CompilationReport(ToSeverity(result.State), result.ErrorCount, result.WarningCount, messages);
        }

        private static void Collect(CompilerResultMessageComposition source, string parentPath, List<CompilationMessage> messages)
        {
            foreach (var message in source)
            {
                var path = Combine(parentPath, message.Path);

                messages.Add(new CompilationMessage(ToSeverity(message.State), path, message.Description));

                Collect(message.Messages, path, messages);
            }
        }

        private static string Combine(string parentPath, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return parentPath;
            }

            if (string.IsNullOrEmpty(parentPath))
            {
                return path;
            }

            return parentPath + PathSeparator + path;
        }

        private static CompilationSeverity ToSeverity(CompilerResultState state)
        {
            switch (state)
            {
                case CompilerResultState.Error:
                    return CompilationSeverity.Error;

                case CompilerResultState.Warning:
                    return CompilationSeverity.Warning;

                case CompilerResultState.Information:
                    return CompilationSeverity.Information;

                default:
                    return CompilationSeverity.Success;
            }
        }
    }
}
