using System;
using System.Collections.Generic;
using TiaMcpServer.Governance;

namespace TiaMcpServer.OpcUa.Test
{
    [TestClass]
    public class OpcUaAccessPolicyTests
    {
        [TestMethod]
        public void Decide_AnEndpointNoPolicyLists_IsRefused()
        {
            var access = new OpcUaAccessPolicy(ModeGate.ForStudy(), StudyPolicy("opcua/192.168.0.1:4840"));

            var decision = access.Decide(OpcUaEndpoint.Parse("opc.tcp://192.168.0.2:4840"));

            Assert.IsFalse(decision.IsAllowed);
        }

        [TestMethod]
        public void Decide_AListedEndpoint_IsAllowed()
        {
            var access = new OpcUaAccessPolicy(ModeGate.ForStudy(), StudyPolicy("opcua/192.168.0.1:4840"));

            var decision = access.Decide(OpcUaEndpoint.Parse("opc.tcp://192.168.0.1"));

            Assert.IsTrue(decision.IsAllowed);
        }

        [TestMethod]
        public void Decide_NoPolicyAtAll_RefusesEveryEndpoint()
        {
            var access = new OpcUaAccessPolicy(ModeGate.ForStudy(), WritePolicy.DenyEverything());

            var decision = access.Decide(OpcUaEndpoint.Parse("opc.tcp://localhost:4840"));

            Assert.IsFalse(decision.IsAllowed);
        }

        [TestMethod]
        public void Decide_AProgramWildcard_DoesNotReachAnOpcUaServer()
        {
            // "PLC_0/*" is a rule about a place in the project. It must not be read as a rule about
            // the network, which is why every OPC UA target carries its own prefix.
            var access = new OpcUaAccessPolicy(ModeGate.ForStudy(), StudyPolicy("PLC_0/*"));

            var decision = access.Decide(OpcUaEndpoint.Parse("opc.tcp://192.168.0.1:4840"));

            Assert.IsFalse(decision.IsAllowed);
        }

        private static WritePolicy StudyPolicy(string allowed)
        {
            return new WritePolicy(new Dictionary<OperationMode, ModeRules>
            {
                [OperationMode.Study] = new ModeRules(OperationMode.Study, new[] { allowed }, Array.Empty<string>())
            });
        }
    }
}
