using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Reaching TIA Portal without holding the Openness gate is refused.
    /// </summary>
    /// <remarks>
    /// The gate would be a convention without this, and a convention rots: a tool that forgot to take
    /// it would work perfectly until the day a job happened to be running at the same time, and the
    /// failure would be a damaged project rather than an exception. `McpServer.Portal` is the single
    /// point every tool passes through, so it is the only place the omission can be caught.
    ///
    /// It lives in this assembly because it touches types that reference Openness, not because it
    /// needs TIA Portal: the refusal happens before any portal is resolved.
    /// </remarks>
    [TestClass]
    public sealed class Test18GateEnforcement
    {
        [TestMethod]
        public void Portal_WithoutTheGate_IsRefusedBeforeAnyPortalIsResolved()
        {
            Assert.IsFalse(OpennessGate.IsHeldByCurrentThread, "this test must start outside the gate");

            var exception = Assert.ThrowsException<PortalException>(() => _ = McpServer.Portal);

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
            StringAssert.Contains(exception.Message, "without holding the Openness gate", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Portal_InsideTheGate_IsHandedOut()
        {
            // The other half: the check must not refuse the legitimate path, or every tool breaks.
            using (OpennessGate.Enter())
            {
                Assert.IsNotNull(McpServer.Portal);
            }
        }
    }
}
