using System;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// Read-only for now. Creating a virtual controller is a real side effect on the machine
    /// running the tests, so instance lifecycle is covered separately and deliberately.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test10Simulation
    {
        [TestMethod]
        public void IsAvailable_PlcSimInstalled_ReturnsTrue()
        {
            // Also proves the PLCSIM_AVAILABLE compilation constant was defined. Without it the
            // whole class compiles down to stubs that always report unavailable, and every other
            // simulation test would pass while testing nothing.
            Assert.IsTrue(
                SimulationRuntime.IsAvailable,
                "The PLCSIM Advanced runtime API was not linked, or its runtime is not registered");
        }

        [TestMethod]
        public void ListInstances_RuntimeAvailable_ReturnsAList()
        {
            using var runtime = new SimulationRuntime();

            var instances = runtime.ListInstances();

            Assert.IsNotNull(instances, "An empty result must be a list, not null");
        }

        [TestMethod]
        public void CreateInstance_EmptyName_ThrowsInvalidParams()
        {
            using var runtime = new SimulationRuntime();

            var exception = Assert.ThrowsException<PortalException>(() => runtime.CreateInstance("   "));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }

        [TestMethod]
        public void StartInstance_UnknownName_ThrowsNotFound()
        {
            using var runtime = new SimulationRuntime();

            var exception = Assert.ThrowsException<PortalException>(() => runtime.StartInstance("NoSuchSimulationInstance"));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }

        [TestMethod]
        public void CreateInstance_NewName_IsRegisteredAndCanBeRemoved()
        {
            // This test creates a real virtual controller on the machine running it. Create and
            // delete live in one test, not two, so a failure halfway still reaches the cleanup in
            // the finally block: a leaked instance keeps its name reserved and breaks later runs.
            //
            // RUN is deliberately not exercised here. A freshly created instance is powered on but
            // empty, and Run() on an empty controller fails with the runtime's -52 "IsEmpty".
            // Reaching RUN requires a downloaded program, so it belongs with the download tests.
            using var runtime = new SimulationRuntime();
            var instanceName = "TiaMcpServerTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            try
            {
                var created = runtime.CreateInstance(instanceName);

                Assert.AreEqual(instanceName, created.Name);
                Assert.IsTrue(
                    runtime.ListInstances().Any(instance => instance.Name == instanceName),
                    "The created instance is not listed");
            }
            finally
            {
                TryDelete(runtime, instanceName);
            }

            Assert.IsFalse(
                runtime.ListInstances().Any(instance => instance.Name == instanceName),
                "The instance is still registered after being deleted");
        }

        [TestMethod]
        public void StartInstance_EmptyController_ReportsTheRuntimeError()
        {
            // Documents the rule above as a test: an empty virtual PLC cannot be put into RUN, and
            // the failure must arrive as a PortalException rather than as the PLCSIM API's own
            // exception type leaking through the layer.
            using var runtime = new SimulationRuntime();
            var instanceName = "TiaMcpServerTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            try
            {
                runtime.CreateInstance(instanceName);

                var exception = Assert.ThrowsException<PortalException>(() => runtime.StartInstance(instanceName));

                Assert.AreEqual(PortalErrorCode.SimulationFailed, exception.Code);
            }
            finally
            {
                TryDelete(runtime, instanceName);
            }
        }

        private static void TryDelete(SimulationRuntime runtime, string instanceName)
        {
            try
            {
                runtime.DeleteInstance(instanceName);
            }
            catch (PortalException)
            {
                // Creating it may be what failed. Nothing to remove, and hiding the real failure
                // behind a cleanup error would waste the run.
            }
        }
    }
}
