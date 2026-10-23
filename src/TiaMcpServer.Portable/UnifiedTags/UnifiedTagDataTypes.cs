using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>The data types an internal WinCC Unified HMI tag takes.</summary>
    /// <remarks>
    /// Measured 2026-10-22 on TIA Portal V20, one internal tag per type on an MTP700: these were taken
    /// and read back as written. Char, String, Date, Time, Date_And_Time, DTL and the .NET spellings
    /// (Boolean, Integer, Float, Double) were refused with "Empty data type or HMI data type", so they
    /// are refused here, before a tag exists. TIA Portal takes the spelling in any case.
    /// </remarks>
    public static class UnifiedTagDataTypes
    {
        private static readonly string[] Measured =
        {
            "Bool", "Byte", "SInt", "USInt", "Int", "UInt", "DInt", "UDInt", "LInt", "ULInt", "Real", "LReal",
            "Word", "DWord", "LWord", "WChar", "WString", "DateTime", "LTime"
        };

        /// <summary>Every data type an internal tag takes.</summary>
        public static IReadOnlyList<string> All => Measured;

        /// <summary>The data type as TIA Portal spells it.</summary>
        /// <param name="dataType">The data type, in any case.</param>
        /// <param name="what">The tag, as a refusal names it.</param>
        /// <returns>The measured spelling, e.g. <c>Bool</c> for <c>bool</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> listing the types there are.</exception>
        public static string Normalise(string? dataType, string what)
        {
            var given = (dataType ?? string.Empty).Trim();

            return Measured.FirstOrDefault(type => type.Equals(given, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"{what}: '{given}' is not a data type an internal Unified tag takes. Use one of: {string.Join(", ", Measured)}");
        }
    }
}
