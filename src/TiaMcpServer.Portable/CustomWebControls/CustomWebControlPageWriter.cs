using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Writes the page a Custom Web Control starts from, <c>control/index.html</c>.</summary>
    /// <remarks>
    /// WebCC's start-up as Siemens' samples use it and as a pilot light measured on 2026-11-15 ran it in TIA Portal's
    /// editor and in runtime: <c>WebCC.start(callback, { methods, events, properties }, ['HMI'], timeout)</c>, then
    /// <c>WebCC.Properties</c> read and <c>WebCC.onPropertyChanged</c> subscribed. The author's script only defines
    /// <c>render(props)</c>, and <c>setup()</c> if it wants one; the page calls them. Helpers: <c>TiaStation.color(argb)</c>
    /// turns a Unified colour into CSS, <c>TiaStation.bool(value)</c> and <c>TiaStation.number(value)</c> read a property
    /// whichever way the runtime hands it over (a text "true" was seen beside a true), and
    /// <c>TiaStation.fire(name, ...args)</c> fires an event.
    ///
    /// No Content-Security-Policy is declared. Measured 2026-11-15/16: controls whose page declared one restricted to
    /// <c>'self'</c> and that carried the WebCC copy of AuditViewer's <c>control/static</c> drew blank in TIA Portal's screen
    /// editor and in runtime; the same controls with neither drew. The two were changed together and not told apart, so
    /// the policy stays out until a control declaring it is seen to draw. What a page may reach is kept down by
    /// <see cref="CustomWebControlRules"/> instead, and by the install's policy target and the fingerprint in its plan.
    /// </remarks>
    public static class CustomWebControlPageWriter
    {
        /// <summary>Where the page loads WebCC from, inside the package.</summary>
        public const string LibraryPath = "libs/webcc.min.js";

        private const int StartTimeoutMilliseconds = 10000;

        private const string Helpers = @"var TiaStation = (function () {
    function color(argb) {
        var n = Number(argb) >>> 0;
        var a = ((n >>> 24) & 255) / 255, r = (n >>> 16) & 255, g = (n >>> 8) & 255, b = n & 255;
        return 'rgba(' + r + ',' + g + ',' + b + ',' + a.toFixed(3) + ')';
    }
    function bool(value) {
        return value === true || value === 1 || value === 'true' || value === 'True' || value === '1';
    }
    function number(value) {
        var n = Number(value);
        return isFinite(n) ? n : 0;
    }
    function fire(name) {
        var args = Array.prototype.slice.call(arguments, 1);
        WebCC.Events.fire.apply(WebCC.Events, [name].concat(args));
    }
    return { color: color, bool: bool, number: number, fire: fire };
})();";

        /// <summary>Writes the page.</summary>
        /// <param name="definition">The checked definition.</param>
        /// <returns>The page, UTF-8 without a byte order mark.</returns>
        public static byte[] Write(CustomWebControlDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var page = new StringBuilder();

            page.Append("<!doctype html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n");
            page.Append("<title>").Append(WebUtility.HtmlEncode(definition.Identity.DisplayName)).Append("</title>\n");
            page.Append("<script type=\"text/javascript\" src=\"").Append(LibraryPath).Append("\"></script>\n");
            page.Append("<style>\nhtml, body { margin: 0; width: 100%; height: 100%; overflow: hidden; background: transparent; }\n");
            page.Append(definition.Content.Css).Append("\n</style>\n</head>\n<body>\n");
            page.Append(definition.Content.Html).Append('\n');
            page.Append("<script>\n").Append(Helpers).Append("\n</script>\n");
            page.Append("<script>\n").Append(definition.Content.Script).Append("\n</script>\n");
            page.Append("<script>\n").Append(StartUp(definition)).Append("\n</script>\n");
            page.Append("</body>\n</html>\n");

            return new UTF8Encoding(false).GetBytes(page.ToString());
        }

        private static string StartUp(CustomWebControlDefinition definition)
        {
            return "(function () {\n"
                + "    var contract = " + Contract(definition) + ";\n"
                + "    function draw() { render(WebCC.Properties); }\n"
                + "    WebCC.start(function (started) {\n"
                + "        if (!started) { return; }\n"
                + "        if (typeof setup === 'function') { setup(); }\n"
                + "        draw();\n"
                + "        WebCC.onPropertyChanged.subscribe(draw);\n"
                + "    }, contract, ['HMI'], " + StartTimeoutMilliseconds + ");\n"
                + "})();";
        }

        private static string Contract(CustomWebControlDefinition definition)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteStartObject("methods");
                writer.WriteEndObject();
                writer.WriteStartArray("events");
                foreach (var fired in definition.Events)
                {
                    writer.WriteStringValue(fired.Name);
                }

                writer.WriteEndArray();
                writer.WriteStartObject("properties");
                foreach (var property in definition.Properties)
                {
                    writer.WritePropertyName(property.Name);
                    property.Default.WriteTo(writer);
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
