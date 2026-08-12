using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test21Project
    {
        [TestCleanup]
        public void TestCleanup()
        {
            // These tests open and close the shared project. Leave it closed so the next class
            // starts from a known state rather than inheriting whatever this one left open.
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void GetProjects_ProjectOpen_ReturnsIt()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);

            var projects = AssemblyHooks.SharedPortal.GetProjects();

            Assert.IsNotNull(projects);
            Assert.IsTrue(projects.Count > 0, "An open project was not reported by GetProjects");
        }

        [TestMethod]
        public void OpenProject_RetrievedProject_Succeeds()
        {
            var result = AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);

            Assert.IsTrue(result, $"Failed to open {AssemblyHooks.ProjectPath}");
        }

        [TestMethod]
        public void CloseProject_OpenProject_Succeeds()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);

            var result = AssemblyHooks.SharedPortal.CloseProject();

            Assert.IsTrue(result, "Failed to close the project");
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetProjects().Count, "A project is still open after closing");
        }

        [TestMethod]
        public void SaveProject_OpenProject_Succeeds()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);

            var result = AssemblyHooks.SharedPortal.SaveProject();

            Assert.IsTrue(result, "Failed to save the project");
        }

        [TestMethod]
        public void SaveAsProject_NewLocation_WritesProjectFile()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
            var target = Path.Combine(AssemblyHooks.CreateTestDirectory(), "TestProject1Copy");

            var result = AssemblyHooks.SharedPortal.SaveAsProject(target);

            Assert.IsTrue(result, $"Failed to save the project as {target}");
            Assert.IsTrue(Directory.Exists(target), $"SaveAs reported success but wrote nothing to {target}");
        }
    }
}
