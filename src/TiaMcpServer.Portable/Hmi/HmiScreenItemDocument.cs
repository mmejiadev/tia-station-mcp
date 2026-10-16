using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TiaMcpServer.Hmi
{
    /// <summary>One item of a screen description as its JSON has it, and nothing more.</summary>
    /// <remarks>
    /// Everything nullable, because a description can leave anything out; the item types refuse to
    /// exist incomplete, and <see cref="HmiScreenItemFactory"/> is what stands between the two. One
    /// flat shape for every kind keeps the description easy to write: a field that does not apply to
    /// a kind is simply not read.
    /// </remarks>
    internal sealed class HmiScreenItemDocument
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("left")]
        public int? Left { get; set; }

        [JsonPropertyName("top")]
        public int? Top { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("press")]
        public string? Press { get; set; }

        [JsonPropertyName("screen")]
        public string? Screen { get; set; }

        [JsonPropertyName("value")]
        public double? Value { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("offColor")]
        public string? OffColor { get; set; }

        [JsonPropertyName("onColor")]
        public string? OnColor { get; set; }

        [JsonPropertyName("flashing")]
        public bool? Flashing { get; set; }

        [JsonPropertyName("visibleTag")]
        public string? VisibleTag { get; set; }

        [JsonPropertyName("minimum")]
        public double? Minimum { get; set; }

        [JsonPropertyName("maximum")]
        public double? Maximum { get; set; }

        [JsonPropertyName("backColor")]
        public string? BackColor { get; set; }

        [JsonPropertyName("foreColor")]
        public string? ForeColor { get; set; }

        [JsonPropertyName("fontSize")]
        public int? FontSize { get; set; }

        [JsonPropertyName("fontStyle")]
        public string? FontStyle { get; set; }

        [JsonPropertyName("align")]
        public string? Align { get; set; }

        [JsonPropertyName("show")]
        public string? Show { get; set; }

        [JsonPropertyName("textList")]
        public string? TextList { get; set; }

        [JsonPropertyName("events")]
        public List<HmiActionDocument>? Events { get; set; }

        [JsonPropertyName("animations")]
        public List<HmiAnimationDocument>? Animations { get; set; }
    }
}
