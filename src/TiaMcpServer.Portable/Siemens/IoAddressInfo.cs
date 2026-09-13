namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One address range of a module: what kind it is, where it starts and how long it is.
    /// </summary>
    /// <remarks>
    /// The number a program actually writes. A tag bound to <c>%I0.0</c> means the first bit of
    /// whichever module starts at 0, so plugging a card and leaving its address to chance is how a
    /// program comes to read a different card than the one it was written for.
    ///
    /// A module usually has one input range and one output range; a card with only inputs has one.
    /// Diagnosis and substitute ranges exist too and are reported rather than hidden, because a
    /// range that is occupied by one of them is just as occupied.
    /// </remarks>
    public sealed class IoAddressInfo
    {
        /// <summary>Creates an address range description.</summary>
        /// <param name="modulePath">Full path of the module the range belongs to.</param>
        /// <param name="ioType">Input, Output, Diagnosis or Substitute, as Openness names them.</param>
        /// <param name="startAddress">The first byte of the range.</param>
        /// <param name="lengthInBits">
        /// How long the range is, **in bits**: measured on 2026-09-13, a 32-channel input card
        /// reports 32 and not 4. The unit appears nowhere in the signature, so a test against a
        /// real card asserts it -- reading bits as bytes would understate every range eightfold
        /// and every span printed beside it would be wrong.
        /// </param>
        public IoAddressInfo(string modulePath, string ioType, int startAddress, int lengthInBits)
        {
            ModulePath = modulePath;
            IoType = ioType;
            StartAddress = startAddress;
            LengthInBits = lengthInBits;
        }

        /// <summary>Full path of the module the range belongs to.</summary>
        public string ModulePath { get; }

        /// <summary>Input, Output, Diagnosis or Substitute.</summary>
        public string IoType { get; }

        /// <summary>The first byte of the range.</summary>
        public int StartAddress { get; }

        /// <summary>How long the range is, in bits.</summary>
        public int LengthInBits { get; }

        /// <summary>The last byte of the range, which is the one a neighbour must start after.</summary>
        public int EndAddress => StartAddress + ByteLength() - 1;

        /// <summary>The range as a program would write it, for example <c>%I0.0..%I3.7</c>.</summary>
        /// <remarks>
        /// Rendered rather than left as two numbers because the numbers are not what anybody
        /// compares: the question being asked is whether a new card fits before the next one, and
        /// the answer is read off the ends.
        /// </remarks>
        public string Span => $"{Prefix()}{StartAddress}.0..{Prefix()}{EndAddress}.7";

        private int ByteLength()
        {
            return LengthInBits <= 0 ? 1 : (LengthInBits + 7) / 8;
        }

        private string Prefix()
        {
            return IoType == "Output" ? "%Q" : "%I";
        }
    }
}
