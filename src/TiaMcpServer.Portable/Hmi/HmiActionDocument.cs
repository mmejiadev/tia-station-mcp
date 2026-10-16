using System.Text.Json.Serialization;

namespace TiaMcpServer.Hmi
{
    /// <summary>One function an event of an item runs, as its JSON has it.</summary>
    internal sealed class HmiActionDocument
    {
        [JsonPropertyName("event")]
        public string? Event { get; set; }

        [JsonPropertyName("function")]
        public string? Function { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("value")]
        public double? Value { get; set; }

        [JsonPropertyName("screen")]
        public string? Screen { get; set; }
    }
}
