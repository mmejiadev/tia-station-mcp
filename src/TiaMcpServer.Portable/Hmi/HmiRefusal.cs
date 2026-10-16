using TiaMcpServer.Siemens;

namespace TiaMcpServer.Hmi
{
    /// <summary>The refusals a screen description gets, all naming the item they are about.</summary>
    internal static class HmiRefusal
    {
        /// <summary>A refusal of bad input about one item.</summary>
        /// <param name="where">The item, as <c>Item 3 ('Start')</c>.</param>
        /// <param name="reason">What is wrong and how to fix it.</param>
        /// <returns>The exception, for the caller to throw.</returns>
        public static PortalException Refuse(string where, string reason)
        {
            return new PortalException(PortalErrorCode.InvalidParams, $"{where}: {reason}");
        }

        /// <summary>A required text field, trimmed.</summary>
        /// <param name="value">The field's value.</param>
        /// <param name="where">The item.</param>
        /// <param name="field">The field's JSON name.</param>
        /// <returns>The value.</returns>
        /// <exception cref="PortalException">The field is missing or blank.</exception>
        public static string RequireText(string? value, string where, string field)
        {
            return string.IsNullOrWhiteSpace(value) ? throw Refuse(where, $"'{field}' is required") : value!.Trim();
        }
    }
}
