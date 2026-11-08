using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace TiaMcpServer.Secrets
{
    /// <summary>A secret's characters as UTF-16 bytes, for the moment of encrypting or comparing it. No managed string holds it.</summary>
    internal static class SecretBytes
    {
        /// <summary>The bytes of a value; the caller clears them.</summary>
        /// <param name="value">The value.</param>
        /// <returns>Its UTF-16 bytes.</returns>
        public static byte[] Of(SecureString value)
        {
            var pointer = Marshal.SecureStringToGlobalAllocUnicode(value);

            try
            {
                var bytes = new byte[value.Length * sizeof(char)];
                Marshal.Copy(pointer, bytes, 0, bytes.Length);

                return bytes;
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(pointer);
            }
        }

        /// <summary>A value from its bytes.</summary>
        /// <param name="bytes">Its UTF-16 bytes.</param>
        /// <returns>The value, read-only.</returns>
        public static SecureString ToSecureString(byte[] bytes)
        {
            var value = new SecureString();

            for (var index = 0; index < bytes.Length; index += sizeof(char))
            {
                value.AppendChar(BitConverter.ToChar(bytes, index));
            }

            value.MakeReadOnly();

            return value;
        }

        /// <summary>Whether two values are the same.</summary>
        /// <param name="first">One.</param>
        /// <param name="second">The other.</param>
        /// <returns>True when they are.</returns>
        public static bool AreEqual(SecureString first, SecureString second)
        {
            var one = Of(first);
            var other = Of(second);

            try
            {
                return one.SequenceEqual(other);
            }
            finally
            {
                Array.Clear(one, 0, one.Length);
                Array.Clear(other, 0, other.Length);
            }
        }
    }
}
