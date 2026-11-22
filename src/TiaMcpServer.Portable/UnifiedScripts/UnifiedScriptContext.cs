using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>What a script is checked against: the HMI's tags and its global script modules.</summary>
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
            : this(tagTypes, new Dictionary<string, IReadOnlyCollection<string>>())
        {
            KnowsModules = false;
        }

        /// <summary>Creates a context with the HMI's global script modules.</summary>
        /// <param name="tagTypes">Every HMI tag, system tags included, with its data type; in any case.</param>
        /// <param name="moduleFunctions">Each module's name with the functions it offers (<see cref="UnifiedScriptExports"/>).</param>
        public UnifiedScriptContext(IReadOnlyDictionary<string, string> tagTypes, IReadOnlyDictionary<string, IReadOnlyCollection<string>> moduleFunctions)
        {
            TagTypes = (tagTypes ?? throw new ArgumentNullException(nameof(tagTypes))).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

            // A module is reached by its name as JavaScript spells it, so case counts here, unlike for tags.
            ModuleFunctions = (moduleFunctions ?? throw new ArgumentNullException(nameof(moduleFunctions))).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            KnowsModules = true;
        }

        /// <summary>Every HMI tag with its data type.</summary>
        public IReadOnlyDictionary<string, string> TagTypes { get; }

        /// <summary>Each global script module with the functions it offers.</summary>
        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> ModuleFunctions { get; }

        /// <summary>Whether the HMI's modules are known; when not, the rules about modules say nothing rather than guess.</summary>
        public bool KnowsModules { get; }
    }
}
