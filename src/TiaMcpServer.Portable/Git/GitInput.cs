using System;
using System.Linq;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Git
{
    /// <summary>
    /// Checks what a caller passes to Git before Git sees it: branch names and commit messages.
    /// </summary>
    /// <remarks>
    /// Branch names go on Git's command line, so they are held to a strict subset of what <c>git check-ref-format</c>
    /// allows: letters, digits, <c>.</c>, <c>_</c>, <c>-</c> and <c>/</c>, no leading dash (which Git would read as an
    /// option), no <c>..</c>, no <c>.lock</c> ending, no empty segment. A name that passes needs no quoting, so nothing
    /// a caller writes can become another argument or another command. A commit message never reaches the command
    /// line at all: it is written to a file Git reads with <c>-F</c>.
    /// </remarks>
    public static class GitInput
    {
        /// <summary>The longest commit message taken.</summary>
        public const int MaximumMessageLength = 5000;

        private const int MaximumBranchLength = 100;
        private static readonly Regex FullPath = new Regex(@"^[A-Za-z]:[\\/]", RegexOptions.CultureInvariant);
        private static readonly Regex BranchCharacters = new Regex(@"^[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant);

        /// <summary>Refuses a branch name that is not safe to pass to Git.</summary>
        /// <param name="branch">The name.</param>
        /// <returns>The name, trimmed.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> with what is wrong.</exception>
        public static string RequireBranch(string branch)
        {
            var name = (branch ?? string.Empty).Trim();
            var problem = BranchProblem(name);

            return problem == null
                ? name
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' is not a branch name this server passes to Git: {problem}");
        }

        /// <summary>Refuses an empty or oversized commit message.</summary>
        /// <param name="message">The message.</param>
        /// <returns>The message, trimmed.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> with what is wrong.</exception>
        public static string RequireMessage(string message)
        {
            var text = (message ?? string.Empty).Trim();

            if (text.Length == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "A commit needs a message saying what changed");
            }

            if (text.Length > MaximumMessageLength || text.Contains('\0'))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"A commit message is plain text of at most {MaximumMessageLength} characters");
            }

            return text;
        }

        /// <summary>Refuses a workspace folder Git must not work in: not on a drive of this machine, or a whole drive.</summary>
        /// <param name="folder">The folder a VCI workspace of the open project names.</param>
        /// <returns>The folder, made canonical.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming the folder.</exception>
        /// <remarks>
        /// The folder is read from the project, which is untrusted data: a project can name a network share, where its
        /// author can change the repository between this server's checks and Git's run and collect the person's
        /// credentials, or a drive's root, which <c>add --all</c> would sweep whole.
        /// </remarks>
        public static string RequireWorkspaceFolder(string folder)
        {
            var text = (folder ?? string.Empty).Trim();

            if (!FullPath.IsMatch(text))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The workspace's folder '{text}' is not on a drive of this machine; Git is run only in a local folder");
            }

            var full = System.IO.Path.GetFullPath(text);

            return System.IO.Path.GetPathRoot(full)!.Length >= full.TrimEnd('\\', '/').Length + 1
                ? throw new PortalException(PortalErrorCode.InvalidState, $"The workspace's folder is the whole drive '{full}'; Git is run only in a folder of its own")
                : full;
        }

        /// <summary>Refuses a path that is not a full one on a drive of this machine.</summary>
        /// <param name="path">The path.</param>
        /// <param name="parameterName">The parameter it came in, to name in the refusal.</param>
        /// <returns>The path, made canonical.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is relative or a network share.</exception>
        /// <remarks>
        /// A relative path would be taken against the server's own working folder — an installation folder, or
        /// System32 — and <c>C:folder</c> or <c>\folder</c> against whatever folder that drive last had. A network share
        /// (<c>\\host\share</c>) is refused too: writing patches there sends the repository's history off the machine, and
        /// reaching the host hands it the person's Windows credentials.
        /// </remarks>
        public static string RequireFullPath(string path, string parameterName)
        {
            var text = (path ?? string.Empty).Trim();

            return FullPath.IsMatch(text)
                ? System.IO.Path.GetFullPath(text)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{parameterName} must be a full path on a drive of this machine, such as C:\\Repos\\Cell\\patches: '{text}' is not one");
        }

        private static string? BranchProblem(string name)
        {
            if (name.Length == 0 || name.Length > MaximumBranchLength)
            {
                return $"give 1 to {MaximumBranchLength} characters";
            }

            if (!BranchCharacters.IsMatch(name))
            {
                return "use letters, digits, '.', '_', '-' and '/' only";
            }

            if (name.StartsWith("-", StringComparison.Ordinal) || name.StartsWith("/", StringComparison.Ordinal) || name.EndsWith("/", StringComparison.Ordinal))
            {
                return "it may not start with '-' or '/', nor end with '/'";
            }

            if (name.Contains("..") || name.Contains("//") || name.EndsWith(".lock", StringComparison.Ordinal) || name.EndsWith(".", StringComparison.Ordinal))
            {
                return "it may not hold '..' or '//', nor end with '.lock' or '.'";
            }

            return name.Split('/').Any(segment => segment.StartsWith(".", StringComparison.Ordinal)) ? "no part of it may start with '.'" : null;
        }
    }
}
