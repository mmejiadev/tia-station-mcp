using System.Text.Json.Serialization;

namespace TiaMcpServer.Hmi
{
    /// <summary>One range of an appearance animation as its JSON has it.</summary>
    internal sealed class HmiRangeDocument
    {
        [JsonPropertyName("from")]
        public double? From { get; set; }

        [JsonPropertyName("to")]
        public double? To { get; set; }

        [JsonPropertyName("backColor")]
        public string? BackColor { get; set; }

        [JsonPropertyName("foreColor")]
        public string? ForeColor { get; set; }

        [JsonPropertyName("flashing")]
        public bool? Flashing { get; set; }
    }
}
