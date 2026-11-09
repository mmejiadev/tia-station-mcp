using System;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Measured 2026-11-08 on TIA Portal V20.</remarks>
    [TestClass]
    public sealed class DeviceTemplateRulesTests
    {
        private static readonly string[] Group = { "DeviceUserGroup" };

        private static readonly string[] Block = { "PlcBlock" };

        private static readonly string[] Two = { "DeviceUserGroup", "DeviceUserGroup" };

        private static readonly string[] Nothing = Array.Empty<string>();

        private static readonly string[] OnSubnet = { "192.168.0.1", "192.168.0.2" };

        private static readonly string[] Placed = { "192.168.0.2", "192.168.0.9" };

        private static readonly string[] OtherAddresses = { "192.168.0.8", "192.168.0.9" };

        [TestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("Folder/Template")]
        public void CheckTemplateName_WhatCannotNameOne_IsRefused(string name)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => DeviceTemplateRules.CheckTemplateName(name));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void CheckTemplateName_AName_Passes()
        {
            DeviceTemplateRules.CheckTemplateName("Template_PLC_HMI");
        }

        [TestMethod]
        public void RequireDeviceGroup_ADeviceGroup_Passes()
        {
            DeviceTemplateRules.RequireDeviceGroup("T", Group);
        }

        [TestMethod]
        public void RequireDeviceGroup_AnythingElse_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => DeviceTemplateRules.RequireDeviceGroup("T", Block));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "PlcBlock", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireDeviceGroup_NothingOrMoreThanOne_IsRefused()
        {
            Assert.ThrowsException<PortalException>(() => DeviceTemplateRules.RequireDeviceGroup("T", Nothing));
            Assert.ThrowsException<PortalException>(() => DeviceTemplateRules.RequireDeviceGroup("T", Two));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(" ")]
        public void CheckSubnetName_ABlank_IsRefused(string name)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => DeviceTemplateRules.CheckSubnetName(name));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        /// <remarks>TIA Portal repeats a template's addresses on a second placement, without a word.</remarks>
        [TestMethod]
        public void CheckAddressesFree_AnAddressRepeated_IsRefusedAndNamed()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => DeviceTemplateRules.CheckAddressesFree("PN", OnSubnet, Placed));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "192.168.0.2", StringComparison.Ordinal);
            Assert.IsFalse(refusal.Message.Contains("192.168.0.9"), refusal.Message);
        }

        [TestMethod]
        public void CheckAddressesFree_OtherAddresses_Passes()
        {
            DeviceTemplateRules.CheckAddressesFree("PN", OnSubnet, OtherAddresses);
        }

        [TestMethod]
        public void CheckAddressesFree_AnEmptySubnet_Passes()
        {
            DeviceTemplateRules.CheckAddressesFree("PN", Nothing, Placed);
        }
    }
}
