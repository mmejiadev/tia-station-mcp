using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using TiaMcpServer.Secrets;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>A secret store holding values made up for the tests, never one of the machine's.</summary>
    /// <remarks>Read-only: the device template tests only reveal, and a store that wrote would be a second one to keep apart from the real.</remarks>
    internal sealed class MadeUpSecrets : ISecretStore
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public MadeUpSecrets(IReadOnlyDictionary<string, string> values)
        {
            _values = values;
        }

        public IReadOnlyList<string> Names()
        {
            return _values.Keys.ToList();
        }

        public void CheckCanStore(string name, bool replace)
        {
            throw new NotSupportedException();
        }

        public void Store(string name, SecureString value, bool replace)
        {
            throw new NotSupportedException();
        }

        public SecureString Reveal(string name)
        {
            if (!_values.TryGetValue(name, out var text))
            {
                throw new PortalException(PortalErrorCode.NotFound, "There is no secret of that name");
            }

            var value = new SecureString();
            foreach (var character in text)
            {
                value.AppendChar(character);
            }

            return value;
        }

        public void Delete(string name)
        {
            throw new NotSupportedException();
        }
    }
}
