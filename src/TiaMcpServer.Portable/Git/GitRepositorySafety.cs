using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Git
{
    /// <summary>
    /// Refuses a repository whose own settings could make Git run a program: anything in its configuration that is not
    /// on a short list, a hook, or a <c>.git</c> that points elsewhere.
    /// </summary>
    /// <remarks>
    /// The folder comes from a TIA Portal project's VCI workspace, and a project is untrusted data: one unpacked from a
    /// shared archive can carry its repository with it. Git reads a repository's own configuration and hooks and acts on
    /// them — <c>core.fsmonitor</c> runs a command on every <c>git status</c>, a <c>filter</c> driver on every
    /// <c>git add</c>, a hook on every commit and <c>am</c>. So the configuration is listed first, without following its
    /// includes, and only the keys <c>git init</c>, <c>git remote add</c> and the person's identity write are taken;
    /// anything else refuses, which is what fail-closed means for a list nobody can make complete the other way round.
    /// The person's own global configuration is theirs and is not checked.
    /// </remarks>
    public static class GitRepositorySafety
    {
        private const string SampleHookSuffix = ".sample";

        private static readonly HashSet<string> AllowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "core.repositoryformatversion",
            "core.filemode",
            "core.bare",
            "core.logallrefupdates",
            "core.ignorecase",
            "core.autocrlf",
            "core.safecrlf",
            "core.longpaths",
            "user.name",
            "user.email",
            "extensions.objectformat"
        };

        /// <remarks>
        /// Taken with one value only: <c>git init</c> on Windows writes <c>core.symlinks=false</c>, and a signing setting
        /// is fine switched off — switched on, a repository would make every commit start a signing program.
        /// </remarks>
        private static readonly Dictionary<string, string> AllowedOnlyAs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["core.symlinks"] = "false",
            ["commit.gpgsign"] = "false"
        };

        private static readonly Regex AllowedSubsectionKey = new Regex(
            @"^(remote\.[^.]+\.(url|fetch)|branch\.[^.]+\.(remote|merge))$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>Refuses the repository at a folder unless its settings run nothing.</summary>
        /// <param name="root">The workspace's folder, which holds <c>.git</c>.</param>
        /// <param name="git">The runner, to list the configuration.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming what was found.</exception>
        public static void Require(string root, GitRunner git)
        {
            if (git == null)
            {
                throw new ArgumentNullException(nameof(git));
            }

            var gitDirectory = Path.Combine(root, ".git");

            RequireOwnLayout(root, gitDirectory);
            RequireNoHooks(gitDirectory);
            RequireKnownSettings(root, git);
        }

        /// <summary>Refuses a <c>.git</c> file, which points Git at a repository somewhere else.</summary>
        /// <param name="root">The workspace's folder.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when there is one.</exception>
        public static void RequireNoLinkedRepository(string root)
        {
            if (File.Exists(Path.Combine(root, ".git")))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{root}' holds a .git file pointing at a repository elsewhere; this server works only on a repository of its own folder. Nothing was run.");
            }
        }

        /// <remarks>
        /// A <c>commondir</c> sends Git to another folder for its settings and hooks, which the checks here would not
        /// read; a submodule is another repository Git enters, with settings and hooks of its own. Neither is something a
        /// VCI workspace needs, so both refuse.
        /// </remarks>
        private static void RequireOwnLayout(string root, string gitDirectory)
        {
            var found = new[]
            {
                File.Exists(Path.Combine(gitDirectory, "commondir")) ? ".git/commondir" : null,
                File.Exists(Path.Combine(root, ".gitmodules")) ? ".gitmodules" : null,
                Directory.Exists(Path.Combine(gitDirectory, "modules")) ? ".git/modules" : null
            }.Where(entry => entry != null).ToList();

            if (found.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The repository holds {string.Join(", ", found)}: it reaches settings or repositories outside its own .git, which this server does not check. Nothing was run.");
            }
        }

        private static void RequireNoHooks(string gitDirectory)
        {
            var hooks = Path.Combine(gitDirectory, "hooks");

            if (!Directory.Exists(hooks))
            {
                return;
            }

            var active = Directory.EnumerateFileSystemEntries(hooks)
                .Where(entry => !entry.EndsWith(SampleHookSuffix, StringComparison.OrdinalIgnoreCase) || Directory.Exists(entry))
                .Select(Path.GetFileName)
                .ToList();

            if (active.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The repository has hooks ({string.Join(", ", active)}), which Git would run; this server does not run a repository's own programs. Remove them in a terminal if they are yours. Nothing was run.");
            }
        }

        private static void RequireKnownSettings(string root, GitRunner git)
        {
            // -z: each entry ends in NUL and its key ends in a new line, so a value holding '=' or a new line is not misread.
            var listed = git.Run(root, new[] { "config", "--local", "--no-includes", "--list", "-z" });

            if (!listed.Succeeded)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The repository's settings could not be read: {listed.Error.Trim()}");
            }

            var unknown = listed.Output
                .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Split(new[] { '\n' }, 2))
                .Where(entry => !IsAllowed(entry[0], entry.Length > 1 ? entry[1] : string.Empty))
                .Select(entry => entry[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unknown.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The repository's own settings hold {string.Join(", ", unknown)}, which this server does not take: some settings make Git run a program. Remove them in a terminal if they are yours. Nothing was run.");
            }
        }

        private static bool IsAllowed(string key, string value)
        {
            if (AllowedOnlyAs.TryGetValue(key, out var only))
            {
                return string.Equals(value.Trim(), only, StringComparison.OrdinalIgnoreCase);
            }

            return AllowedKeys.Contains(key) || AllowedSubsectionKey.IsMatch(key);
        }
    }
}
