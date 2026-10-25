using System;
using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>A property whose value a JavaScript function returns, run on a trigger.</summary>
    /// <remarks>
    /// Openness's script dynamization (measured 2026-10-25 on TIA Portal V20). A new one starts with the
    /// trigger <c>AutomaticTags</c>, which runs the script whenever a tag it reads changes; a list of
    /// tags, or a fixed cycle, can be given instead. A custom cycle is not offered: the format of its
    /// duration has not been measured.
    /// </remarks>
    public sealed class UnifiedScriptAnimation : UnifiedAnimation
    {
        /// <summary>The trigger when none is given, and how a description spells <c>AutomaticTags</c>.</summary>
        public const string AutomaticTrigger = "automatic";

        /// <summary>The trigger that runs on the listed tags, as Openness names it.</summary>
        public const string TagsTrigger = "Tags";

        /// <summary>The triggers a description names, Openness's own <c>TriggerType</c> names but <c>automatic</c>.</summary>
        public static readonly IReadOnlyList<string> NamedTriggers = new[]
        {
            AutomaticTrigger, "Disabled", "T100ms", "T250ms", "T500ms", "T1s", "T2s", "T5s", "T10s"
        };

        /// <summary>Creates a script animation.</summary>
        /// <param name="path">The property's path.</param>
        /// <param name="code">The JavaScript, which returns the property's value, and its global definition area or null to leave TIA Portal's own.</param>
        /// <param name="trigger">One of <see cref="NamedTriggers"/>, or <see cref="TagsTrigger"/> with <paramref name="triggerTags"/>.</param>
        /// <param name="triggerTags">The tags that run the script, for <see cref="TagsTrigger"/>; empty otherwise.</param>
        public UnifiedScriptAnimation(string path, (string Script, string? GlobalDefinitions) code, string trigger, IReadOnlyList<string> triggerTags)
            : base(path)
        {
            Script = code.Script;
            GlobalDefinitions = code.GlobalDefinitions;
            Trigger = trigger;
            TriggerTags = triggerTags ?? Array.Empty<string>();
        }

        /// <summary>The JavaScript.</summary>
        public string Script { get; }

        /// <summary>The global definition area, or null to leave TIA Portal's own.</summary>
        public string? GlobalDefinitions { get; }

        /// <summary>The trigger.</summary>
        public string Trigger { get; }

        /// <summary>The tags that run the script; empty unless the trigger is <see cref="TagsTrigger"/>.</summary>
        public IReadOnlyList<string> TriggerTags { get; }

        /// <inheritdoc />
        public override IReadOnlyList<string> LinkedTags => TriggerTags;
    }
}
