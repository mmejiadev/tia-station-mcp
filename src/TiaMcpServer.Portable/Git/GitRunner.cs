using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Git
{
    /// <summary>
    /// Runs one Git command in one folder, and nothing else.
    /// </summary>
    /// <remarks>
    /// The folder is the process's working directory, never an argument, and every argument is one this server built
    /// from a fixed word or a value <see cref="GitInput"/> passed: none carries a space or a quote, so the command line
    /// cannot be split into another one; no shell is involved either. Git is told never to prompt — a server has nobody to answer, and a prompt
    /// would hang the call — and is stopped after a timeout. Git itself is found on the PATH, as installed.
    /// </remarks>
    public sealed class GitRunner
    {
        /// <remarks>
        /// Put before every command, and inherited by every Git that one starts (a submodule's included): no file
        /// system monitor program, no hooks — <c>NUL</c> is a folder holding none, measured on Git for Windows 2.55 with a
        /// failing pre-commit hook that then did not run — and no recursion into submodules. A repository's own settings
        /// are checked by <see cref="GitRepositorySafety"/> as well; these hold even where a check misses something.
        /// </remarks>
        private const string ProtectiveSettings = "-c core.fsmonitor=false -c core.hooksPath=NUL -c submodule.recurse=false";

        private static readonly Regex SafeArgument = new Regex(@"^[A-Za-z0-9._/:=@%^~+()-]+$", RegexOptions.CultureInvariant);

        private readonly TimeSpan _timeout;

        /// <summary>Creates a runner.</summary>
        /// <param name="timeout">How long a command may take before it is stopped.</param>
        public GitRunner(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        /// <summary>Runs Git with these arguments in this folder.</summary>
        /// <param name="workingDirectory">The repository's folder, a full path that exists.</param>
        /// <param name="arguments">The arguments, each a single token without spaces or quotes; a file path is passed separately.</param>
        /// <param name="filePath">A file Git is given as the last argument, quoted, e.g. a message for <c>-F</c>; or null.</param>
        /// <returns>What Git answered.</returns>
        /// <exception cref="PortalException">
        /// An argument that is not a single safe token, a folder that is not there, Git not installed, or the timeout.
        /// </exception>
        public GitResult Run(string workingDirectory, IReadOnlyList<string> arguments, string? filePath = null)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            RequireFolder(workingDirectory);
            var commandLine = BuildCommandLine(arguments, filePath);

            using var process = new Process { StartInfo = StartInfo(workingDirectory, commandLine) };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Git is not installed or not on the PATH; install Git for Windows", null, ex);
            }

            // Its own input, closed at once: with the stdio transport the server's input is the MCP channel, and a Git, ssh
            // or helper reading it would take the client's next message.
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)_timeout.TotalMilliseconds))
            {
                process.Kill();
                throw new PortalException(PortalErrorCode.ExportFailed, $"git {arguments[0]} did not finish within {_timeout.TotalSeconds:0} s and was stopped");
            }

            Task.WaitAll(output, error);

            return new GitResult(process.ExitCode, output.Result, error.Result);
        }

        private static void RequireFolder(string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory) || !Path.IsPathRooted(workingDirectory) || !Directory.Exists(workingDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{workingDirectory}' is not a folder Git can work in");
            }
        }

        private static string BuildCommandLine(IReadOnlyList<string> arguments, string? filePath)
        {
            var unsafeArgument = arguments.FirstOrDefault(argument => argument == null || !SafeArgument.IsMatch(argument));

            if (arguments.Count == 0 || unsafeArgument != null || arguments[0].StartsWith("-", StringComparison.Ordinal))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Refusing to pass '{unsafeArgument}' to Git: arguments are single tokens this server builds");
            }

            var command = ProtectiveSettings + " " + string.Join(" ", arguments);

            if (filePath == null)
            {
                return command;
            }

            if (filePath.IndexOf('"') >= 0 || !Path.IsPathRooted(filePath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "A file passed to Git is a full path without quotes");
            }

            // Quoted, and a trailing backslash doubled so it does not escape the closing quote.
            return command + " \"" + (filePath.EndsWith("\\", StringComparison.Ordinal) ? filePath + "\\" : filePath) + "\"";
        }

        private static ProcessStartInfo StartInfo(string workingDirectory, string commandLine)
        {
            var info = new ProcessStartInfo("git", commandLine)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Never wait for a person: no terminal prompt, no credential window.
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            info.EnvironmentVariables["GCM_INTERACTIVE"] = "never";
            info.EnvironmentVariables["GIT_ASKPASS"] = string.Empty;

            return info;
        }
    }
}
