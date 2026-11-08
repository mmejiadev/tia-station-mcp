using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Secrets
{
    /// <summary>Secrets encrypted with Windows DPAPI for the current Windows account, one file each.</summary>
    /// <remarks>
    /// Under the account's local application data and never beside a project, which is where a repository would pick it up.
    /// Each file is bound to its name through DPAPI's entropy, so a file renamed cannot be read as another secret.
    /// No message repeats a name: a value typed in the wrong place must not come back on the screen.
    /// The folder is deliberately not an option: nothing should be able to point the store at a project.
    /// </remarks>
    public sealed class DpapiSecretStore : ISecretStore
    {
        /// <summary>The most characters a value may have.</summary>
        public const int MaxValueLength = 256;

        private const string Extension = ".secret";

        private const string Purpose = "TiaMcpServer.Secret.v1:";

        private static readonly Regex ValidName = new Regex(@"\A[A-Za-z][A-Za-z0-9_-]{0,63}\z", RegexOptions.CultureInvariant);

        // Device names Windows will not create a file for.
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "con", "prn", "aux", "nul",
            "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
        };

        private readonly string _root;

        /// <summary>Creates the store.</summary>
        /// <param name="root">The folder holding the files.</param>
        public DpapiSecretStore(string root)
        {
            _root = string.IsNullOrWhiteSpace(root) ? throw new ArgumentException("The folder is required", nameof(root)) : root;
        }

        /// <summary>The store of the current Windows account, in its own data.</summary>
        /// <returns>The store.</returns>
        public static DpapiSecretStore ForCurrentUser()
        {
            return new DpapiSecretStore(GetDefaultRoot());
        }

        /// <summary>The folder of the current Windows account's secrets.</summary>
        /// <returns>Its full path.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> for an account with no local data folder: the path would otherwise be relative to wherever the server runs.</exception>
        public static string GetDefaultRoot()
        {
            var data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return string.IsNullOrEmpty(data)
                ? throw new PortalException(PortalErrorCode.InvalidState, "This Windows account has no local data folder to keep secrets in")
                : Path.Combine(data, "TiaMcpServer", "secrets");
        }

        /// <inheritdoc />
        public IReadOnlyList<string> Names()
        {
            if (!Directory.Exists(_root))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(_root, "*" + Extension)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(IsValidName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <inheritdoc />
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a bad name; <see cref="PortalErrorCode.InvalidState"/> for a name held when replacing was not asked for.</exception>
        public void CheckCanStore(string name, bool replace)
        {
            if (File.Exists(FileOf(name)) && !replace)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "There is already a secret of that name; nothing was changed. Ask to replace it if that is what you mean.");
            }
        }

        /// <inheritdoc />
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a bad name or value; <see cref="PortalErrorCode.InvalidState"/> for a name held when replacing was not asked for.</exception>
        public void Store(string name, SecureString value, bool replace)
        {
            CheckCanStore(name, replace);

            if (value == null || value.Length == 0 || value.Length > MaxValueLength)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"A secret's value must be between 1 and {MaxValueLength} characters");
            }

            var file = FileOf(name);
            var plain = SecretBytes.Of(value);

            try
            {
                Directory.CreateDirectory(_root);
                WriteInOneStep(file, ProtectedData.Protect(plain, EntropyOf(name), DataProtectionScope.CurrentUser));
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
            }
        }

        /// <inheritdoc />
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a bad name; <see cref="PortalErrorCode.NotFound"/> for one not held; <see cref="PortalErrorCode.InvalidState"/> when this account cannot read it.</exception>
        public SecureString Reveal(string name)
        {
            var file = RequireFileOf(name);
            byte[] plain;

            try
            {
                plain = ProtectedData.Unprotect(File.ReadAllBytes(file), EntropyOf(name), DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "That secret cannot be read by this Windows account; store it again");
            }

            try
            {
                return SecretBytes.ToSecureString(plain);
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
            }
        }

        /// <inheritdoc />
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a bad name; <see cref="PortalErrorCode.NotFound"/> for one not held.</exception>
        public void Delete(string name)
        {
            File.Delete(RequireFileOf(name));
        }

        private static bool IsValidName(string name)
        {
            return name != null && ValidName.IsMatch(name) && !ReservedNames.Contains(name);
        }

        private static byte[] EntropyOf(string name)
        {
            return Encoding.UTF8.GetBytes(Purpose + (name ?? string.Empty).ToUpperInvariant());
        }

        private string FileOf(string name)
        {
            if (!IsValidName(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "A secret's name starts with a letter and holds up to 64 letters, digits, '_' and '-'");
            }

            return Path.Combine(_root, name + Extension);
        }

        private string RequireFileOf(string name)
        {
            var file = FileOf(name);

            return File.Exists(file) ? file : throw new PortalException(PortalErrorCode.NotFound, "There is no secret of that name");
        }

        /// <remarks>Never half a file: a replacement that fails leaves the secret as it was, and no copy behind.</remarks>
        private static void WriteInOneStep(string file, byte[] content)
        {
            var written = $"{file}.{Guid.NewGuid():N}.tmp";

            try
            {
                File.WriteAllBytes(written, content);

                if (File.Exists(file))
                {
                    File.Replace(written, file, null);
                    return;
                }

                File.Move(written, file);
            }
            finally
            {
                File.Delete(written);
            }
        }
    }
}
