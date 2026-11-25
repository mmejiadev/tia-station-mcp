using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// A PLC and an HMI with their integrated connection, as a template. On V20 Openness could not create that connection and
    /// the group was made by hand once; on V21 the whole group is built with AddDevice, AddPcStation (both with a group),
    /// the network tools and CreateHmiConnection, and a placed copy keeps the connection to its own PLC (measured 2026-11-24).
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateDeviceGroupTemplate"), Description("Make a device group of the open project a template in a global library, with the integrated HMI connection it holds. On TIA Portal V21 the group can be built entirely through these tools: AddDevice and AddPcStation with groupName, CreateSubnet and ConnectDeviceToSubnet, then CreateHmiConnection; on V20 the connection is made by hand. Openness cannot move a device into a group, so the devices have to be created in it. A template of that name already there is refused. The library is archived to the backup registry first, then saved and closed; it must not be open in TIA Portal. Use it with CreateDevicesFromTemplate.")]
        public static ResponseLibraryTypes CreateDeviceGroupTemplate(
            [Description("groupName: the device group, at the top of the project tree")] string groupName,
            [Description("globalLibraryPath: the global library file, e.g. 'C:\\Libraries\\Kit\\Kit.al20'")] string globalLibraryPath,
            [Description("templateName: the name for the template")] string templateName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.GlobalLibraryFile(globalLibraryPath);
                var backupDirectory = Backups.Allocate("CreateDeviceGroupTemplate", target);
                var request = new Governance.ChangeRequest("CreateDeviceGroupTemplate", target, $"{templateName} from device group {groupName}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.CreateDeviceGroupTemplate(groupName, globalLibraryPath, templateName, backupDirectory), $"Template '{templateName}' saved in '{globalLibraryPath}'."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"making a template of device group '{groupName}'");
            }
        }

        [McpServerTool(Name = "CreateDevicesFromTemplate"), Description("Bring a device group template from a global library into the open project, with its integrated HMI connection; join each device's first Ethernet interface to a subnet, creating it when there is none of that name; protect each PLC with a stored secret, by name; and compile each device. The secret's value is never given here: ask the person to run 'TiaMcpServer secret set NAME' in their own terminal, and ListSecrets names what is stored. Without a secret a PLC does not compile, and its name comes back in plcsWithoutPassword. TIA Portal renames everything of a template placed twice and repeats its addresses, so a subnet already holding an address is refused, and the names it gave are in the answer, with any device that has no Ethernet interface and any PLC that already had a password. A subnet already in the project is joined, which changes it, so the network as it stood is recorded to the backup registry first. A write policy has to allow the project, the library by its full folder, and the secret by name ('secret/NAME'). A failure leaves nothing in the project. Save the project to keep it.")]
        public static ResponseLibraryTypes CreateDevicesFromTemplate(
            [Description("globalLibraryPath: the global library file, e.g. 'C:\\Libraries\\Kit\\Kit.al20'")] string globalLibraryPath,
            [Description("templateName: the template, as CreateDeviceGroupTemplate named it")] string templateName,
            [Description("subnetName: the subnet to join the devices to, e.g. 'PN_Cell'")] string subnetName,
            [Description("plcSecretName: the NAME of the stored secret for the PLCs; empty for none. Never the password")] string plcSecretName = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var placement = new DeviceTemplatePlacement(globalLibraryPath, (templateName ?? string.Empty).Trim(), (subnetName ?? string.Empty).Trim(), (plcSecretName ?? string.Empty).Trim());
                var target = TargetOfPlacement(placement);
                var backupDirectory = Backups.Allocate("CreateDevicesFromTemplate", target);
                var secret = placement.PlcSecretName.Length == 0 ? "no PLC secret" : $"PLC secret named '{placement.PlcSecretName}'";
                var request = new Governance.ChangeRequest("CreateDevicesFromTemplate", target, $"template {placement.TemplateName} from {globalLibraryPath}, on subnet {placement.SubnetName}, {secret}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.CreateDevicesFromTemplate(placement, SecretStore, backupDirectory), $"Template '{placement.TemplateName}' placed on subnet '{placement.SubnetName}'. Read each device's compilation in the answer, and save the project to keep it."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"placing template '{templateName}'");
            }
        }

        /// <remarks>
        /// It writes the project, takes from the library and puts a stored secret into the PLCs, and a policy has to allow
        /// all three: a template name gates nothing, and a rule letting a session build devices must not let it use every
        /// password kept on the machine. Each refused target is the one the refusal is recorded against.
        /// </remarks>
        private static string TargetOfPlacement(DeviceTemplatePlacement placement)
        {
            var projectAndLibrary = GuardedWrites.TargetToPropose(ChangeTarget.Project, ChangeTarget.GlobalLibraryFile(placement.GlobalLibraryPath));

            return placement.PlcSecretName.Length == 0
                ? projectAndLibrary
                : GuardedWrites.TargetToPropose(projectAndLibrary, ChangeTarget.Secret(placement.PlcSecretName));
        }
    }
}
