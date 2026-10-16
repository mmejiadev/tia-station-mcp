namespace TiaMcpServer.Hmi
{
    /// <summary>One parameter of a system function: either a link by name or a plain value.</summary>
    public sealed class HmiFunctionArgument
    {
        private HmiFunctionArgument(string name, string linkedName, string value, string valueType)
        {
            Name = name;
            LinkedName = linkedName;
            Value = value;
            ValueType = valueType;
        }

        /// <summary>The parameter's name, as the system function spells it, for example <c>Screen name</c>.</summary>
        public string Name { get; }

        /// <summary>The tag or screen it links to, or empty when it is a plain value.</summary>
        public string LinkedName { get; }

        /// <summary>The plain value, or empty when it is a link.</summary>
        public string Value { get; }

        /// <summary>The .NET type TIA Portal records the value with, for example <c>System.Int32</c>.</summary>
        public string ValueType { get; }

        /// <summary>A parameter that names a tag or a screen.</summary>
        /// <param name="name">The parameter's name.</param>
        /// <param name="linkedName">The tag or screen.</param>
        /// <returns>The argument.</returns>
        public static HmiFunctionArgument Link(string name, string linkedName)
        {
            return new HmiFunctionArgument(name, linkedName, string.Empty, string.Empty);
        }

        /// <summary>A parameter that holds a value.</summary>
        /// <param name="name">The parameter's name.</param>
        /// <param name="value">The value, written invariantly.</param>
        /// <param name="valueType">The .NET type TIA Portal records it with.</param>
        /// <returns>The argument.</returns>
        public static HmiFunctionArgument Constant(string name, string value, string valueType)
        {
            return new HmiFunctionArgument(name, string.Empty, value, valueType);
        }
    }
}
