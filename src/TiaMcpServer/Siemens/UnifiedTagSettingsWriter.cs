using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.HmiTags;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Siemens
{
    /// <summary>Writes a WinCC Unified tag's settings once the tag exists, its link included.</summary>
    /// <remarks>
    /// Measured 2026-10-25 on TIA Portal V20. A linked tag's data type is filled in by TIA Portal from the
    /// PLC tag, so its values are checked here, against that type, rather than when the list is read. A
    /// limit and a substitute value are written as their kind first, then the value: the value is a
    /// disabled field until the kind is set. The scaling values are numbers; Openness refuses them as text.
    /// Thresholds cannot be written at all: <c>Thresholds.Create()</c> answers "New thresholds are not
    /// supported", on internal and linked tags alike.
    /// </remarks>
    internal sealed class UnifiedTagSettingsWriter
    {
        private readonly Language _language;

        /// <summary>Creates a writer.</summary>
        /// <param name="language">The project's editing language, the one a comment is written in.</param>
        public UnifiedTagSettingsWriter(Language language)
        {
            _language = language;
        }

        /// <summary>Writes every setting the description has.</summary>
        /// <param name="tag">The tag, already created and linked.</param>
        /// <param name="description">What was asked for.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when a linked tag's value does not fit the type TIA Portal gave it.</exception>
        public void Write(HmiTag tag, UnifiedTagDescription description)
        {
            var what = $"Tag '{description.Name}'";

            WriteOptions(tag, description.Settings.Options, what);
            WriteLimits(tag, description.Settings.Limits, what);
            WritePlcReading(tag, description.Settings.PlcReading, what);
        }

        private void WriteOptions(HmiTag tag, UnifiedTagOptions options, string what)
        {
            if (options.Comment != null)
            {
                var item = tag.Comment.Items.Find(_language)
                    ?? throw new PortalException(PortalErrorCode.InvalidState, $"{what}: the comment has no entry for the project's editing language {_language.Culture.Name}");
                item.Text = options.Comment;
            }

            if (options.InitialValue != null)
            {
                tag.InitialValue = UnifiedTagValues.Constant(tag.DataType, options.InitialValue, $"{what}, start value");
            }

            if (options.Persistent != null)
            {
                tag.Persistent = options.Persistent.Value;
            }

            if (options.MaxLength != null)
            {
                tag.MaxLength = options.MaxLength.Value;
            }
        }

        private static void WriteLimits(HmiTag tag, UnifiedTagLimits limits, string what)
        {
            if (!limits.IsSet)
            {
                return;
            }

            if (!UnifiedTagValues.IsNumeric(tag.DataType))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: only a number tag has limits; this one is a {tag.DataType}");
            }

            var lower = Checked(limits.Lower, tag.DataType, $"{what}, lower limit");
            var upper = Checked(limits.Upper, tag.DataType, $"{what}, upper limit");
            if (lower is { IsTag: false } && upper is { IsTag: false })
            {
                UnifiedTagValues.RequireOrdered(lower.Value, upper.Value, what);
            }

            WriteLimit(tag.InitialMinValue, lower);
            WriteLimit(tag.InitialMaxValue, upper);
        }

        private static UnifiedTagLimit? Checked(UnifiedTagLimit? limit, string dataType, string what)
        {
            return limit == null || limit.IsTag ? limit : UnifiedTagLimit.Constant(UnifiedTagValues.Constant(dataType, limit.Value, what));
        }

        private static void WriteLimit(Range range, UnifiedTagLimit? limit)
        {
            if (limit == null)
            {
                return;
            }

            range.ValueType = limit.IsTag ? HmiLimitValueType.Tag : HmiLimitValueType.Constant;
            range.Value = limit.Value;
        }

        private static void WritePlcReading(HmiTag tag, UnifiedTagPlcReading reading, string what)
        {
            if (reading.Acquisition != null)
            {
                tag.AcquisitionMode = ToOpenness(reading.Acquisition.Mode);
                if (reading.Acquisition.Cycle != null)
                {
                    tag.AcquisitionCycle = reading.Acquisition.Cycle;
                }
            }

            if (reading.Scaling != null)
            {
                if (!UnifiedTagValues.IsNumeric(tag.DataType))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: only a number tag is scaled; this one is a {tag.DataType}");
                }

                tag.LinearScaling = true;
                tag.PlcStartValue = reading.Scaling.Plc.Start;
                tag.PlcEndValue = reading.Scaling.Plc.End;
                tag.HmiStartValue = reading.Scaling.Hmi.Start;
                tag.HmiEndValue = reading.Scaling.Hmi.End;
            }

            if (reading.Substitute != null)
            {
                tag.SubstituteValue.SubstituteValueUsage = ToOpenness(reading.Substitute.Usage);
                tag.SubstituteValue.Value = UnifiedTagValues.Constant(tag.DataType, reading.Substitute.Value, $"{what}, substitute value");
            }
        }

        private static HmiAcquisitionMode ToOpenness(UnifiedAcquisitionMode mode)
        {
            return mode switch
            {
                UnifiedAcquisitionMode.CyclicOnUse => HmiAcquisitionMode.CyclicOnUse,
                UnifiedAcquisitionMode.OnDemand => HmiAcquisitionMode.OnDemand,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised acquisition mode: {mode}")
            };
        }

        private static HmiSubstituteValueUsage ToOpenness(UnifiedSubstituteUsage usage)
        {
            return usage switch
            {
                UnifiedSubstituteUsage.InvalidValue => HmiSubstituteValueUsage.InvalidValue,
                UnifiedSubstituteUsage.RangeViolation => HmiSubstituteValueUsage.RangeViolation,
                UnifiedSubstituteUsage.InvalidValueOrRangeViolation => HmiSubstituteValueUsage.InvalidValueOrRangeViolation,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised substitute value usage: {usage}")
            };
        }
    }
}
