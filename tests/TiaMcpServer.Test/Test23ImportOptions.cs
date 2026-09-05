using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// These need no project and no TIA Portal, but they cannot leave this assembly: the option is
    /// an Openness enum, so a project that references it needs a licensed machine to build. That is
    /// the whole reason the conversion moved out of the MCP layer and down here.
    ///
    /// The options travel through these tests as strings, including the expected ones. Naming the
    /// enum in a signature puts it in the metadata VSTest reads while discovering tests, which
    /// happens before <c>AssemblyInitialize</c> has resolved Openness — the whole class is then
    /// silently skipped, which is how the first version of this file passed by not running.
    /// </remarks>
    [TestClass]
    public sealed class Test23ImportOptions
    {
        [TestMethod]
        public void Parse_EmptyOption_ReturnsOverride()
        {
            var option = ImportDocumentOption.Parse(string.Empty);

            Assert.AreEqual("Override", option.ToString());
        }

        [TestMethod]
        [DataRow("None", "None")]
        [DataRow("override", "Override")]
        [DataRow("SKIPINACTIVECULTURES", "SkipInactiveCultures")]
        public void Parse_EnumName_ReturnsThatOption(string given, string expected)
        {
            var option = ImportDocumentOption.Parse(given);

            Assert.AreEqual(expected, option.ToString());
        }

        /// <remarks>
        /// The aliases are inherited and kept on purpose: a caller writing "skipInactive" means one
        /// thing only. This is here so that removing them is a deliberate decision, not an accident.
        /// </remarks>
        [TestMethod]
        [DataRow("skipInactive", "SkipInactiveCultures")]
        [DataRow("activateInactive", "ActivateInactiveCultures")]
        public void Parse_KnownAlias_ReturnsThatOption(string given, string expected)
        {
            var option = ImportDocumentOption.Parse(given);

            Assert.AreEqual(expected, option.ToString());
        }

        /// <remarks>
        /// The category matters more than the refusal. A mistyped option is the caller's mistake,
        /// and reporting it as an operation failure tells them the environment broke and to try
        /// again — which is the one thing that will not help.
        /// </remarks>
        [TestMethod]
        public void Parse_UnknownWord_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => ImportDocumentOption.Parse("overwrite"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "overwrite");
        }

        [TestMethod]
        public void Validate_UnknownWord_ThrowsBeforeAnythingIsPlanned()
        {
            Assert.ThrowsException<PortalException>(() => ImportDocumentOption.Validate("overwrite"));
        }
    }
}
