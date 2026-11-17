using System;
using System.Security.Cryptography;
using System.Text;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>The GUID of a Custom Web Control, derived from its name.</summary>
    /// <remarks>
    /// A name-based UUID in the manner of RFC 4122 version 5, hashed with SHA-256 rather than SHA-1, in a namespace of this
    /// server's own; nothing outside this server needs to compute it, so only its stability matters. TIA Portal finds a control's package by
    /// the GUID in its file name, <c>{GUID}.zip</c>, and places it by its name; deriving one from the other means a control
    /// installed again replaces its own package rather than adding a second one that shows under the same name.
    /// </remarks>
    public static class CustomWebControlGuid
    {
        private const int VersionByte = 6;
        private const int VariantByte = 8;
        private const int GuidLength = 16;

        private static readonly Guid Namespace = new Guid("6f0d2a52-6a07-4f5e-9d1b-3c9e7b21a4c8");

        /// <summary>The GUID for a control's name.</summary>
        /// <param name="name">The control's <c>identity.name</c>; compared as written, so case matters.</param>
        /// <returns>The same GUID for the same name, every time.</returns>
        public static Guid For(string name)
        {
            var namespaceBytes = ToNetworkOrder(Namespace.ToByteArray());
            var nameBytes = Encoding.UTF8.GetBytes(name ?? string.Empty);
            var input = new byte[namespaceBytes.Length + nameBytes.Length];

            Buffer.BlockCopy(namespaceBytes, 0, input, 0, namespaceBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, input, namespaceBytes.Length, nameBytes.Length);

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(input);
            var bytes = new byte[GuidLength];
            Array.Copy(hash, bytes, GuidLength);

            bytes[VersionByte] = (byte)((bytes[VersionByte] & 0x0F) | 0x50);
            bytes[VariantByte] = (byte)((bytes[VariantByte] & 0x3F) | 0x80);

            return new Guid(ToNetworkOrder(bytes));
        }

        /// <remarks><see cref="Guid.ToByteArray"/> keeps its first three fields little-endian; RFC 4122 hashes them big-endian.</remarks>
        private static byte[] ToNetworkOrder(byte[] bytes)
        {
            var swapped = (byte[])bytes.Clone();

            Array.Reverse(swapped, 0, 4);
            Array.Reverse(swapped, 4, 2);
            Array.Reverse(swapped, 6, 2);

            return swapped;
        }
    }
}
