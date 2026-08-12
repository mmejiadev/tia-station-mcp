namespace TiaMcpServer.Test
{
    /// <remarks>
    /// Only the first test here can run. The rest need a multiuser session file and are kept,
    /// marked, rather than deleted: the surface is real and untested, and a deleted test stops
    /// saying so. See <see cref="Settings.NoMultiuserSessionAsset"/>.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test22Session
    {
        [TestMethod]
        public void GetSessions_NoSessionOpen_ReturnsEmptyList()
        {
            var sessions = AssemblyHooks.SharedPortal.GetSessions();

            // The distinction that matters: an empty list, not null. Callers iterate the result.
            Assert.IsNotNull(sessions, "GetSessions returned null instead of an empty list");
            Assert.AreEqual(0, sessions.Count, "A session is open when none should be");
        }

        [TestMethod]
        [Ignore(Settings.NoMultiuserSessionAsset)]
        public void OpenSession_ValidSession_Succeeds()
        {
            Assert.Inconclusive(Settings.NoMultiuserSessionAsset);
        }

        [TestMethod]
        [Ignore(Settings.NoMultiuserSessionAsset)]
        public void CloseSession_OpenSession_Succeeds()
        {
            Assert.Inconclusive(Settings.NoMultiuserSessionAsset);
        }

        [TestMethod]
        [Ignore(Settings.NoMultiuserSessionAsset)]
        public void SaveSession_OpenSession_Succeeds()
        {
            Assert.Inconclusive(Settings.NoMultiuserSessionAsset);
        }
    }
}
