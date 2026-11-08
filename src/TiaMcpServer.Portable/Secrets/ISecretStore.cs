using System.Collections.Generic;
using System.Security;

namespace TiaMcpServer.Secrets
{
    /// <summary>Values the server needs and must never see in the open: kept encrypted, used by name.</summary>
    public interface ISecretStore
    {
        /// <summary>The names of the secrets held; never a value.</summary>
        /// <returns>The names, in order.</returns>
        IReadOnlyList<string> Names();

        /// <summary>Throws unless a secret of that name can be stored, so that nobody types a value for nothing.</summary>
        /// <param name="name">Its name.</param>
        /// <param name="replace">True to replace a secret of that name.</param>
        void CheckCanStore(string name, bool replace);

        /// <summary>Stores a secret.</summary>
        /// <param name="name">Its name.</param>
        /// <param name="value">Its value.</param>
        /// <param name="replace">True to replace a secret of that name.</param>
        void Store(string name, SecureString value, bool replace);

        /// <summary>Reads a secret, for the moment it is handed to TIA Portal.</summary>
        /// <param name="name">Its name.</param>
        /// <returns>The value; the caller disposes it.</returns>
        SecureString Reveal(string name);

        /// <summary>Deletes a secret.</summary>
        /// <param name="name">Its name.</param>
        void Delete(string name);
    }
}
