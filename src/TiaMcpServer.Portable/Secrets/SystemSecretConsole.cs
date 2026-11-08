using System;
using System.Security;

namespace TiaMcpServer.Secrets
{
    /// <summary>The Windows console.</summary>
    public sealed class SystemSecretConsole : ISecretConsole
    {
        /// <inheritdoc />
        public bool IsInteractive => !Console.IsInputRedirected;

        /// <inheritdoc />
        public SecureString ReadHidden(string prompt)
        {
            Console.Write(prompt);
            var value = new SecureString();

            for (var key = Console.ReadKey(intercept: true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(intercept: true))
            {
                Apply(key, value);
            }

            Console.WriteLine();

            return value;
        }

        /// <inheritdoc />
        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        private static void Apply(ConsoleKeyInfo key, SecureString value)
        {
            if (key.Key == ConsoleKey.Backspace && value.Length > 0)
            {
                value.RemoveAt(value.Length - 1);
                return;
            }

            // One past the limit, so that the store refuses it rather than this cutting it short.
            if (!char.IsControl(key.KeyChar) && value.Length <= DpapiSecretStore.MaxValueLength)
            {
                value.AppendChar(key.KeyChar);
            }
        }
    }
}
