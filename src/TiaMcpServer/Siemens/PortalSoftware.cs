using Microsoft.Extensions.Logging;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Safety;
using Siemens.Engineering.SW;
using System;
using System.Net;
using System.Security;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The PLC software of a device, which every block and type operation is addressed through.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Describes the PLC software at a path.</summary>
        /// <param name="softwarePath">Path to the PLC software in the project.</param>
        /// <returns>The description, or null when there is no PLC software there.</returns>
        public ObjectDescription? GetPlcSoftware(string softwarePath)
        {
            var software = FindPlcSoftware(softwarePath);

            return software == null ? null : ObjectDescriber.Describe(software, software.Name);
        }

        private PlcSoftware? FindPlcSoftware(string softwarePath)
        {
            _logger?.LogInformation($"Getting software by path: {softwarePath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);

            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                return plcSoftware;
            }

            return null;
        }

        /// <summary>
        /// Compiles a PLC software, a classic WinCC HMI target or a WinCC Unified HMI, and returns
        /// what the compiler said, flattened into data. A Unified HMI compiles with the whole station
        /// that holds it.
        /// </summary>
        /// <param name="softwarePath">Full path to the PLC software or HMI, for example <c>Group1/PLC_1</c> or <c>PC-System_1/HMI_RT_1</c>.</param>
        /// <param name="password">Password for the safety administration, when the software needs one.</param>
        /// <returns>
        /// The report. A failed compile is a normal outcome and comes back as a report with
        /// errors, not as an exception: the caller's next step is to read them and fix the code.
        /// </returns>
        /// <exception cref="PortalException">
        /// No project is open, the software path does not resolve, or the safety login failed —
        /// none of which the compiler's output can explain.
        /// </exception>
        public CompilationReport CompileSoftware(string softwarePath, string password = "")
        {
            _logger?.LogInformation("Compiling software by path: {SoftwarePath}", softwarePath);

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before compiling");
                }

                var softwareContainer = GetSoftwareContainer(softwarePath);

                LoginToSafetyProgram(softwareContainer, password);

                var compilable = FindCompilable(softwareContainer)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"No PLC or HMI software found: {softwarePath}");

                var result = compilable.Compile();

                return CompilerResultReader.Read(result);
            }
            catch (Exception ex)
            {
                var pex = ForgetEndedSession(ex, "CompileSoftware")
                    ?? ex as PortalException
                    ?? new PortalException(PortalErrorCode.CompileFailed, $"Compile failed: {ex.Message}", null, ex);

                pex.Data["softwarePath"] = softwarePath;

                _logger?.LogError(pex, "CompileSoftware failed for {SoftwarePath}", softwarePath);
                throw pex;
            }
        }

        /// <remarks>
        /// A classic WinCC HMI target compiles through the same service as a PLC program, so one
        /// tool covers both: generated screens and HMI tags are only checked by this. WinCC Unified
        /// does not; see <see cref="FindUnifiedStationCompilable"/>.
        /// </remarks>
        private static ICompilable? FindCompilable(SoftwareContainer? softwareContainer)
        {
            return softwareContainer switch
            {
                { Software: PlcSoftware plcSoftware } => plcSoftware.GetService<ICompilable>(),
                { Software: HmiTarget hmiTarget } => hmiTarget.GetService<ICompilable>(),
                { Software: UnifiedHmiSoftware unified } => FindUnifiedStationCompilable(softwareContainer.Parent, unified.Name),
                _ => null
            };
        }

        private static void LoginToSafetyProgram(SoftwareContainer? softwareContainer, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return;
            }

            var admin = (softwareContainer?.Parent as DeviceItem)?.GetService<SafetyAdministration>();

            if (admin == null || admin.IsLoggedOnToSafetyOfflineProgram)
            {
                return;
            }

            SecureString secureString = new NetworkCredential(string.Empty, password).SecurePassword;

            admin.LoginToSafetyOfflineProgram(secureString);
        }
    }
}
