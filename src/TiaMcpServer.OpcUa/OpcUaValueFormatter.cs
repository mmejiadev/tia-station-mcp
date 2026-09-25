using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Opc.Ua;

namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// Turns what the stack returned into an <see cref="OpcUaReading"/>.
    /// </summary>
    /// <remarks>
    /// Kept apart from the connection so it can be tested with values built in memory. This is the
    /// place where the bug this repository already had once would come back: this machine is
    /// es-ES, and <c>ToString()</c> on a double prints a half as <c>0,5</c>. A caller that copies
    /// that into anything expecting a number reads five. Everything numeric goes through the
    /// invariant culture.
    /// </remarks>
    internal static class OpcUaValueFormatter
    {
        private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        internal static OpcUaReading Format(string nodeId, DataValue dataValue)
        {
            var isGood = StatusCode.IsGood(dataValue.StatusCode);
            var value = isGood ? FormatValue(dataValue.Value) : string.Empty;
            var dataType = dataValue.WrappedValue.TypeInfo?.BuiltInType.ToString() ?? "Null";

            return new OpcUaReading(
                nodeId,
                value,
                dataType,
                DescribeStatus(dataValue.StatusCode),
                isGood,
                FormatTimestamp(dataValue.SourceTimestamp));
        }

        internal static string FormatValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                string text => text,
                bool flag => flag ? "true" : "false",
                DateTime moment => FormatTimestamp(moment),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                byte[] bytes => BitConverter.ToString(bytes),
                IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Select(FormatValue)) + "]",
                _ => value.ToString() ?? string.Empty
            };
        }

        internal static string DescribeStatus(StatusCode status)
        {
            // StatusCode.ToString() prints the symbolic name when the stack knows it, and the hex
            // code otherwise. The symbolic name is the part that tells a caller what to do:
            // BadNodeIdUnknown is a typo, BadNotReadable is a permission set on the CPU.
            return StatusCode.IsGood(status) ? "Good" : status.ToString();
        }

        private static string FormatTimestamp(DateTime moment)
        {
            return moment == DateTime.MinValue
                ? string.Empty
                : moment.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        }
    }
}
