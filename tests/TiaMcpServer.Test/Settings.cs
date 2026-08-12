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
