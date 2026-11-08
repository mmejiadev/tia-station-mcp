using System.Security;

namespace TiaMcpServer.Secrets
{
    /// <summary>The terminal a person types a secret into.</summary>
    public interface ISecretConsole
    {
        /// <summary>Whether a person can type here: a piped input cannot be one.</summary>
        bool IsInteractive { get; }

        /// <summary>Reads a value without showing it.</summary>
        /// <param name="prompt">What to ask.</param>
        /// <returns>The value; the caller disposes it.</returns>
        SecureString ReadHidden(string prompt);

        /// <summary>Tells the person something. Never a value.</summary>
        /// <param name="message">The message.</param>
        void WriteLine(string message);
    }
}
