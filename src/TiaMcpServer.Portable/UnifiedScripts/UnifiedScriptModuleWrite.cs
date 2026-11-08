using System;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>A global script module to write to a WinCC Unified HMI.</summary>
    public sealed class UnifiedScriptModuleWrite
    {
        /// <summary>Creates the description of a write.</summary>
        /// <param name="name">The module's name.</param>
        /// <param name="code">Its JavaScript, with <c>export function</c> for what screens may call.</param>
        /// <param name="replace">Whether a module of that name may be replaced.</param>
        public UnifiedScriptModuleWrite(string name, string code, bool replace)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Replace = replace;
        }

        /// <summary>The module's name.</summary>
        public string Name { get; }

        /// <summary>Its JavaScript.</summary>
        public string Code { get; }

        /// <summary>Whether a module of that name may be replaced.</summary>
        public bool Replace { get; }
    }
}
