using System;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>What a Custom Web Control's names, size and content may be.</summary>
    /// <remarks>
    /// The control name is what TIA Portal places it by (measured 2026-11-15: <c>identity.name</c> works and the GUID does
    /// not), and the property and event names are read by the script as JavaScript identifiers, so both are kept to letters,
    /// digits, dots between parts and underscores. The script and style are written inside a <c>script</c> and a
    /// <c>style</c> element of one page, so a closing tag inside them would end that element early and run what follows as
    /// markup; such text is refused rather than escaped, as escaping it would change what the author wrote.
    /// </remarks>
    public static class CustomWebControlRules
    {
        /// <summary>The longest control name taken.</summary>
        public const int MaximumNameLength = 64;

        /// <summary>The smallest size, in pixels, a control is placed at by default.</summary>
        public const int MinimumSize = 16;

        /// <summary>The largest size, in pixels, a control is placed at by default.</summary>
        public const int MaximumSize = 4000;

        /// <summary>The longest markup, style or script taken, in characters.</summary>
        public const int MaximumContentLength = 200_000;

        /// <summary>The most properties a control may offer.</summary>
        public const int MaximumProperties = 64;

        /// <summary>The most events a control may fire.</summary>
        public const int MaximumEvents = 16;

        private static readonly Regex ControlName = new Regex(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$", RegexOptions.CultureInvariant);

        private static readonly Regex Identifier = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        /// <remarks>
        /// Names an object literal or JavaScript's prototypes treat specially: the page writes the properties as an object
        /// literal, where <c>__proto__</c> sets the prototype instead of declaring a property (review of 2026-11-15).
        /// </remarks>
        private static readonly string[] ReservedNames = { "__proto__", "constructor", "prototype", "hasOwnProperty", "toString", "valueOf", "__defineGetter__", "__defineSetter__" };

        /// <remarks>
        /// What would end the markup's place early, or swallow the helper and start-up scripts after it: an element whose
        /// content is raw text, and a comment (review of 2026-11-15). A control that needs a script writes it in script.
        /// </remarks>
        private static readonly string[] HtmlForbidden = { "<script", "</script", "<style", "</style", "<!--", "<textarea", "<plaintext", "<xmp", "<title", "<noembed", "<noframes", "<noscript", "<iframe", "</body", "</html" };

        /// <remarks>A closing tag ends the element early; an opening tag or a comment opener can stop the author's own closing one from closing it.</remarks>
        private static readonly string[] ScriptForbidden = { "</script", "<script", "<!--" };

        private static readonly string[] CssForbidden = { "</style", "<style", "<!--", "</script", "<script" };

        /// <remarks>
        /// What would load something from another machine or send something to one: an address with a scheme, a
        /// protocol-relative one, and the browser's network calls (security review of 2026-11-15). A page cannot be kept in
        /// its package by a Content-Security-Policy, which is not written (see <see cref="CustomWebControlPageWriter"/>), so
        /// this is checked in what is written instead. It stops a definition that reaches out plainly - an LLM steered by text
        /// in a project, a copied snippet - and not one built to hide it; that one still needs a policy that names the control
        /// (<c>custom-web-controls/&lt;name&gt;</c>) and leaves its fingerprint in the plan.
        /// </remarks>
        private static readonly string[] NetworkForbidden = { "://", "src=\"//", "src='//", "href=\"//", "href='//", "url(//", "url('//", "url(\"//", "@import", "fetch(", "xmlhttprequest", "websocket", "eventsource", "sendbeacon", "importscripts", "import(", "window.open", "location.href", "location.assign", "location.replace", "document.cookie", "localstorage", "indexeddb" };

        /// <summary>Refuses a control name that is not dotted letters and digits.</summary>
        /// <param name="name">E.g. <c>TiaStation.PilotLight</c>.</param>
        /// <returns>The name.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is blank, too long or not of that shape.</exception>
        public static string RequireControlName(string? name)
        {
            var trimmed = (name ?? string.Empty).Trim();

            return trimmed.Length <= MaximumNameLength && ControlName.IsMatch(trimmed)
                ? trimmed
                : throw new PortalException(PortalErrorCode.InvalidParams, $"Control name '{name}' must be letters and digits, parts separated by dots, e.g. 'Plant.Pump', at most {MaximumNameLength} characters");
        }

        /// <summary>Refuses a property, argument or event name the script could not read as an identifier.</summary>
        /// <param name="name">E.g. <c>IsOn</c>.</param>
        /// <param name="what">What it names, in a refusal.</param>
        /// <returns>The name.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is not an identifier.</exception>
        public static string RequireIdentifier(string? name, string what)
        {
            var trimmed = (name ?? string.Empty).Trim();

            if (Array.Exists(ReservedNames, reserved => string.Equals(reserved, trimmed, StringComparison.Ordinal)))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} '{name}' is a name JavaScript treats specially; choose another");
            }

            return Identifier.IsMatch(trimmed)
                ? trimmed
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} '{name}' must be a letter or underscore followed by letters, digits or underscores");
        }

        /// <summary>Refuses a default size outside what a screen holds.</summary>
        /// <param name="size">The size in pixels.</param>
        /// <param name="what">Width or height, in a refusal.</param>
        /// <returns>The size.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is out of range.</exception>
        public static int RequireSize(int size, string what)
        {
            return size >= MinimumSize && size <= MaximumSize
                ? size
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be from {MinimumSize} to {MaximumSize} pixels, not {size}");
        }

        /// <summary>Refuses markup too long, or holding what would end its place or swallow the scripts after it.</summary>
        /// <param name="text">The markup inside the body.</param>
        /// <returns>The text, never null.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what it holds.</exception>
        public static string RequireHtml(string? text)
        {
            return RequireContent(text, HtmlForbidden, "html");
        }

        /// <summary>Refuses a style too long, or holding a tag or comment opener.</summary>
        /// <param name="text">The style.</param>
        /// <returns>The text, never null.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what it holds.</exception>
        public static string RequireCss(string? text)
        {
            return RequireContent(text, CssForbidden, "css");
        }

        /// <summary>Refuses a script too long, or holding a script tag or comment opener.</summary>
        /// <param name="text">The script.</param>
        /// <returns>The text, never null.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what it holds.</exception>
        public static string RequireScript(string? text)
        {
            return RequireContent(text, ScriptForbidden, "script");
        }

        private static string RequireContent(string? text, string[] forbidden, string what)
        {
            var content = text ?? string.Empty;

            if (content.Length > MaximumContentLength)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} is {content.Length} characters; at most {MaximumContentLength} are taken");
            }

            var found = Array.Find(forbidden, tag => content.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0);
            if (found != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} holds '{found}', which would end its place in the page early or swallow what follows; write it another way (in a string, '<' + '/script')");
            }

            var reaching = Array.Find(NetworkForbidden, call => content.IndexOf(call, StringComparison.OrdinalIgnoreCase) >= 0);

            return reaching == null
                ? content
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} holds '{reaching}': a control draws from its own properties and package only, and reaches no other address, network call or browser storage");
        }

        /// <summary>Refuses a script that does not define the function the page calls.</summary>
        /// <param name="script">The script.</param>
        /// <returns>The script.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it has no <c>render</c>.</exception>
        public static string RequireRender(string script)
        {
            return Regex.IsMatch(script ?? string.Empty, @"\bfunction\s+render\s*\(|\brender\s*=\s*(function\b|\()", RegexOptions.CultureInvariant)
                ? script!
                : throw new PortalException(PortalErrorCode.InvalidParams, "script must define function render(props), which is called with the control's properties on start and on every change");
        }
    }
}
