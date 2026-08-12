namespace TiaMcpServer.Test
{
    [TestClass]
    public class AssemblyHooks
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            // Runs once before any tests in the assembly
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            // Deliberately empty. Killing leftover TIA Portal processes from here is tempting but
            // wrong: TiaPortal.GetProcesses() cannot tell an instance started by this suite from
            // one the developer opened by hand, and Dispose() on an attached instance only
            // detaches anyway. Each test owns the portal it starts and releases it in its own
            // TestCleanup; see Test1Portal.
        }
    }
}
