using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TiaMcpServer.Hmi
{
    /// <summary>One animation of an item as its JSON has it: <c>appearance</c> with ranges, or <c>visibility</c>.</summary>
    internal sealed class HmiAnimationDocument
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("ranges")]
        public List<HmiRangeDocument>? Ranges { get; set; }

        [JsonPropertyName("bit")]
        public int? Bit { get; set; }

        [JsonPropertyName("visible")]
        public bool? Visible { get; set; }
    }
}
