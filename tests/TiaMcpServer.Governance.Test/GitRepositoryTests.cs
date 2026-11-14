using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Git;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Against real Git in temporary folders: what the operations do is Git's behaviour, and a test of a stand-in
    /// would only test the stand-in. Git for Windows has to be installed, as it is wherever this server runs Git.
    /// Each repository gets its own author and no signing, so a machine's global settings do not decide the result.
    /// </remarks>
    [TestClass]
    public sealed class GitRepositoryTests
    {
        private static readonly GitRunner Git = new GitRunner(TimeSpan.FromMinutes(1));

        private string _root = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _root = NewFolder();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DeleteFolder(_root);
        }

        [TestMethod]
        public void Init_AFolder_MakesARepositoryOnMain()
        {
            var repository = NewRepository(_root);

            StringAssert.Contains(repository.Status()[0], "main", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Init_ARepositoryAlready_ThrowsInvalidState()
        {
            var repository = NewRepository(_root);

            Assert.ThrowsException<PortalException>(() => repository.Init());
        }

        [TestMethod]
        public void Commit_AChangedFile_RecordsItWithItsMessage()
        {
            var repository = NewRepository(_root);
            File.WriteAllText(Path.Combine(_root, "Main.xml"), "<Block />");

            repository.Commit("Main: first version");

            StringAssert.Contains(repository.Log(1).Single(), "Main: first version", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Commit_NothingChanged_ThrowsInvalidState()
        {
            var repository = CommittedRepository(_root);

            var refusal = Assert.ThrowsException<PortalException>(() => repository.Commit("again"));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void CreateBranch_ANewName_SwitchesToIt()
        {
            var repository = CommittedRepository(_root);

            repository.CreateBranch("feature/conveyor");

            Assert.IsTrue(repository.Branches().Contains("*feature/conveyor"), string.Join(", ", repository.Branches()));
        }

        /// <remarks>Git would carry the change over or stop half-way; it is refused before Git is asked.</remarks>
        [TestMethod]
        public void SwitchBranch_WithUncommittedChanges_ThrowsAndStaysPut()
        {
            var repository = CommittedRepository(_root);
            repository.CreateBranch("feature/conveyor");
            File.WriteAllText(Path.Combine(_root, "Main.xml"), "<Block changed=\"yes\" />");

            Assert.ThrowsException<PortalException>(() => repository.SwitchBranch("main"));

            Assert.IsTrue(repository.Branches().Contains("*feature/conveyor"));
        }

        [TestMethod]
        public void SwitchBranch_Back_GivesTheBranchsFiles()
        {
            var repository = CommittedRepository(_root);
            repository.CreateBranch("feature/conveyor");
            File.WriteAllText(Path.Combine(_root, "Main.xml"), "<Block changed=\"yes\" />");
            repository.Commit("changed on the branch");

            repository.SwitchBranch("main");

            Assert.AreEqual("<Block />", File.ReadAllText(Path.Combine(_root, "Main.xml")));
        }

        /// <remarks>Changes carried to another copy without a server: exported as patches on one side, imported on the other.</remarks>
        [TestMethod]
        public void ImportChanges_PatchesExportedFromAnotherCopy_BringItsCommit()
        {
            var repository = CommittedRepository(_root);
            var other = NewFolder();

            try
            {
                CopyFolder(_root, other);
                repository.CreateBranch("feature/conveyor");
                File.WriteAllText(Path.Combine(_root, "Main.xml"), "<Block changed=\"yes\" />");
                repository.Commit("Conveyor change");
                var patches = repository.ExportChanges("main", Path.Combine(NewFolder(), "patches"));

                new GitRepository(other, Git).ImportChanges(patches.Single());

                Assert.AreEqual("<Block changed=\"yes\" />", File.ReadAllText(Path.Combine(other, "Main.xml")));
            }
            finally
            {
                DeleteFolder(other);
            }
        }

        [TestMethod]
        public void ImportChanges_APatchThatDoesNotApply_ThrowsAndChangesNothing()
        {
            var repository = CommittedRepository(_root);
            var patch = Path.Combine(NewFolder(), "bad.patch");
            File.WriteAllText(patch, "From 0000000000000000000000000000000000000000 Mon Sep 17 00:00:00 2001\nSubject: [PATCH] bad\n\n---\n Missing.xml | 1 -\n\ndiff --git a/Missing.xml b/Missing.xml\n--- a/Missing.xml\n+++ b/Missing.xml\n@@ -1 +0,0 @@\n-<Nothing />\n");

            Assert.ThrowsException<PortalException>(() => repository.ImportChanges(patch));

            Assert.AreEqual(1, repository.Log(10).Count);
            Assert.AreEqual(1, repository.Status().Count, string.Join(", ", repository.Status()));
        }

        [TestMethod]
        public void ExportChanges_AnUnsafeBase_ThrowsInvalidParams()
        {
            var repository = CommittedRepository(_root);

            Assert.ThrowsException<PortalException>(() => repository.ExportChanges("--output=x", NewFolder()));
        }

        /// <remarks>core.fsmonitor makes 'git status' run a program; the setting is refused before Git is asked for a status.</remarks>
        [TestMethod]
        public void Status_ARepositorySettingThatRunsAProgram_ThrowsInvalidState()
        {
            var repository = CommittedRepository(_root);
            Git.Run(_root, new[] { "config", "core.fsmonitor", "hostname" });

            var refusal = Assert.ThrowsException<PortalException>(() => repository.Status());

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "core.fsmonitor", StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void Commit_ARepositoryWithAHook_ThrowsInvalidState()
        {
            var repository = CommittedRepository(_root);
            File.WriteAllText(Path.Combine(_root, ".git", "hooks", "pre-commit"), "#!/bin/sh\nexit 0\n");
            File.WriteAllText(Path.Combine(_root, "Main.xml"), "<Block changed=\"yes\" />");

            var refusal = Assert.ThrowsException<PortalException>(() => repository.Commit("with a hook"));

            StringAssert.Contains(refusal.Message, "pre-commit", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Init_AFolderWhoseGitPointsElsewhere_ThrowsInvalidState()
        {
            File.WriteAllText(Path.Combine(_root, ".git"), "gitdir: C:/elsewhere/.git\n");

            var refusal = Assert.ThrowsException<PortalException>(() => new GitRepository(_root, Git).Init());

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void Status_ARepositoryWithARemote_IsRead()
        {
            var repository = CommittedRepository(_root);
            Git.Run(_root, new[] { "remote", "add", "origin", "https://example.invalid/cell.git" });

            Assert.IsTrue(repository.Status().Count >= 1);
        }

        /// <remarks>A commondir sends Git to another folder's settings and hooks, which the checks would not read.</remarks>
        [TestMethod]
        public void Status_ARepositoryWithACommonDir_ThrowsInvalidState()
        {
            var repository = CommittedRepository(_root);
            File.WriteAllText(Path.Combine(_root, ".git", "commondir"), "../elsewhere");

            var refusal = Assert.ThrowsException<PortalException>(() => repository.Status());

            StringAssert.Contains(refusal.Message, "commondir", StringComparison.Ordinal);
        }

        /// <remarks>A submodule is another repository Git enters, with settings of its own.</remarks>
        [TestMethod]
        public void Status_ARepositoryWithSubmodules_ThrowsInvalidState()
        {
            var repository = CommittedRepository(_root);
            File.WriteAllText(Path.Combine(_root, ".gitmodules"), "[submodule \"sub\"]\n\tpath = sub\n");

            var refusal = Assert.ThrowsException<PortalException>(() => repository.Status());

            StringAssert.Contains(refusal.Message, ".gitmodules", StringComparison.Ordinal);
        }

        /// <remarks>Switched on, a repository's signing setting makes every commit start a signing program.</remarks>
        [TestMethod]
        public void Commit_ARepositoryThatSignsCommits_ThrowsInvalidState()
        {
            var repository = CommittedRepository(_root);
            Git.Run(_root, new[] { "config", "commit.gpgsign", "true" });
            File.WriteAllText(Path.Combine(_root, "Main.xml"), "<Block changed=\"yes\" />");

            var refusal = Assert.ThrowsException<PortalException>(() => repository.Commit("signed"));

            StringAssert.Contains(refusal.Message, "commit.gpgsign", StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        [DataRow("\\\\host\\share")]
        [DataRow("C:\\")]
        [DataRow("relative\\folder")]
        public void RequireWorkspaceFolder_ANetworkShareADriveOrRelative_ThrowsInvalidState(string folder)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => GitInput.RequireWorkspaceFolder(folder));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void RequireWorkspaceFolder_AFolderOnADrive_IsTaken()
        {
            Assert.AreEqual(_root.TrimEnd('\\'), GitInput.RequireWorkspaceFolder(_root).TrimEnd('\\'));
        }

        [TestMethod]
        [DataRow("patches")]
        [DataRow("C:patches")]
        [DataRow("\\patches")]
        [DataRow("\\\\host\\share\\patches")]
        public void ExportChanges_ARelativeFolder_ThrowsInvalidParams(string folder)
        {
            var repository = CommittedRepository(_root);

            var refusal = Assert.ThrowsException<PortalException>(() => repository.ExportChanges("main", folder));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void Status_NotARepository_ThrowsInvalidState()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => new GitRepository(_root, Git).Status());

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        private static GitRepository NewRepository(string root)
        {
            var repository = new GitRepository(root, Git);
            repository.Init();
            Git.Run(root, new[] { "config", "user.name", "Test" });
            Git.Run(root, new[] { "config", "user.email", "test@example.invalid" });
            Git.Run(root, new[] { "config", "commit.gpgsign", "false" });

            return repository;
        }

        private static GitRepository CommittedRepository(string root)
        {
            var repository = NewRepository(root);
            File.WriteAllText(Path.Combine(root, "Main.xml"), "<Block />");
            repository.Commit("first");

            return repository;
        }

        private static string NewFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "tia-git-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(folder);

            return folder;
        }

        private static void CopyFolder(string from, string to)
        {
            foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(to, file.Substring(from.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }

        /// <remarks>Git marks its object files read-only, which Directory.Delete refuses.</remarks>
        private static void DeleteFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(folder, true);
        }
    }
}
