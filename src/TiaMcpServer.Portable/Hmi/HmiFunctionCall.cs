using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A system function an event runs.</summary>
    /// <remarks>
    /// Only the functions measured in the class projects' exports, with the parameters exactly as they
    /// were written there (2026-10-13): <c>SetBitWhileKeyPressed</c>, <c>SetTag</c>,
    /// <c>ActivateScreen</c>, <c>ActivatePreviousScreen</c> and <c>ActivateScreenByNumber</c>; and
    /// <c>SetBit</c> and <c>ResetBit</c> (2026-10-23, from a button of 0967-HMI_PROCÉS). Exports read from
    /// TIA Portal V20; the same names and parameters are to be confirmed on V21. Another
    /// function is left out until its parameters have been read from an export: nothing here could
    /// tell a parameter spelled wrong from a right one.
    /// </remarks>
    public sealed class HmiFunctionCall
    {
        /// <summary>The parameter that names the tag a function writes.</summary>
        public const string TagParameter = "Tag";

        /// <summary>The parameter of <c>ActivateScreen</c> that names the screen to open.</summary>
        public const string ScreenParameter = "Screen name";

        private const string ObjectNumberParameter = "Object number";
        private const string IntegerType = "System.Int32";

        private HmiFunctionCall(string name, IReadOnlyList<HmiFunctionArgument> arguments)
        {
            Name = name;
            Arguments = arguments;
        }

        /// <summary>The system function's name.</summary>
        public string Name { get; }

        /// <summary>Its parameters, in the order an export lists them.</summary>
        public IReadOnlyList<HmiFunctionArgument> Arguments { get; }

        /// <summary>Holds bit 0 of a tag at 1 while the button is held: the class start and stop buttons.</summary>
        /// <param name="tag">The HMI tag.</param>
        /// <returns>The call.</returns>
        public static HmiFunctionCall SetBitWhileKeyPressed(string tag)
        {
            return new HmiFunctionCall(
                "SetBitWhileKeyPressed",
                new[] { HmiFunctionArgument.Link(TagParameter, tag), HmiFunctionArgument.Constant("Bit", "0", IntegerType) });
        }

        /// <summary>Sets a Bool tag to 1: on press, with <see cref="ResetBit"/> on release, a jog button.</summary>
        /// <param name="tag">The HMI tag.</param>
        /// <returns>The call.</returns>
        public static HmiFunctionCall SetBit(string tag)
        {
            return new HmiFunctionCall("SetBit", new[] { HmiFunctionArgument.Link(TagParameter, tag) });
        }

        /// <summary>Sets a Bool tag to 0.</summary>
        /// <param name="tag">The HMI tag.</param>
        /// <returns>The call.</returns>
        public static HmiFunctionCall ResetBit(string tag)
        {
            return new HmiFunctionCall("ResetBit", new[] { HmiFunctionArgument.Link(TagParameter, tag) });
        }

        /// <summary>Writes a value into a tag: the class counter reset button.</summary>
        /// <param name="tag">The HMI tag.</param>
        /// <param name="value">The value to write.</param>
        /// <returns>The call.</returns>
        public static HmiFunctionCall SetTag(string tag, double value)
        {
            return new HmiFunctionCall(
                "SetTag",
                new[] { HmiFunctionArgument.Link(TagParameter, tag), HmiFunctionArgument.Constant("Value", HmiScreenWriter.Number(value), "System.Double") });
        }

        /// <summary>Opens another screen by name: screen navigation.</summary>
        /// <param name="screen">The screen's name.</param>
        /// <returns>The call.</returns>
        public static HmiFunctionCall ActivateScreen(string screen)
        {
            return new HmiFunctionCall(
                "ActivateScreen",
                new[] { HmiFunctionArgument.Link(ScreenParameter, screen), HmiFunctionArgument.Constant(ObjectNumberParameter, "0", IntegerType) });
        }

        /// <summary>Opens the screen shown before this one: a back button.</summary>
        /// <returns>The call.</returns>
        public static HmiFunctionCall ActivatePreviousScreen()
        {
            return new HmiFunctionCall("ActivatePreviousScreen", System.Array.Empty<HmiFunctionArgument>());
        }

        /// <summary>Opens the screen whose number a tag holds: a screen selector.</summary>
        /// <param name="tag">The HMI tag holding the screen number.</param>
        /// <returns>The call.</returns>
        public static HmiFunctionCall ActivateScreenByNumber(string tag)
        {
            return new HmiFunctionCall(
                "ActivateScreenByNumber",
                new[] { HmiFunctionArgument.Link("Screen number", tag), HmiFunctionArgument.Constant(ObjectNumberParameter, "0", IntegerType) });
        }

        /// <summary>Writes the call as one entry of an event's function list.</summary>
        /// <param name="writer">The screen's writer.</param>
        /// <returns>The <c>FunctionListEntries</c> object.</returns>
        internal XElement WriteEntry(HmiScreenWriter writer)
        {
            return writer.Node("Hmi.Event.FunctionListEntry", "FunctionListEntries", () => new[]
            {
                HmiScreenWriter.Attributes(new Dictionary<string, string> { ["Name"] = Name, ["Type"] = "SystemFunction" }),
                HmiScreenWriter.Objects(Arguments.Select(argument => WriteArgument(writer, argument)).ToList())
            });
        }

        private static XElement WriteArgument(HmiScreenWriter writer, HmiFunctionArgument argument)
        {
            return writer.Node("Hmi.Event.FunctionListEntryParameter", "Parameters", () => argument.LinkedName.Length > 0
                ? new[] { HmiScreenWriter.Attributes(new Dictionary<string, string> { ["Name"] = argument.Name }), HmiScreenWriter.Link("Value", argument.LinkedName) }
                : new[]
                {
                    new XElement(
                        "AttributeList",
                        new XElement("Name", argument.Name),
                        new XElement("Value", new XAttribute("Type", argument.ValueType), argument.Value))
                });
        }
    }
}
