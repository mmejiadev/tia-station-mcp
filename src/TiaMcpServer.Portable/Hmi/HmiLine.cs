using System;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A straight line from one point to another: a pipe, a separator.</summary>
    /// <remarks>
    /// TIA Portal writes a line twice over: its end points, and the box around them. Measured on the one
    /// line of the class projects (<c>0967-Prova_0</c>): from (174, 98) to (654, 93) the box is left 174,
    /// top 93, 480 by 5, so the box is the end points' bounding rectangle. A line drawn exactly along an
    /// axis has a box of size 0 across it: measured 2026-10-21, a horizontal line imported with height 1
    /// is refused ("the recommended value is 0") and TIA Portal ends the project session.
    /// </remarks>
    public sealed class HmiLine : HmiScreenItem
    {
        /// <summary>Creates a line.</summary>
        /// <param name="frame">Its name, look and animations; the box's position is the start point.</param>
        /// <param name="end">The end point, as a box whose position is the point; its size is unused.</param>
        /// <param name="lineWidth">The stroke, in pixels.</param>
        public HmiLine(HmiItemFrame frame, HmiBox end, int lineWidth)
            : base(frame)
        {
            End = end;
            LineWidth = lineWidth;
        }

        /// <summary>The end point.</summary>
        public HmiBox End { get; }

        /// <summary>The stroke, in pixels.</summary>
        public int LineWidth { get; }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var start = Frame.Box;
            var bounds = new HmiBox(
                Math.Min(start.Left, End.Left),
                Math.Min(start.Top, End.Top),
                Math.Abs(End.Left - start.Left),
                Math.Abs(End.Top - start.Top));

            var style = HmiItemStyle.Line();
            style["StartLeft"] = HmiScreenWriter.Number(start.Left);
            style["StartTop"] = HmiScreenWriter.Number(start.Top);
            style["EndLeft"] = HmiScreenWriter.Number(End.Left);
            style["EndTop"] = HmiScreenWriter.Number(End.Top);
            style["LineWidth"] = HmiScreenWriter.Number(LineWidth);

            if (Frame.Look.ForeColor.Length > 0)
            {
                style["Color"] = Frame.Look.ForeColor;
            }

            // The colour went into Color above: a line has no ForeColor attribute for Place to write.
            var look = new HmiLook(Frame.Look.BackColor, string.Empty, Frame.Look.Font, Frame.Look.HorizontalAlignment);
            var line = new HmiItemFrame(Frame.Name, bounds, look, Frame.Animations);

            return writer.Item("Hmi.Screen.Line", HmiScreenWriter.Place(style, line, HmiScreenWriter.NotFocusable), () => WriteAnimations(writer).ToList());
        }
    }
}
