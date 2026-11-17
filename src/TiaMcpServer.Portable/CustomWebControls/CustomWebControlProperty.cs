using System.Text.Json;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>One property a Custom Web Control offers on its interface, or one argument of an event it fires.</summary>
    public sealed class CustomWebControlProperty
    {
        /// <summary>Creates the property.</summary>
        /// <param name="name">Its name, as the script reads it: <c>WebCC.Properties.IsOn</c>.</param>
        /// <param name="kind">The kind of value it holds.</param>
        /// <param name="defaultValue">Its value until something is set or bound, already checked against <paramref name="kind"/>.</param>
        public CustomWebControlProperty(string name, CustomWebControlPropertyKind kind, JsonElement defaultValue)
        {
            Name = name;
            Kind = kind;
            Default = defaultValue;
        }

        /// <summary>Its name.</summary>
        public string Name { get; }

        /// <summary>The kind of value it holds.</summary>
        public CustomWebControlPropertyKind Kind { get; }

        /// <summary>Its default, as JSON: a truth value, a text, a number, or for a colour the ARGB number.</summary>
        public JsonElement Default { get; }
    }
}
