using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>What a script is checked against: the HMI's tags.</summary>
    /// <remarks>
    /// When there are none, the rules that need them say nothing rather than guess. There is no list of
    /// the Unified system functions to check calls against: the one TIA Portal V20 ships as XML
    /// (<c>Data\Hmi\Scripting\Utah-Functions.en.xml</c>) is classic WinCC's and has no <c>SetTagValue</c>,
    /// and the Unified one is packed in an undocumented binary (measured 2026-10-25).
    /// </remarks>
    public sealed class UnifiedScriptContext
    {
        /// <summary>A context without tags: only the rules that need none run.</summary>
        public static readonly UnifiedScriptContext None = new UnifiedScriptContext(new Dictionary<string, string>());

        /// <summary>Creates a context.</summary>
        /// <param name="tagTypes">Every HMI tag, system tags included, with its data type; in any case.</param>
        public UnifiedScriptContext(IReadOnlyDictionary<string, string> tagTypes)
        {
            TagTypes = (tagTypes ?? throw new ArgumentNullException(nameof(tagTypes))).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Every HMI tag with its data type.</summary>
        public IReadOnlyDictionary<string, string> TagTypes { get; }
    }
}
