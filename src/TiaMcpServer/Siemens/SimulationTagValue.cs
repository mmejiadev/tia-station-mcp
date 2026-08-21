using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>A value read from, or written to, a tag of a virtual controller.</summary>
    public sealed class SimulationTagValue
    {
        /// <summary>Creates a tag value.</summary>
        /// <param name="name">The tag name the value belongs to.</param>
        /// <param name="dataType">The declared PLC data type the value was read as.</param>
        /// <param name="value">The value, as a boxed bool or number.</param>
        public SimulationTagValue(string name, string dataType, object value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>The tag name the value belongs to.</summary>
        public string Name { get; }

        /// <summary>The declared PLC data type the value was read as.</summary>
        public string DataType { get; }

        /// <summary>
        /// The value, boxed. A bool stays a bool and a number stays a number, so it serialises as
        /// <c>true</c> or <c>17</c> rather than as <c>"true"</c> — a caller comparing a step count
        /// should not have to parse a string first.
        /// </summary>
        /// <remarks>
        /// One exception, and it is the honest one: a WChar arrives as a one-character string,
        /// because a character is not a number to anyone reading it. A Char is a number, because
        /// the PLCSIM API writes one as a signed byte and reporting it as a letter would not match
        /// what a write of the same tag accepts.
        /// </remarks>
        public object Value { get; }
    }
}
