using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Writing one block or type to a SimaticML file inside a bulk export, shared by ExportBlocks and
    /// ExportTypes. Each held its own copy of this loop body, over a hundred lines with four catch
    /// blocks, and both sent every failure only to the log.
    /// </remarks>
    public partial class Portal
    {
        private static string XmlExportPath(string exportPath, string groupPath, string name)
        {
            return Path.Combine(exportPath, groupPath.Replace('/', Path.DirectorySeparatorChar), $"{name}.xml");
        }

        /// <remarks>
        /// One item failing must not stop the others, so each failure is recorded rather than thrown;
        /// the log keeps the exception. A stale file is removed first so a failed export cannot leave
        /// the old one behind looking current.
        /// </remarks>
        private bool TryExportXml(string name, string path, Action<FileInfo> export, List<string> failures)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Delete(path);
                export(new FileInfo(path));
                return true;
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: {ex.Message}");
                _logger?.LogError(ex, "Exporting {Name} to {Path} failed", name, path);
                return false;
            }
        }

        private void LogBulkExport(string operation, int exportedCount, IReadOnlyList<string> failures)
        {
            if (failures.Count > 0)
            {
                _logger?.LogWarning("{Operation} exported {Count}; {Failures} failed. First failure: {First}", operation, exportedCount, failures.Count, failures[0]);
                return;
            }

            _logger?.LogInformation("{Operation} exported {Count}.", operation, exportedCount);
        }
    }
}
