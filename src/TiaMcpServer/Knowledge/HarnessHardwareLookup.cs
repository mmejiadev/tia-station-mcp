using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace TiaMcpServer.Knowledge
{
    /// <summary>
    /// Reaches the documentation index by running the harness lookup this repository already ships.
    /// </summary>
    /// <remarks>
    /// **The ranking lives in one place, and it is not here.** BM25 and the lexical vector are
    /// implemented once, in <c>harness/src/knowledge/</c>, and this class runs that program and
    /// reads its JSON. A second implementation in C# would have to agree with the first, and two
    /// rankings that must agree are two rankings that eventually will not.
    ///
    /// The cost is that Node has to be on the machine. It is an *optional* dependency: when it is
    /// missing, or the index has not been built, or the answer cannot be read, the result is
    /// <see cref="HardwareContextOutcome.Unavailable"/> carrying the reason, and the change proceeds
    /// without citations. Nothing in this class can stop a write.
    /// </remarks>
    public sealed class HarnessHardwareLookup : IHardwareLookup
    {
        /// <summary>How many excerpts a plan carries. More than a reader will read is fewer read.</summary>
        private const int Excerpts = 3;

        /// <summary>The exit code the lookup uses for an honest silence, as opposed to a failure.</summary>
        private const int NotFoundExitCode = 2;

        private readonly string _scriptPath;
        private readonly string _indexPath;
        private readonly TimeSpan _timeout;

        /// <summary>Creates a lookup against a harness checkout.</summary>
        /// <param name="scriptPath">Full path to <c>hardwareLookup.ts</c>.</param>
        /// <param name="indexPath">Full path to the index the harness built.</param>
        /// <param name="timeout">How long to wait before giving up on an answer.</param>
        /// <exception cref="ArgumentException">A path is empty, or the timeout is not positive.</exception>
        public HarnessHardwareLookup(string scriptPath, string indexPath, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                throw new ArgumentException("The lookup needs the script that answers it", nameof(scriptPath));
            }

            if (string.IsNullOrWhiteSpace(indexPath))
            {
                throw new ArgumentException("The lookup needs the index to search", nameof(indexPath));
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentException($"A lookup cannot wait {timeout}", nameof(timeout));
            }

            _scriptPath = scriptPath;
            _indexPath = indexPath;
            _timeout = timeout;
        }

        /// <summary>Asks the index, and never lets its failure become the caller's.</summary>
        /// <param name="question">What to ask.</param>
        /// <returns>Excerpts, an honest not-found, or an unavailable carrying the reason.</returns>
        /// <remarks>
        /// The exception types caught here are named rather than caught as <c>Exception</c>: these
        /// are the ways starting a process and reading its answer are known to fail, and anything
        /// else is a defect that should surface instead of being turned into a shrug. Every one of
        /// them still produces a *visible* result — the reason reaches the plan and the audit trail,
        /// so a broken lookup is reported, never swallowed.
        /// </remarks>
        public HardwareContext Describe(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return HardwareContext.Unavailable("the change carried nothing to ask about");
            }

            if (!File.Exists(_indexPath))
            {
                return HardwareContext.Unavailable($"no documentation index at {_indexPath}");
            }

            try
            {
                return Ask(question);
            }
            catch (Win32Exception failure)
            {
                return HardwareContext.Unavailable($"the lookup could not be started: {failure.Message}");
            }
            catch (IOException failure)
            {
                return HardwareContext.Unavailable($"the lookup could not be read: {failure.Message}");
            }
            catch (JsonException failure)
            {
                return HardwareContext.Unavailable($"the lookup answered something unreadable: {failure.Message}");
            }
            catch (InvalidOperationException failure)
            {
                return HardwareContext.Unavailable($"the lookup did not run: {failure.Message}");
            }
        }

        /// <summary>Runs the lookup and reads its answer.</summary>
        /// <param name="question">What to ask.</param>
        /// <returns>The context its answer describes.</returns>
        /// <remarks>
        /// Standard error is redirected and drained rather than ignored. A child process whose
        /// error pipe fills up blocks writing to it, forever, while the parent waits for the output
        /// pipe — a deadlock that appears only once the lookup has enough to say, which is to say
        /// only when something is already wrong.
        /// </remarks>
        private HardwareContext Ask(string question)
        {
            using (var lookup = Process.Start(StartInfo(question)))
            {
                if (lookup == null)
                {
                    return HardwareContext.Unavailable("the lookup produced no process");
                }

                lookup.ErrorDataReceived += (sender, line) => { };
                lookup.BeginErrorReadLine();

                var answer = lookup.StandardOutput.ReadToEnd();

                if (!lookup.WaitForExit((int)_timeout.TotalMilliseconds))
                {
                    lookup.Kill();

                    return HardwareContext.Unavailable($"the lookup did not answer within {_timeout.TotalSeconds:0} s");
                }

                return Read(answer, lookup.ExitCode);
            }
        }

        private ProcessStartInfo StartInfo(string question)
        {
            var arguments = new[]
            {
                _scriptPath,
                "--query", question,
                "--index", _indexPath,
                "--limit", Excerpts.ToString(CultureInfo.InvariantCulture),
                "--format", "json"
            };

            return new ProcessStartInfo("node", CommandLine.Join(arguments))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        /// <summary>Turns the lookup's answer into a context, trusting its exit code over its text.</summary>
        /// <param name="answer">What it printed on standard output.</param>
        /// <param name="exitCode">What it exited with.</param>
        /// <returns>The context.</returns>
        /// <remarks>
        /// A not-found is a correct answer with its own exit code, and it is read as one rather than
        /// as an error. Any other non-zero code is a failure of the lookup itself and says so,
        /// instead of being reported as documented silence — the distinction that
        /// <see cref="HardwareContextOutcome"/> exists to keep.
        /// </remarks>
        private static HardwareContext Read(string answer, int exitCode)
        {
            if (exitCode == NotFoundExitCode)
            {
                return HardwareContext.NotFound();
            }

            if (exitCode != 0)
            {
                return HardwareContext.Unavailable($"the lookup failed with exit code {exitCode}");
            }

            var citations = Parse(answer);

            return citations.Count == 0 ? HardwareContext.NotFound() : HardwareContext.Cited(citations);
        }

        private static List<HardwareCitation> Parse(string answer)
        {
            using (var document = JsonDocument.Parse(answer))
            {
                var found = new List<HardwareCitation>();

                if (!document.RootElement.TryGetProperty("citations", out var citations))
                {
                    return found;
                }

                foreach (var citation in citations.EnumerateArray())
                {
                    found.Add(ReadCitation(citation));
                }

                return found;
            }
        }

        private static HardwareCitation ReadCitation(JsonElement citation)
        {
            var document = new SourceDocument(
                Text(citation, "device"),
                Text(citation, "title"),
                Text(citation, "version"));

            return new HardwareCitation(document, Page(citation), Text(citation, "excerpt"));
        }

        private static string Text(JsonElement citation, string field)
        {
            if (!citation.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"A citation has no '{field}'");
            }

            return value.GetString() ?? string.Empty;
        }

        private static int Page(JsonElement citation)
        {
            if (!citation.TryGetProperty("page", out var value) || !value.TryGetInt32(out var page))
            {
                throw new JsonException("A citation has no readable page number");
            }

            return page;
        }
    }
}
