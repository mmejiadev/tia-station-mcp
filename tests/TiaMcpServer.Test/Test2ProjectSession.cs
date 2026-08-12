namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test2ProjectSession
    {
        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void GetProjects_ProjectOpen_ListsProjectsAndSessions()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);

            var projects = AssemblyHooks.SharedPortal.GetProjects();
            projects.AddRange(AssemblyHooks.SharedPortal.GetSessions());

            Assert.IsTrue(projects.Count > 0, "Neither a project nor a session was reported");
        }

        [TestMethod]
        public void GetProjectTree_OpenProject_DescribesTheProject()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);

            var tree = AssemblyHooks.SharedPortal.GetProjectTree();

            // The original test asserted IsNotNull on a bool, which can never fail. A tree is
            // useful only if it actually names the devices, so that is what is checked.
            Assert.IsFalse(string.IsNullOrWhiteSpace(tree), "The project tree is empty");
            Assert.IsTrue(tree!.Contains("PLC_0"), $"The project tree does not mention PLC_0:\n{tree}");
        }

        [TestMethod]
        public void GetSoftwareTree_PlcSoftware_DescribesTheProgram()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);

            var tree = AssemblyHooks.SharedPortal.GetSoftwareTree(Settings.Project1PlcSoftwarePath0);

            Assert.IsFalse(string.IsNullOrWhiteSpace(tree), "The software tree is empty");
            Assert.IsTrue(tree!.Contains("Main"), $"The software tree does not mention the Main block:\n{tree}");
        }
    }
}
