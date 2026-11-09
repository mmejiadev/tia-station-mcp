using System;
using System.IO;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The test fixture's constants.
    /// </summary>
    /// <remarks>
    /// This file used to pin every test to absolute paths under <c>D:\Siemens\...</c>, which
    /// existed only on the original author's machine and left the whole suite dead everywhere
    /// else. Nothing here is a filesystem path any more: the project is built at run time by
    /// retrieving <c>assets/TestProject1.zap20</c>, and what remains are paths *inside* the
    /// project, which are part of the fixture rather than of the machine.
    ///
    /// They are still <c>const</c> because <c>[DataRow]</c> only accepts compile-time constants.
    /// </remarks>
    internal static class Settings
    {
        /// <summary>
        /// The sample archive shipped with the repository, resolved relative to the test assembly
        /// so it works wherever the tests run.
        /// </summary>
        public static string Project1ArchivePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "TestProject1.zap20");

        /// <summary>
        /// A class project with one WinCC Unified PC RT whose screen holds a script dynamization with
        /// no tag — an error TIA Portal's Info tab showed with no text (2026-10-17).
        /// </summary>
        public static string UnifiedArchivePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "UnifiedProject.zap20");

        /// <summary>
        /// The class project 0967_1, archived without restorable data: an S7-1500, a WinCC Unified PC RT
        /// with HMI tags M1 to M7 of the PLC data type UDT_MOTOR_V1, and two faceplate types in its library,
        /// FACEPLATE_MOTOR_V1 and Faceplate_BOTO_MOTOR_V1, each with version 0.0.1 still in work.
        /// </summary>
        public static string FaceplateArchivePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "FaceplateProject.zap20");

        /// <summary>
        /// The class project UNIFIED-LIBRERIES with the device group Group_1: an S7-1200 and a WinCC Unified PC RT with their
        /// integrated connection, made by hand in TIA Portal on 2026-11-08, which Openness cannot do.
        /// </summary>
        public static string HmiTemplateArchivePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "HmiTemplateProject.zap20");

        /// <summary>
        /// A global library folder, zipped: FACEPLATE_MOTOR_V1 and Faceplate_BOTO_MOTOR_V1 released as 0.0.1,
        /// and UDT_MOTOR_V1 that the first depends on. Made from the class project 0967_1 on 2026-10-30.
        /// </summary>
        public static string FaceplateLibraryZipPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "FaceplateLibrary.zip");

        /// <summary>
        /// The write policy the suite runs under, resolved the same way as the archive.
        /// </summary>
        /// <remarks>
        /// Writes go through the governance layer, which denies everything unless a policy names
        /// the target. Without this file every MCP write tool in the suite would be refused —
        /// correctly, and with a message saying so.
        /// </remarks>
        public static string Project1PolicyPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "policy.json");

        // PLC software paths inside TestProject1. Only the first is exercised by the tests that
        // touch blocks and tag tables; the deeper ones cover path resolution through nested groups.
        public const string Project1PlcSoftwarePath0 = "PLC_0";
        public const string Project1PlcSoftwarePath1 = "PC-System_0/Software PLC_0";
        public const string Project1PlcSoftwarePath2 = "Group1/PLC_1";
        public const string Project1PlcSoftwarePath3 = "Group1/PC-System_1/Software PLC_1";
        public const string Project1PlcSoftwarePath4 = "Group1/Group1.1/PLC_1.1";
        public const string Project1PlcSoftwarePath5 = "Group1/Group1.1/PC-System_1.1/Software PLC_1.1";
        public const string Project1PlcSoftwarePath6 = "Group1/Group1.1/Group1.1.1/PLC_1.1.1";
        public const string Project1PlcSoftwarePath7 = "Group1/Group1.1/Group1.1.1/PC-System_1.1.1/Software PLC_1.1.1";

        /// <summary>
        /// Reason given by every test that needs a multiuser session.
        /// </summary>
        /// <remarks>
        /// These are not skipped out of laziness. A local session is an <c>.alsNN</c> file produced
        /// by a TIA Portal Multiuser server against a server project; there is no such asset in the
        /// repository and one cannot be synthesised offline. Rather than delete the tests and lose
        /// the record that this surface is untested, they are marked and kept.
        /// </remarks>
        public const string NoMultiuserSessionAsset =
            "Needs a multiuser session (.alsNN). No such asset ships with the repository and one cannot be created without a TIA Portal Multiuser server.";
    }
}
