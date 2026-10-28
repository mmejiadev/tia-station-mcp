namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>One script of a screen and where it is: the item, the event or animated property, and its global definitions.</summary>
    public sealed class UnifiedScriptSource
    {
        /// <summary>Creates a source.</summary>
        /// <param name="item">The screen item's name, or the screen's for one of its own events.</param>
        /// <param name="where">E.g. <c>event Down</c> or <c>animation Visible</c>.</param>
        /// <param name="script">The script.</param>
        /// <param name="globals">Its global definitions, or null when it has none of its own.</param>
        public UnifiedScriptSource(string item, string where, string script, string? globals)
        {
            Item = item;
            Where = where;
            Script = script;
            Globals = globals;
        }

        /// <summary>The screen item's name, or the screen's.</summary>
        public string Item { get; }

        /// <summary>The event or animated property the script belongs to.</summary>
        public string Where { get; }

        /// <summary>The script.</summary>
        public string Script { get; }

        /// <summary>Its global definitions, or null.</summary>
        public string? Globals { get; }
    }
}
