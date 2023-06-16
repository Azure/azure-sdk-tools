global using NUnit.Framework;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Scan;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Tests
{
    public class CLITests
    {
        public string TestDirectory { get; protected set; } 

        [SetUp]
        public void Setup()
        {
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }

            // copy our static test files there
            var source = Path.Combine(Directory.GetCurrentDirectory(), "TestResources");
            var target = Path.Combine(workingDirectory, "TestResources");

            Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(source, target);

            TestDirectory = workingDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(TestDirectory, true);
        }

        [Test]
        [TestCase("scan", "-c", "")]
        [TestCase("scan", "--config", "")]
        public void TestScanOptions(params string[] args)
        {
        }

        [Test]
        [TestCase("scan")]
        [TestCase("scan", "--config")]
        public void TestInvalidScanOptions(params string[] args)
        {
            var obj = new object(); 
            
            var rootCommand = Program.InitializeCommandOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;
            });

        }
    }
}
