using System;
using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec.Tests
{
    /// <remarks>
    /// Loading is where a specification actually goes wrong: a key spelled differently, a station name
    /// with a space in it, a count left at zero. Tests that construct the object in code never
    /// exercise any of that, which is why these write real files.
    /// </remarks>
    [TestClass]
    public sealed class CellSpecificationFileTests
    {
        private string _directory = string.Empty;

        [TestInitialize]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "TiaMcpCellSpecTests", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void RemoveDirectory()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [TestMethod]
        public void Load_AValidFile_ReadsTheStationsInOrder()
        {
            var path = Write(
                "{ \"cell\": \"Demo\", \"stations\": ["
                + "{ \"name\": \"Feeder\", \"workSteps\": 2, \"dwellCycles\": 5 },"
                + "{ \"name\": \"Driller\", \"workSteps\": 3, \"dwellCycles\": 10 } ] }");

            var cell = CellSpecificationFile.Load(path);

            Assert.AreEqual("Demo", cell.Name);
            Assert.AreEqual(2, cell.Stations.Count);
            Assert.AreEqual("Feeder", cell.Stations[0].Name);
            Assert.AreEqual(3, cell.Stations[1].WorkSteps);
            Assert.AreEqual(10, cell.Stations[1].DwellCycles);
        }

        [TestMethod]
        public void Load_AFileWithCommentsAndATrailingComma_IsAccepted()
        {
            // These files are edited by a person deciding what a cell is, and a person needs to be able
            // to write down why. Refusing a comment would push that explanation out of the file.
            var path = Write(
                "// which cell this is" + Environment.NewLine
                + "{ \"cell\": \"Demo\", \"stations\": [ { \"name\": \"A\", \"workSteps\": 1, \"dwellCycles\": 1 }, ] }");

            var cell = CellSpecificationFile.Load(path);

            Assert.AreEqual("Demo", cell.Name);
        }

        [TestMethod]
        public void Load_KeysInADifferentCase_AreStillRead()
        {
            var path = Write("{ \"Cell\": \"Demo\", \"Stations\": [ { \"Name\": \"A\", \"WorkSteps\": 1, \"DwellCycles\": 1 } ] }");

            Assert.AreEqual("Demo", CellSpecificationFile.Load(path).Name);
        }

        [TestMethod]
        public void Load_AMissingFile_SaysWhereCellsLive()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => CellSpecificationFile.Load(Path.Combine(_directory, "no-such-cell.json")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
            StringAssert.Contains(exception.Message, "spec/cells/", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Load_BrokenJson_NamesTheFile()
        {
            var path = Write("{ \"cell\": \"Demo\", ");

            var exception = Assert.ThrowsException<PortalException>(() => CellSpecificationFile.Load(path));

            StringAssert.Contains(exception.Message, "not valid JSON", StringComparison.Ordinal);
            StringAssert.Contains(exception.Message, path, StringComparison.Ordinal);
        }

        [TestMethod]
        public void Load_AStationWithASpaceInItsName_NamesTheFileAndTheRule()
        {
            // The failure this whole loader exists to report well. Without the file name, "a station
            // name may only contain letters" leaves the person guessing which of four files to open.
            var path = Write("{ \"cell\": \"Demo\", \"stations\": [ { \"name\": \"Drill 1\", \"workSteps\": 1, \"dwellCycles\": 1 } ] }");

            var exception = Assert.ThrowsException<PortalException>(() => CellSpecificationFile.Load(path));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
            StringAssert.Contains(exception.Message, path, StringComparison.Ordinal);
            StringAssert.Contains(exception.Message, "SCL identifier", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Load_AFileWithNoStations_IsRefused()
        {
            var path = Write("{ \"cell\": \"Demo\", \"stations\": [] }");

            var exception = Assert.ThrowsException<PortalException>(() => CellSpecificationFile.Load(path));

            StringAssert.Contains(exception.Message, "no stations", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Load_AFileMissingWorkSteps_IsRefusedRatherThanDefaultedToZero()
        {
            // An omitted number deserialises to 0, and 0 work steps is a station that reports Done
            // without doing anything. Silently accepting the default would be the worst outcome.
            var path = Write("{ \"cell\": \"Demo\", \"stations\": [ { \"name\": \"A\" } ] }");

            Assert.ThrowsException<PortalException>(() => CellSpecificationFile.Load(path));
        }

        [TestMethod]
        public void Load_WithNoPath_IsRejected()
        {
            Assert.ThrowsException<ArgumentException>(() => CellSpecificationFile.Load(string.Empty));
        }

        [TestMethod]
        public void Load_TheRepositorysOwnCells_Works()
        {
            // The two files the roadmap names, loaded as they ship. A specification nobody loads is a
            // specification that quietly stops matching the loader.
            var root = FindRepositoryRoot();

            foreach (var file in new[] { "two-station-demo.json", "four-station-cell.json" })
            {
                var cell = CellSpecificationFile.Load(Path.Combine(root, "spec", "cells", file));

                Assert.IsTrue(cell.Stations.Count > 0, $"{file} declares no stations");
            }
        }

        private string Write(string json)
        {
            var path = Path.Combine(_directory, "cell.json");

            File.WriteAllText(path, json);

            return path;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "spec")))
            {
                directory = directory.Parent;
            }

            Assert.IsNotNull(directory, "could not find the repository root, so spec/ cannot be read");

            return directory!.FullName;
        }
    }
}
