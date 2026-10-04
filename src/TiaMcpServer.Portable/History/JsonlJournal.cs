using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.History
{
    /// <summary>
    /// A journal written as JSON Lines: one object per line, appended and never rewritten.
    /// </summary>
    /// <typeparam name="TRecord">The kind of fact recorded.</typeparam>
    /// <remarks>
    /// The same discipline as <see cref="Governance.JsonlAuditTrail"/> — opened for append, UTF-8
    /// without a byte order mark, appends serialised by a lock because a compile started as a job
    /// runs on the thread pool — and without its hash chain. The chain is what makes the audit
    /// trail evidence of decisions; a journal of compiler output records facts anybody can
    /// reproduce by compiling again, and does not need to be.
    ///
    /// Names in camel case and enumerations as words, so that the web platform's importer reads a
    /// severity as "Error" rather than as 3, and keeps reading it when a value is added.
    /// </remarks>
    public sealed class JsonlJournal<TRecord> : IJournal<TRecord>
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly object _gate = new object();
        private readonly string _path;

        /// <summary>Creates a journal backed by a file.</summary>
        /// <param name="path">Where to write. Its directory is created if missing.</param>
        /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
        public JsonlJournal(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A journal needs a path", nameof(path));
            }

            _path = path;
        }

        /// <inheritdoc />
        public void Append(TRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            try
            {
                var line = JsonSerializer.Serialize(record, Options);

                lock (_gate)
                {
                    EnsureDirectory();
                    File.AppendAllText(_path, line + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (Exception exception)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The journal at '{_path}' could not be written: {exception.Message}",
                    null,
                    exception);
            }
        }

        private void EnsureDirectory()
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(_path));

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
