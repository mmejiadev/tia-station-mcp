namespace TiaMcpServer.Hmi
{
    /// <summary>A pilot light's colour at 0 and at 1, and whether it flashes at 1.</summary>
    public sealed class HmiPilotLightColors
    {
        /// <summary>Creates the colours.</summary>
        /// <param name="offColor">Colour while the tag is 0, as <c>R, G, B</c>.</param>
        /// <param name="onColor">Colour while the tag is 1, as <c>R, G, B</c>.</param>
        /// <param name="flashesWhenOn">Whether it flashes while the tag is 1, as the class alarm light does.</param>
        public HmiPilotLightColors(string offColor, string onColor, bool flashesWhenOn)
        {
            OffColor = offColor;
            OnColor = onColor;
            FlashesWhenOn = flashesWhenOn;
        }

        /// <summary>Colour while the tag is 0.</summary>
        public string OffColor { get; }

        /// <summary>Colour while the tag is 1.</summary>
        public string OnColor { get; }

        /// <summary>Whether it flashes while the tag is 1.</summary>
        public bool FlashesWhenOn { get; }
    }
}
