using System.Collections.Generic;

namespace TiaMcpServer.Hmi
{
    /// <summary>
    /// The attributes TIA Portal gives a new screen item, per kind, read from the class projects.
    /// </summary>
    /// <remarks>
    /// Written out in full rather than left to the import's defaults, so that a generated screen
    /// exported again reads attribute for attribute like a screen drawn by hand. Whether the import
    /// would choose the same defaults on its own has not been measured; writing them makes the
    /// question irrelevant.
    ///
    /// Source, 2026-10-13: the nine buttons of <c>0967 hmi_prova1</c> and the teacher's
    /// <c>0967 - explicació clase</c> are identical, so the button style is TIA's own. The I/O field
    /// and text field are the values most of their kind share, since the teacher recoloured some.
    /// The bar is the first of <c>hmi_prova1</c>'s two. Name, box and tab index are set per item and
    /// are absent here.
    ///
    /// Each call returns a new dictionary, because an item fills its own in.
    /// </remarks>
    public static class HmiItemStyle
    {
        /// <summary>The font size of every item kind in the class projects but the recoloured ones.</summary>
        public const int FontSize = 13;

        /// <summary>The font size of a bar's scale in the class projects.</summary>
        public const int BarFontSize = 11;

        /// <summary>The font of the class date and time fields.</summary>
        public const int DateTimeFontSize = 16;

        /// <summary>The style of the class date and time fields' font.</summary>
        public const string DateTimeFontStyle = "Regular";

        /// <summary>The font style every new item gets.</summary>
        public const string FontStyle = "Bold";

        /// <summary>A text button.</summary>
        /// <returns>Its attributes.</returns>
        public static Dictionary<string, string> Button()
        {
            return new Dictionary<string, string>
            {
                ["BackColor"] = "99, 101, 113", ["BackFillStyle"] = "GradientVertical", ["BitNumber"] = "0",
                ["BorderBackColor"] = "105, 105, 105", ["BorderColor"] = "71, 73, 87", ["BorderWidth"] = "2",
                ["CornerRadius"] = "3", ["CornerStyle"] = "Pointed", ["EdgeStyle"] = "Solid", ["Enabled"] = "true",
                ["FirstGradientColor"] = "131, 132, 142", ["FirstGradientOffset"] = "15", ["FitToLargest"] = "false",
                ["Flashing"] = "None", ["FocusColor"] = "148, 182, 231", ["FocusWidth"] = "2", ["ForeColor"] = "255, 255, 255",
                ["HorizontalAlignment"] = "Center", ["HorizontalPictureAlignment"] = "Center", ["HotKey"] = "0",
                ["MiddleGradientColor"] = "99, 101, 113", ["Mode"] = "Text",
                ["PictureAreaBottomMargin"] = "0", ["PictureAreaLeftMargin"] = "0", ["PictureAreaRightMargin"] = "0", ["PictureAreaTopMargin"] = "0",
                ["PictureAutoSizing"] = "StretchPicture", ["SecondGradientColor"] = "88, 90, 103", ["SecondGradientOffset"] = "15",
                ["TextAreaBottomMargin"] = "0", ["TextAreaLeftMargin"] = "0", ["TextAreaRightMargin"] = "0", ["TextAreaTopMargin"] = "0",
                ["TextOrientation"] = "Horizontal", ["UseDesignColorSchema"] = "false", ["UseFirstGradient"] = "true",
                ["UseSecondGradient"] = "true", ["UseTwoHandOperation"] = "false", ["VerticalAlignment"] = "Middle",
                ["VerticalPictureAlignment"] = "Middle"
            };
        }

        /// <summary>An I/O field showing a decimal number.</summary>
        /// <returns>Its attributes.</returns>
        public static Dictionary<string, string> IoField()
        {
            return new Dictionary<string, string>
            {
                ["AboveUpperLimitColor"] = "237, 88, 97", ["BackColor"] = "255, 255, 255", ["BackFillStyle"] = "Solid",
                ["BelowLowerLimitColor"] = "241, 161, 44", ["BorderBackColor"] = "101, 103, 115", ["BorderColor"] = "71, 73, 87",
                ["BorderWidth"] = "4", ["BottomMargin"] = "2", ["CornerRadius"] = "3", ["DataFormat"] = "Decimal",
                ["EdgeStyle"] = "Double", ["Enabled"] = "true", ["FitToLargest"] = "false", ["Flashing"] = "None",
                ["ForeColor"] = "49, 52, 74", ["HiddenInput"] = "false", ["HorizontalAlignment"] = "Left", ["LeftMargin"] = "3",
                ["RightMargin"] = "2", ["ShiftDecimalPoint"] = "0", ["ShowLeadingZeros"] = "false", ["TextOrientation"] = "Horizontal",
                ["TopMargin"] = "2", ["Unit"] = string.Empty, ["UseDesignColorSchema"] = "false", ["UseTwoHandOperation"] = "false",
                ["VerticalAlignment"] = "Middle"
            };
        }

        /// <summary>A symbolic I/O field: a text from a text list, chosen by a tag's value.</summary>
        /// <returns>Its attributes.</returns>
        /// <remarks>
        /// From the one symbolic field of the class projects (<c>hmi_prova1</c>, screen overview); its
        /// border colours and width differ from the I/O field's and are kept as measured.
        /// </remarks>
        public static Dictionary<string, string> SymbolicIoField()
        {
            return new Dictionary<string, string>
            {
                ["AboveUpperLimitColor"] = "237, 88, 97", ["BackColor"] = "255, 255, 255", ["BackFillStyle"] = "Solid",
                ["BelowLowerLimitColor"] = "241, 161, 44", ["BitNumber"] = "0", ["BorderBackColor"] = "226, 225, 225",
                ["BorderColor"] = "156, 154, 165", ["BorderWidth"] = "1", ["BottomMargin"] = "2", ["CornerRadius"] = "3",
                ["CountVisibleItems"] = "3", ["EdgeStyle"] = "Solid", ["Enabled"] = "true", ["EvenRowBackColor"] = "230, 230, 232",
                ["FitToLargest"] = "false", ["Flashing"] = "None", ["FlashingOnLimitViolation"] = "false", ["ForeColor"] = "49, 52, 74",
                ["HorizontalAlignment"] = "Left", ["LeftMargin"] = "3", ["OnValue"] = "1", ["RightMargin"] = "2",
                ["SelectBackColor"] = "162, 204, 213", ["SelectForeColor"] = "255, 255, 255", ["ShowDropDownButton"] = "true",
                ["ShowDropDownList"] = "true", ["TextOrientation"] = "Horizontal", ["TopMargin"] = "2",
                ["UseDesignColorSchema"] = "false", ["VerticalAlignment"] = "Middle"
            };
        }

        /// <summary>A field showing the panel's own date or time.</summary>
        /// <returns>Its attributes, without what to show, which the item sets.</returns>
        /// <remarks>From the two date and time fields of <c>hmi_prova1</c>'s screen overview, identical but for that.</remarks>
        public static Dictionary<string, string> DateTimeField()
        {
            return new Dictionary<string, string>
            {
                ["BackColor"] = "255, 255, 255", ["BackFillStyle"] = "Transparent", ["BorderBackColor"] = "226, 225, 225",
                ["BorderColor"] = "156, 154, 165", ["BorderWidth"] = "0", ["BottomMargin"] = "2", ["CornerRadius"] = "3",
                ["DisplaySystemTime"] = "true", ["EdgeStyle"] = "Double", ["Enabled"] = "true", ["FitToLargest"] = "false",
                ["Flashing"] = "None", ["ForeColor"] = "0, 0, 0", ["HorizontalAlignment"] = "Right", ["LeftMargin"] = "3",
                ["LongDateTimeFormat"] = "false", ["Mode"] = "Output", ["RightMargin"] = "2", ["TextOrientation"] = "Horizontal",
                ["TopMargin"] = "2", ["UseDesignColorSchema"] = "false", ["UseTwoHandOperation"] = "false", ["VerticalAlignment"] = "Middle"
            };
        }

        /// <summary>A static text field.</summary>
        /// <returns>Its attributes.</returns>
        public static Dictionary<string, string> TextField()
        {
            return new Dictionary<string, string>
            {
                ["BackColor"] = "255, 255, 255", ["BackFillStyle"] = "Transparent", ["BorderBackColor"] = "101, 103, 115",
                ["BorderColor"] = "71, 73, 87", ["BorderWidth"] = "0", ["BottomMargin"] = "2", ["CornerRadius"] = "3",
                ["EdgeStyle"] = "Double", ["FitToLargest"] = "true", ["Flashing"] = "None", ["ForeColor"] = "49, 52, 74",
                ["HorizontalAlignment"] = "Left", ["LeftMargin"] = "3", ["RightMargin"] = "2", ["TextOrientation"] = "Horizontal",
                ["TopMargin"] = "2", ["UseDesignColorSchema"] = "false", ["VerticalAlignment"] = "Middle"
            };
        }

        /// <summary>A circle, which the class projects use as a pilot light.</summary>
        /// <returns>Its attributes.</returns>
        public static Dictionary<string, string> Circle()
        {
            return new Dictionary<string, string>
            {
                ["BackFillStyle"] = "Solid", ["BorderColor"] = "24, 28, 49", ["BorderWidth"] = "1", ["EdgeStyle"] = "Solid",
                ["Flashing"] = "None", ["UseDesignColorSchema"] = "false"
            };
        }

        /// <summary>A rectangle.</summary>
        /// <returns>Its attributes.</returns>
        /// <remarks>From <c>0967-Exercici_1</c>'s screen <c>principal</c>, 2026-10-21; the corners are square.</remarks>
        public static Dictionary<string, string> Rectangle()
        {
            return new Dictionary<string, string>
            {
                ["BackColor"] = "217, 217, 217", ["BackFillStyle"] = "Solid", ["BorderColor"] = "24, 28, 49", ["BorderWidth"] = "1",
                ["EdgeStyle"] = "Solid", ["Flashing"] = "None", ["RoundCornerHeight"] = "0", ["RoundCornerWidth"] = "0",
                ["UseDesignColorSchema"] = "false"
            };
        }

        /// <summary>A straight line, without arrow heads.</summary>
        /// <returns>Its attributes, without the end points, which the item sets.</returns>
        /// <remarks>From <c>0967-Prova_0</c>'s screen <c>pantalla principal</c>, 2026-10-21.</remarks>
        public static Dictionary<string, string> Line()
        {
            return new Dictionary<string, string>
            {
                ["BackColor"] = "255, 255, 255", ["Color"] = "24, 28, 49", ["EndStyle"] = "NoEnd", ["FillStyle"] = "Transparent",
                ["Flashing"] = "None", ["LineEndShapeStyle"] = "Round", ["LineWidth"] = "1", ["StartStyle"] = "NoEnd",
                ["Style"] = "Solid", ["UseDesignColorSchema"] = "false"
            };
        }

        /// <summary>A graphic view showing one of the project's pictures, stretched to its box.</summary>
        /// <returns>Its attributes.</returns>
        /// <remarks>From <c>0967-HMI_PROCÉS</c>'s screen <c>Principal</c>, which has 62 of them, 2026-10-21.</remarks>
        public static Dictionary<string, string> GraphicView()
        {
            return new Dictionary<string, string>
            {
                ["AutoSizing"] = "StretchPicture", ["BackColor"] = "173, 174, 181", ["BackFillStyle"] = "Transparent",
                ["BorderColor"] = "0, 0, 0", ["BorderWidth"] = "0", ["EdgeStyle"] = "Solid", ["FitToLargest"] = "false",
                ["Flashing"] = "None", ["TransparentColor"] = "255, 0, 255", ["UseTransparentColor"] = "false"
            };
        }

        /// <summary>A vertical bar with a scale.</summary>
        /// <returns>Its attributes.</returns>
        public static Dictionary<string, string> Bar()
        {
            return new Dictionary<string, string>
            {
                ["BackColor"] = "241, 241, 242", ["BarBackColor"] = "0, 255, 255", ["BarEdgeStyle"] = "Solid", ["BarOrientation"] = "Up",
                ["BorderBackColor"] = "101, 103, 115", ["BorderColor"] = "71, 73, 87", ["BorderWidth"] = "7", ["CompatibilityMode"] = "false",
                ["CornerRadius"] = "4", ["CountSubDivisions"] = "5", ["EdgeStyle"] = "Double", ["Flashing"] = "None",
                ["FlashingOnLimitViolation"] = "false", ["ForeColor"] = "0, 122, 204", ["IntegerDigits"] = "4",
                ["LargeTickLabelingStep"] = "2", ["Precision"] = "0",
                ["RangeLower1Color"] = "241, 161, 44", ["RangeLower1Enabled"] = "true", ["RangeLower2Color"] = "241, 161, 44",
                ["RangeLower2Enabled"] = "true", ["RangeNormalColor"] = "56, 195, 70", ["RangeNormalEnabled"] = "true",
                ["RangeUpper1Color"] = "241, 161, 44", ["RangeUpper1Enabled"] = "true", ["RangeUpper2Color"] = "237, 88, 97",
                ["RangeUpper2Enabled"] = "true", ["ScaleColor"] = "49, 52, 74", ["ScaleGradation"] = "10",
                ["ScaleLabelingDoubleLined"] = "false", ["ScalePosition"] = "LeftUp", ["SegmentColoring"] = "Entire",
                ["ShowLimitLines"] = "false", ["ShowLimitMarkers"] = "true", ["ShowLimitRanges"] = "false",
                ["ShowProcessValue"] = "true", ["ShowScale"] = "true", ["ShowTickLabels"] = "true", ["Unit"] = string.Empty,
                ["UseAutoScaling"] = "false", ["UseDesignColorSchema"] = "false", ["UseExponentialFormat"] = "false"
            };
        }
    }
}
