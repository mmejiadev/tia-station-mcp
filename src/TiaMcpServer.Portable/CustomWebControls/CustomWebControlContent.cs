namespace TiaMcpServer.CustomWebControls
{
    /// <summary>What a Custom Web Control looks like and does: its markup, its style, its script, and its size.</summary>
    /// <remarks>
    /// The script defines <c>render(props)</c>, called with the control's properties once it starts and again on every
    /// change, and may define <c>setup()</c>, called once before. <see cref="CustomWebControlPageWriter"/> wraps the three
    /// into the page TIA Portal loads, with WebCC's start-up and two helpers: <c>TiaStation.color(argb)</c> turns a Unified
    /// colour into CSS, and <c>TiaStation.fire(name, ...args)</c> fires one of the control's events.
    /// </remarks>
    public sealed class CustomWebControlContent
    {
        /// <summary>Creates the content.</summary>
        /// <param name="html">The markup inside the page's body.</param>
        /// <param name="css">The style, without a <c>style</c> element.</param>
        /// <param name="script">The script, without a <c>script</c> element; it defines <c>render(props)</c>.</param>
        /// <param name="size">The size the control is placed at by default, in pixels.</param>
        public CustomWebControlContent(string html, string css, string script, (int Width, int Height) size)
        {
            Html = html;
            Css = css;
            Script = script;
            Width = size.Width;
            Height = size.Height;
        }

        /// <summary>The markup inside the page's body.</summary>
        public string Html { get; }

        /// <summary>The style.</summary>
        public string Css { get; }

        /// <summary>The script; it defines <c>render(props)</c>.</summary>
        public string Script { get; }

        /// <summary>The default width, in pixels.</summary>
        public int Width { get; }

        /// <summary>The default height, in pixels.</summary>
        public int Height { get; }
    }
}
