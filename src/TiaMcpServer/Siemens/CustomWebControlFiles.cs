using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.CustomWebControls;

namespace TiaMcpServer.Siemens
{
    /// <summary>Where a project's Custom Web Controls live, what is installed there, and where WebCC comes from.</summary>
    /// <remarks>
    /// Measured 2026-11-15 on TIA Portal V21: a project's own controls are the packages in
    /// <c>&lt;project folder&gt;\UserFiles\CustomControls</c>, named <c>{GUID}.zip</c>; Siemens' come with TIA Portal in
    /// <c>Portal V21\Data\Hmi\CustomControls</c>, and every one of those carries WebCC, which a new control needs and this
    /// server does not ship. The TIA Portal folder is the parent of the <c>PublicAPI</c> folder the Openness assembly is
    /// loaded from (<c>Portal V21\PublicAPI\V21\net48</c>), and nothing above it is searched: a folder a user can create
    /// higher up, such as <c>D:\Data\Hmi\CustomControls</c>, must not supply the script every control will carry
    /// (security review of 2026-11-15). The project's folder is not trusted either, being data from wherever the project
    /// came from: a folder that is a junction or link is refused, and a package is written whole or not at all.
    /// </remarks>
    internal static class CustomWebControlFiles
    {
        private const string PackagePattern = "{*}.zip";
        private const string PublicApiFolder = "PublicAPI";

        /// <summary>The project's own Custom Web Control folder.</summary>
        /// <param name="project">The open project.</param>
        /// <returns><c>&lt;project folder&gt;\UserFiles\CustomControls</c>, whether or not it exists yet.</returns>
        internal static string ProjectFolder(Project project)
        {
            return Path.Combine(project.Path.Directory!.FullName, "UserFiles", "CustomControls");
        }

        /// <summary>Reads every package in a folder.</summary>
        /// <param name="folder">The folder; one that does not exist holds nothing.</param>
        /// <returns>Each readable package's file and summary, and each unreadable one's file and why.</returns>
        internal static (IReadOnlyList<(string File, CustomWebControlSummary Summary)> Read, IReadOnlyList<string> Unreadable) ReadInstalled(string folder)
        {
            var read = new List<(string, CustomWebControlSummary)>();
            var unreadable = new List<string>();

            if (!Directory.Exists(folder))
            {
                return (read, unreadable);
            }

            foreach (var file in Directory.GetFiles(folder, PackagePattern).OrderBy(found => found, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    read.Add((file, CustomWebControlPackageReader.ReadManifest(stream)));
                }
                catch (Exception unreadablePackage) when (IsUnreadable(unreadablePackage))
                {
                    unreadable.Add($"{Path.GetFileName(file)}: {unreadablePackage.Message.Trim()}");
                }
            }

            return (read, unreadable);
        }

        /// <summary>Finds WebCC in the TIA Portal the server runs on.</summary>
        /// <returns>The bytes of <c>webcc.min.js</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when no package of Siemens' carries it.</exception>
        internal static byte[] FindWebCc()
        {
            var folder = SiemensControlsFolder()
                ?? throw new PortalException(PortalErrorCode.InvalidState, "TIA Portal's own Custom Web Controls (Data\\Hmi\\CustomControls) were not found beside the Openness assembly, and a new control needs the WebCC library they carry");

            var skipped = new List<string>();
            var packages = Directory.GetFiles(folder, "*.zip").OrderBy(found => found, StringComparer.OrdinalIgnoreCase).ToList();

            // Every package's control/libs before any package's control/static: see CustomWebControlPackageReader.LibraryEntries.
            foreach (var entryPath in CustomWebControlPackageReader.LibraryEntries)
            {
                foreach (var package in packages)
                {
                    var library = TryReadLibrary(package, entryPath, skipped);

                    if (library != null)
                    {
                        return library;
                    }
                }
            }

            throw new PortalException(PortalErrorCode.InvalidState, $"None of TIA Portal's Custom Web Controls in '{folder}' carries a readable webcc.min.js{(skipped.Count == 0 ? string.Empty : "; unreadable: " + string.Join("; ", skipped))}");
        }

        /// <remarks>A package of Siemens' that cannot be read is skipped, another carrying the same library, and why is kept for the refusal if none can.</remarks>
        private static byte[]? TryReadLibrary(string package, string entryPath, List<string> skipped)
        {
            try
            {
                using var stream = File.OpenRead(package);

                return CustomWebControlPackageReader.ReadLibraryAt(stream, entryPath);
            }
            catch (Exception unreadable) when (IsUnreadable(unreadable))
            {
                skipped.Add($"{Path.GetFileName(package)}: {unreadable.Message.Trim()}");
                return null;
            }
        }

        /// <remarks>What reading a package that is locked, damaged or not a package raises; anything else is not expected and goes on up.</remarks>
        private static bool IsUnreadable(Exception failure)
        {
            return failure is PortalException || failure is IOException || failure is UnauthorizedAccessException || failure is InvalidDataException;
        }

        /// <summary>Writes a package whole: to a temporary file beside it first, then into place.</summary>
        /// <param name="folder">The project's control folder.</param>
        /// <param name="file">The package's full path in it.</param>
        /// <param name="package">The package's bytes.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when the folder, or a folder above it in the project, is a junction or link.</exception>
        internal static void WritePackage(string folder, string file, byte[] package)
        {
            RequireNoLink(folder);
            Directory.CreateDirectory(folder);
            RequireNoLink(folder);

            var temporary = file + ".writing";
            File.WriteAllBytes(temporary, package);

            if (File.Exists(file))
            {
                File.Replace(temporary, file, null);
                return;
            }

            File.Move(temporary, file);
        }

        /// <remarks>The control folder and <c>UserFiles</c> above it: either one a junction would send the write out of the project.</remarks>
        private static void RequireNoLink(string folder)
        {
            foreach (var checkedFolder in new[] { folder, Path.GetDirectoryName(folder) })
            {
                if (checkedFolder != null && Directory.Exists(checkedFolder) && new DirectoryInfo(checkedFolder).Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"'{checkedFolder}' is a junction or link, so a package written there would land outside the project; nothing was written");
                }
            }
        }

        private static string? SiemensControlsFolder()
        {
            var location = typeof(TiaPortal).Assembly.Location;
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            for (var folder = new FileInfo(location).Directory; folder != null; folder = folder.Parent)
            {
                if (string.Equals(folder.Name, PublicApiFolder, StringComparison.OrdinalIgnoreCase) && folder.Parent != null)
                {
                    var candidate = Path.Combine(folder.Parent.FullName, "Data", "Hmi", "CustomControls");

                    return Directory.Exists(candidate) ? candidate : null;
                }
            }

            return null;
        }
    }
}
