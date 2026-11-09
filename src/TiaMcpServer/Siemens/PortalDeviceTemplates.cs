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
        /// <returns>What was placed, under the names TIA Portal gave it, with each device's compilation.</returns>
        /// <exception cref="PortalException">See <see cref="DeviceTemplateRules"/>; or the template or the secret is missing. What fails leaves nothing in the project.</exception>
        public JsonObject CreateDevicesFromTemplate(DeviceTemplatePlacement placement, ISecretStore secrets)
        {
            _logger?.LogInformation("Placing template {Template} on subnet {Subnet}...", placement?.TemplateName, placement?.SubnetName);

            try
            {
                DeviceTemplateRules.CheckTemplateName(placement!.TemplateName);
                DeviceTemplateRules.CheckSubnetName(placement.SubnetName);
                RequireDeviceGroupsOfOpenProject();

                using var password = string.IsNullOrWhiteSpace(placement.PlcSecretName) ? null : secrets.Reveal(placement.PlcSecretName);
                var (placed, warning) = GlobalLibraryAccess.Use(RequirePortal(), placement.GlobalLibraryPath, global => PlaceTemplate(global, placement, password));

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

        private JsonObject PlaceTemplate(GlobalLibrary global, DeviceTemplatePlacement placement, SecureString? password)
        {
            var copy = MasterCopyReader.Find(global.MasterCopyFolder, placement.TemplateName)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"The global library has no template '{placement.TemplateName}'. It holds: {string.Join(", ", global.MasterCopyFolder.MasterCopies.Select(held => held.Name))}");

            var contents = MasterCopyReader.Contents(copy);
            DeviceTemplateRules.RequireDeviceGroup(copy.Name, contents.Select(content => content.TypeName).ToList());

            var placed = CurrentProject!.DeviceGroups.CreateFrom(copy);
            var subnet = FindSubnet(placement.SubnetName);
            var isNewSubnet = subnet == null;

            try
            {
                subnet ??= CurrentProject.Subnets.Create(EthernetSubnetType, placement.SubnetName);
                var joined = JoinToSubnet(placed, subnet);
                var plcs = ProtectPlcs(placed, password);

                return DescribePlaced(placed, contents[0].Name, (subnet.Name, joined, plcs));
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                var undone = UndoPlacement(placed, isNewSubnet ? subnet : null);

                throw new PortalException((failure as PortalException)?.Code ?? PortalErrorCode.WriteFailed, $"{failure.GetBaseException().Message.Trim()} {undone}", null, failure);
            }
        }

        private Subnet? FindSubnet(string subnetName)
        {
            return AllSubnets().FirstOrDefault(subnet => string.Equals(subnet.Name, subnetName, StringComparison.OrdinalIgnoreCase));
        }

        /// <remarks>One interface for each device: a second one on the same subnet is a fault, and which to use is the template's to say.</remarks>
        private static JsonArray JoinToSubnet(DeviceUserGroup placed, Subnet subnet)
        {
            var nodes = placed.Devices.Select(device => (Device: device, Node: FirstEthernetNode(device))).Where(entry => entry.Node != null).ToList();

            DeviceTemplateRules.CheckAddressesFree(subnet.Name, subnet.Nodes.Select(AddressOf), nodes.Select(entry => AddressOf(entry.Node!)));

            var joined = new JsonArray();
            foreach (var (device, node) in nodes)
            {
                node!.ConnectToSubnet(subnet);
                joined.Add(new JsonObject { ["device"] = device.Name, ["node"] = node.Name, ["address"] = AddressOf(node) });
            }

            return joined;
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

        /// <remarks>Only a PLC without a password: TIA Portal refuses a second one (measured 2026-11-08).</remarks>
        private static (JsonArray Protected, JsonArray WithoutPassword) ProtectPlcs(DeviceUserGroup placed, SecureString? password)
        {
            var isProtected = new JsonArray();
            var withoutPassword = new JsonArray();

            foreach (var item in placed.Devices.SelectMany(device => AllDeviceItems(device.DeviceItems)))
            {
                var configurator = item.GetService<PlcMasterSecretConfigurator>();

                if (configurator == null || configurator.MasterSecretConfiguration == MasterSecretConfiguration.WithPassword)
                {
                    continue;
                }

                if (password == null)
                {
                    withoutPassword.Add(item.Name);
                    continue;
                }

                configurator.Protect(password);
                isProtected.Add(item.Name);
            }

            return (isProtected, withoutPassword);
        }

        private static JsonObject DescribePlaced(DeviceUserGroup placed, string nameInTemplate, (string Subnet, JsonArray Joined, (JsonArray Protected, JsonArray WithoutPassword) Plcs) connected)
        {
            return new JsonObject
            {
                ["group"] = placed.Name,
                ["renamedByTiaPortal"] = placed.Name != nameInTemplate,
                ["subnet"] = connected.Subnet,
                ["joined"] = connected.Joined,
                ["protectedPlcs"] = connected.Plcs.Protected,
                ["plcsWithoutPassword"] = connected.Plcs.WithoutPassword,
                ["compile"] = new JsonArray(placed.Devices.Select(device => (JsonNode)DescribeCompilation(device)).ToArray())
            };
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

        /// <returns>What became of it, for the refusal to say; a failure here must not replace the failure being reported.</returns>
        private static string UndoPlacement(DeviceUserGroup placed, Subnet? createdSubnet)
        {
            var name = placed.Name;

            try
            {
                placed.Delete();
                createdSubnet?.Delete();

                return "Nothing was left in the project.";
            }
            catch (Exception failure) when (IsUndoableFailure(failure))
            {
                return $"Taking it back failed too ({failure.Message}): the device group '{name}' may still be in the project.";
            }
        }
    }
}
