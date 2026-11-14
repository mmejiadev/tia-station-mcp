using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Git
{
    /// <summary>
    /// The Git operations this server runs in a VCI workspace's folder: status, history, branches, commit, pull, and
    /// changes exported to patch files and imported from them.
    /// </summary>
    /// <remarks>
    /// Chosen to lose nothing. Switching branch, pulling and importing refuse while the folder has uncommitted changes,
    /// which Git would otherwise carry over or stop half-way on. A pull only fast-forwards: when the two sides diverged
    /// it stops and says so instead of making a merge nobody asked for. An import is checked with <c>git apply
    /// --check</c> first, and an <c>am</c> that fails is aborted, so the folder is left as it was. There is no push.
    /// Commits carry the author Git is configured with on this machine, the person's.
    /// </remarks>
    public sealed class GitRepository
    {
        private const int MaximumLogEntries = 200;
        private static readonly Regex CommitHash = new Regex(@"^[0-9a-fA-F]{7,40}$", RegexOptions.CultureInvariant);

        private readonly string _root;
        private readonly GitRunner _git;

        /// <summary>Creates the operations for one folder.</summary>
        /// <param name="root">The workspace's folder, a full path.</param>
        /// <param name="git">The runner.</param>
        public GitRepository(string root, GitRunner git)
        {
            _root = root;
            _git = git ?? throw new ArgumentNullException(nameof(git));
        }

        /// <summary>Makes the folder a repository whose first branch is <c>main</c>.</summary>
        /// <returns>What Git said.</returns>
        /// <exception cref="PortalException">It is a repository already, or Git failed.</exception>
        public string Init()
        {
            GitRepositorySafety.RequireNoLinkedRepository(_root);

            if (Directory.Exists(Path.Combine(_root, ".git")))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{_root}' is a Git repository already; nothing was changed");
            }

            return Require(new[] { "init", "--initial-branch=main" }).Trim();
        }

        /// <summary>The current branch and every changed file, as <c>git status --porcelain</c> lists them.</summary>
        /// <returns>The branch line first, then one line per changed file.</returns>
        public IReadOnlyList<string> Status()
        {
            RequireRepository();

            return Lines(Require(new[] { "status", "--porcelain=v1", "--branch", "--untracked-files=all" }));
        }

        /// <summary>The latest commits of the current branch.</summary>
        /// <param name="count">How many, 1 to 200.</param>
        /// <returns>One line per commit: hash, date, author, message.</returns>
        public IReadOnlyList<string> Log(int count)
        {
            RequireRepository();
            var entries = Math.Max(1, Math.Min(MaximumLogEntries, count));

            var log = _git.Run(_root, new[] { "log", "-n", entries.ToString(System.Globalization.CultureInfo.InvariantCulture), "--date=short", "--pretty=format:%h%x09%ad%x09%an%x09%s" });

            // A repository with no commit yet answers with an error; it has no history, which is not a failure.
            return log.Succeeded ? Lines(log.Output) : Array.Empty<string>();
        }

        /// <summary>The local branches, the current one marked with <c>*</c>.</summary>
        /// <returns>One line per branch.</returns>
        public IReadOnlyList<string> Branches()
        {
            RequireRepository();

            return Lines(Require(new[] { "branch", "--list", "--format=%(HEAD)%(refname:short)" }));
        }

        /// <summary>Creates a branch from the current commit and switches to it.</summary>
        /// <param name="branch">The new branch's name.</param>
        /// <returns>What Git said.</returns>
        public string CreateBranch(string branch)
        {
            RequireRepository();

            return Require(new[] { "switch", "-c", GitInput.RequireBranch(branch) }).Trim();
        }

        /// <summary>Switches to another branch, the folder's files becoming that branch's.</summary>
        /// <param name="branch">The branch.</param>
        /// <returns>What Git said.</returns>
        /// <exception cref="PortalException">Uncommitted changes, or Git failed.</exception>
        public string SwitchBranch(string branch)
        {
            RequireClean("switching branch");

            return Require(new[] { "switch", GitInput.RequireBranch(branch) }).Trim();
        }

        /// <summary>Commits every change in the folder.</summary>
        /// <param name="message">What changed.</param>
        /// <returns>The commit's line, as Git printed it.</returns>
        /// <exception cref="PortalException">Nothing to commit, an invalid message, or Git failed.</exception>
        public string Commit(string message)
        {
            RequireRepository();
            var text = GitInput.RequireMessage(message);

            Require(new[] { "add", "--all" });

            if (Status().Count <= 1)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Nothing to commit: the workspace's files are as the last commit has them");
            }

            var messageFile = Path.Combine(Path.GetTempPath(), "tia-mcp-commit-" + Guid.NewGuid().ToString("N") + ".txt");

            try
            {
                File.WriteAllText(messageFile, text + "\n", new UTF8Encoding(false));

                return Require(new[] { "commit", "-F" }, messageFile).Trim();
            }
            finally
            {
                File.Delete(messageFile);
            }
        }

        /// <summary>Brings in the remote branch's commits, only when no merge is needed.</summary>
        /// <returns>What Git said.</returns>
        /// <exception cref="PortalException">Uncommitted changes, branches that diverged, no remote, or Git failed.</exception>
        public string Pull()
        {
            RequireClean("pulling");

            return Require(new[] { "pull", "--ff-only" }).Trim();
        }

        /// <summary>Writes the commits made since a branch or commit as patch files.</summary>
        /// <param name="since">The branch or commit they were made after, e.g. <c>main</c>.</param>
        /// <param name="outputDirectory">The folder the patch files go in, a full path.</param>
        /// <returns>The patch files written.</returns>
        public IReadOnlyList<string> ExportChanges(string since, string outputDirectory)
        {
            var folder = GitInput.RequireFullPath(outputDirectory, "outputDirectory");
            RequireRepository();
            var basis = RequireRevision(since);

            Directory.CreateDirectory(folder);

            return Lines(Require(new[] { "format-patch", basis + "..HEAD", "-o" }, folder));
        }

        /// <summary>Applies the commits of a patch file, as <see cref="ExportChanges"/> writes them.</summary>
        /// <param name="patchFile">The file, a full path.</param>
        /// <returns>What Git said.</returns>
        /// <exception cref="PortalException">Uncommitted changes, a patch that does not apply, or Git failed.</exception>
        public string ImportChanges(string patchFile)
        {
            var patch = GitInput.RequireFullPath(patchFile, "patchFile");
            RequireClean("importing changes");

            if (!File.Exists(patch))
            {
                throw new PortalException(PortalErrorCode.NotFound, $"No patch file at '{patch}'");
            }

            Require(new[] { "apply", "--check" }, patch);
            var applied = _git.Run(_root, new[] { "am" }, patch);

            if (applied.Succeeded)
            {
                return applied.Output.Trim();
            }

            var aborted = _git.Run(_root, new[] { "am", "--abort" });

            if (!aborted.Succeeded)
            {
                throw new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"The patch did not apply ({applied.Error.Trim()}), and undoing it failed too ({aborted.Error.Trim()}): the workspace is part-way through 'git am'. Run 'git am --abort' in a terminal there.");
            }

            throw new PortalException(PortalErrorCode.InvalidState, $"The patch did not apply and was undone: {applied.Error.Trim()}");
        }

        private static string RequireRevision(string revision)
        {
            var text = (revision ?? string.Empty).Trim();

            return CommitHash.IsMatch(text) ? text : GitInput.RequireBranch(text);
        }

        /// <remarks>Its settings are checked on every call: they can change between two of them.</remarks>
        private void RequireRepository()
        {
            GitRepositorySafety.RequireNoLinkedRepository(_root);

            if (!Directory.Exists(Path.Combine(_root, ".git")))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{_root}' is not a Git repository yet; GitInitWorkspace makes it one");
            }

            GitRepositorySafety.Require(_root, _git);
        }

        private void RequireClean(string doing)
        {
            var changed = Status().Skip(1).ToList();

            if (changed.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Not {doing}: the workspace has uncommitted changes ({string.Join(", ", changed.Take(5))}{(changed.Count > 5 ? ", ..." : string.Empty)}). Commit them first, or synchronise them away.");
            }
        }

        private string Require(IReadOnlyList<string> arguments, string? filePath = null)
        {
            var result = _git.Run(_root, arguments, filePath);

            return result.Succeeded
                ? result.Output
                : throw new PortalException(PortalErrorCode.InvalidState, $"git {arguments[0]} failed: {(result.Error.Trim().Length > 0 ? result.Error.Trim() : result.Output.Trim())}");
        }

        private static List<string> Lines(string text)
        {
            return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}
