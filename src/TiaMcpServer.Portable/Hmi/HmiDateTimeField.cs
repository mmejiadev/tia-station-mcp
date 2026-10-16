using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A field showing the panel's own clock: the time, the date, or both.</summary>
    public sealed class HmiDateTimeField : HmiScreenItem
    {
        /// <summary>Creates a date and time field.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        /// <param name="showsDate">Whether it shows the date.</param>
        /// <param name="showsTime">Whether it shows the time.</param>
        public HmiDateTimeField(HmiItemFrame frame, bool showsDate, bool showsTime)
            : base(frame)
        {
            ShowsDate = showsDate;
            ShowsTime = showsTime;
        }

        /// <summary>Whether it shows the date.</summary>
        public bool ShowsDate { get; }

        /// <summary>Whether it shows the time.</summary>
        public bool ShowsTime { get; }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var style = HmiItemStyle.DateTimeField();
            style["ShowDate"] = ShowsDate ? "true" : "false";
            style["ShowTime"] = ShowsTime ? "true" : "false";

            var placed = HmiScreenWriter.Place(style, Frame, HmiScreenWriter.NotFocusable);
            var font = ResolveFont(HmiItemStyle.DateTimeFontSize, HmiItemStyle.DateTimeFontStyle);

            return writer.Item("Hmi.Screen.DateTimeField", placed, () => new[] { writer.Font(font), writer.EmptyText("HelpText") }
                .Concat(WriteAnimations(writer))
                .ToList());
        }
    }
}
