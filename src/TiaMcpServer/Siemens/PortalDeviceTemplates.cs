using Microsoft.Extensions.Logging;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.Secrets;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// A PLC and an HMI with their integrated connection, as a device group template in a global library. See
    /// <see cref="DeviceTemplateRules"/>. A secret's value goes from the store to TIA Portal and nowhere else: it is
    /// never a parameter of anything here that is logged, recorded in <c>Exception.Data</c> or returned.
    /// </remarks>
    public partial class Portal
    {
        private const string EthernetSubnetType = "System:Subnet.Ethernet";

        private const string AddressAttribute = "Address";

        private const int MaxReportedDeviceErrors = 10;

        /// <summary>Makes a device group of the open project a template in a global library, its integrated connection included.</summary>
        /// <param name="groupName">The device group, made by hand with its connection.</param>
        /// <param name="globalLibraryPath">The global library file; it must not be open.</param>
        /// <param name="templateName">The template's name.</param>
        /// <param name="backupDirectory">Where the library is archived before it changes.</param>
        /// <returns><c>{ template, group, devices, library }</c>; and <c>warning</c> when the library could not be closed.</returns>
        /// <exception cref="PortalException">The group is missing or empty, the library already holds that name, or the library is open.</exception>
        public JsonObject CreateDeviceGroupTemplate(string groupName, string globalLibraryPath, string templateName, string backupDirectory)
        {
            _logger?.LogInformation("Making device group {Group} a template in {Library}...", groupName, globalLibraryPath);

            try
            {
                DeviceTemplateRules.CheckTemplateName(templateName);
                var group = RequireDeviceGroup(groupName);
                var devices = new JsonArray(group.Devices.Select(device => (JsonNode)device.Name).ToArray());

                var (_, warning) = GlobalLibraryAccess.UseForWriting(RequirePortal(), globalLibraryPath, backupDirectory, global =>
                {
                    MasterCopyRules.CheckNotPresent(templateName, global.MasterCopyFolder.MasterCopies.Select(held => held.Name), "The global library");
                    global.MasterCopyFolder.MasterCopies.Create(group).SetAttribute("Name", templateName);

                    return true;
                });

                return WithWarning(new JsonObject { ["template"] = templateName, ["group"] = group.Name, ["devices"] = devices, ["library"] = globalLibraryPath }, warning);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CreateDeviceGroupTemplate", PortalErrorCode.WriteFailed), "CreateDeviceGroupTemplate", globalLibraryPath, groupName ?? string.Empty);
            }
        }

        /// <summary>Brings a device group template into the open project, joins its devices to a subnet, protects its PLCs and compiles it.</summary>
        /// <param name="placement">The template, the subnet and the name of the secret.</param>
        /// <param name="secrets">Where the secret is kept.</param>
        /// <param name="backupDirectory">Where the network as it stood is recorded first: a subnet already there is joined, and so changed.</param>
        /// <returns>What was placed, under the names TIA Portal gave it, with each device's compilation.</returns>
        /// <exception cref="PortalException">See <see cref="DeviceTemplateRules"/>; or the template or the secret is missing. What fails leaves nothing in the project.</exception>
        public JsonObject CreateDevicesFromTemplate(DeviceTemplatePlacement placement, ISecretStore secrets, string backupDirectory)
        {
            _logger?.LogInformation("Placing template {Template} on subnet {Subnet}...", placement?.TemplateName, placement?.SubnetName);

            try
            {
                DeviceTemplateRules.CheckTemplateName(placement!.TemplateName);
                DeviceTemplateRules.CheckSubnetName(placement.SubnetName);
                RequireDeviceGroupsOfOpenProject();

                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: a subnet already in the project is joined, and so changed");
                }

                using var password = string.IsNullOrWhiteSpace(placement.PlcSecretName) ? null : secrets.Reveal(placement.PlcSecretName);
                var (placed, warning) = GlobalLibraryAccess.Use(RequirePortal(), placement.GlobalLibraryPath, global => PlaceTemplate(global, placement, password, backupDirectory));

                return WithWarning(placed, warning);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CreateDevicesFromTemplate", PortalErrorCode.WriteFailed), "CreateDevicesFromTemplate", placement?.GlobalLibraryPath ?? string.Empty, placement?.TemplateName ?? string.Empty);
            }
        }

        private DeviceUserGroupComposition RequireDeviceGroupsOfOpenProject()
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project first");
            }

            return CurrentProject!.DeviceGroups;
        }

        private DeviceUserGroup RequireDeviceGroup(string groupName)
        {
            var groups = RequireDeviceGroupsOfOpenProject();
            var group = groups.Find(groupName ?? string.Empty)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"The project has no device group '{groupName}'. It has: {string.Join(", ", groups.Select(held => held.Name))}");

            return group.Devices.Count > 0
                ? group
                : throw new PortalException(PortalErrorCode.InvalidState, $"The device group '{groupName}' is empty; a template of it would bring nothing");
        }

        /// <remarks>
        /// The network is recorded once the template is found and right before anything is placed: a call refused for a name
        /// or a secret that is not there leaves no backup protecting nothing.
        /// </remarks>
        private JsonObject PlaceTemplate(GlobalLibrary global, DeviceTemplatePlacement placement, SecureString? password, string backupDirectory)
        {
            var copy = MasterCopyReader.Find(global.MasterCopyFolder, placement.TemplateName)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"The global library has no template '{placement.TemplateName}'. It holds: {string.Join(", ", global.MasterCopyFolder.MasterCopies.Select(held => held.Name))}");

            var contents = MasterCopyReader.Contents(copy);
            DeviceTemplateRules.RequireDeviceGroup(copy.Name, contents.Select(content => content.TypeName).ToList());

            // Found before anything is placed, so a failure in looking leaves nothing to take back.
            var subnet = FindSubnet(placement.SubnetName);
            RecordNetworkBefore(backupDirectory);
            var placed = CurrentProject!.DeviceGroups.CreateFrom(copy);
            var made = new List<(string Name, Action Delete)> { (placed.Name, placed.Delete) };

            try
            {
                subnet ??= CreateSubnet(placement.SubnetName, made);
                var devices = AllDevices(placed);

                return DescribePlaced(placed, contents[0].Name, devices, (subnet.Name, JoinToSubnet(devices, subnet), ProtectPlcs(devices, password)));
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                // TIA Portal's own reason is inside its wrapper, and the undo names only what this call made.
                var reported = failure as PortalException
                    ?? new PortalException(PortalErrorCode.WriteFailed, failure.GetBaseException().Message.Trim(), null, failure);

                throw UnifiedRollback.Undo(made, reported, made.Count == 1 ? "device group" : "device group and subnet");
            }
        }

        private Subnet CreateSubnet(string subnetName, List<(string Name, Action Delete)> made)
        {
            var created = CurrentProject!.Subnets.Create(EthernetSubnetType, subnetName);
            made.Add((created.Name, created.Delete));

            return created;
        }

        /// <remarks>A template's group may hold groups of its own, and their devices are placed with it.</remarks>
        private static List<Device> AllDevices(DeviceUserGroup group)
        {
            return group.Devices.Concat(group.Groups.SelectMany(AllDevices)).ToList();
        }

        private Subnet? FindSubnet(string subnetName)
        {
            return AllSubnets().FirstOrDefault(subnet => string.Equals(subnet.Name, subnetName, StringComparison.OrdinalIgnoreCase));
        }

        /// <remarks>
        /// One interface for each device: a second one on the same subnet is a fault, and which to use is the template's to say.
        /// Through <see cref="SubnetConfigurator"/>, as ConnectDeviceToSubnet does, so a node already on another subnet is
        /// refused rather than moved. A device with no Ethernet interface is named in the answer, not passed over.
        /// </remarks>
        private (JsonArray Joined, JsonArray WithoutEthernet) JoinToSubnet(IReadOnlyList<Device> devices, Subnet subnet)
        {
            var nodes = devices.Select(device => (Device: device, Node: FirstEthernetNode(device))).ToList();
            var withEthernet = nodes.Where(entry => entry.Node != null).ToList();

            DeviceTemplateRules.CheckAddressesFree(subnet.Name, subnet.Nodes.Select(AddressOf), withEthernet.Select(entry => AddressOf(entry.Node!)));

            var configurator = new SubnetConfigurator(_logger);
            var subnets = AllSubnets();
            var joined = new JsonArray();

            foreach (var (device, node) in withEthernet)
            {
                configurator.Connect(node!, subnets, subnet.Name);
                joined.Add(new JsonObject { ["device"] = device.Name, ["node"] = node!.Name, ["address"] = AddressOf(node) });
            }

            return (joined, new JsonArray(nodes.Where(entry => entry.Node == null).Select(entry => (JsonNode)entry.Device.Name).ToArray()));
        }

        private static string AddressOf(Node node)
        {
            return node.GetAttribute(AddressAttribute) as string ?? string.Empty;
        }

        private static Node? FirstEthernetNode(Device device)
        {
            return AllDeviceItems(device.DeviceItems)
                .Select(item => item.GetService<NetworkInterface>())
                .Where(networkInterface => networkInterface != null)
                .SelectMany(networkInterface => networkInterface!.Nodes)
                .FirstOrDefault(node => node.NodeType == NetType.Ethernet);
        }

        private static IEnumerable<DeviceItem> AllDeviceItems(IEnumerable<DeviceItem> items)
        {
            return items.SelectMany(item => new[] { item }.Concat(AllDeviceItems(item.DeviceItems)));
        }

        /// <remarks>
        /// Only a PLC without a password: TIA Portal refuses a second one (measured 2026-11-08). One that had a password in
        /// the template keeps it, and is named so, since the stored secret is then not the one that opens it.
        /// </remarks>
        private static PlcProtection ProtectPlcs(IReadOnlyList<Device> devices, SecureString? password)
        {
            var protection = new PlcProtection();

            foreach (var item in devices.SelectMany(device => AllDeviceItems(device.DeviceItems)))
            {
                var configurator = item.GetService<PlcMasterSecretConfigurator>();

                if (configurator == null)
                {
                    continue;
                }

                if (configurator.MasterSecretConfiguration == MasterSecretConfiguration.WithPassword)
                {
                    protection.AlreadyProtected.Add(item.Name);
                    continue;
                }

                if (password == null)
                {
                    protection.WithoutPassword.Add(item.Name);
                    continue;
                }

                configurator.Protect(password);
                protection.Protected.Add(item.Name);
            }

            return protection;
        }

        /// <remarks>
        /// TIA Portal renames a group placed a second time and every device in it (Group_2, PLC_2, HMI_RT_2, measured
        /// 2026-11-08), so the devices are named as they now are, beside the flag.
        /// </remarks>
        private static JsonObject DescribePlaced(DeviceUserGroup placed, string nameInTemplate, IReadOnlyList<Device> devices, (string Subnet, (JsonArray Joined, JsonArray WithoutEthernet) Network, PlcProtection Plcs) connected)
        {
            return new JsonObject
            {
                ["group"] = placed.Name,
                ["renamedByTiaPortal"] = placed.Name != nameInTemplate,
                ["devices"] = new JsonArray(devices.Select(device => (JsonNode)device.Name).ToArray()),
                ["subnet"] = connected.Subnet,
                ["joined"] = connected.Network.Joined,
                ["withoutEthernet"] = connected.Network.WithoutEthernet,
                ["protectedPlcs"] = connected.Plcs.Protected,
                ["plcsWithoutPassword"] = connected.Plcs.WithoutPassword,
                ["plcsAlreadyProtected"] = connected.Plcs.AlreadyProtected,
                ["compile"] = new JsonArray(devices.Select(device => (JsonNode)DescribeCompilation(device)).ToArray())
            };
        }

        private sealed class PlcProtection
        {
            public JsonArray Protected { get; } = new JsonArray();

            public JsonArray WithoutPassword { get; } = new JsonArray();

            public JsonArray AlreadyProtected { get; } = new JsonArray();
        }

        private static JsonObject DescribeCompilation(Device device)
        {
            var report = CompileDevice(device);

            return new JsonObject
            {
                ["device"] = device.Name,
                ["errors"] = report.ErrorCount,
                ["warnings"] = report.WarningCount,
                ["messages"] = new JsonArray(report.Errors.Take(MaxReportedDeviceErrors).Select(message => (JsonNode)message.ToString()).ToArray())
            };
        }

        /// <remarks>What is placed is kept even when it does not compile: a PLC still needing its password is the expected case.</remarks>
        private static CompilationReport CompileDevice(Device device)
        {
            try
            {
                return CompilerResultReader.Read(device.GetService<ICompilable>()?.Compile());
            }
            catch (Exception failure) when (IsUndoableFailure(failure))
            {
                return new CompilationReport(CompilationSeverity.Error, errorCount: 1, warningCount: 0, new[] { new CompilationMessage(CompilationSeverity.Error, device.Name, failure.Message) });
            }
        }
    }
}
