global using NUnit.Framework;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Scan;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Tests
{
    public class Tests
    {
        private RunConfiguration GetRunConfiguration()
        {
            return new RunConfiguration()
            {
                Repos = new List<RepoConfiguration>
                {
                    new RepoConfiguration("azure/azure-sdk-for-python")
                    {
                        Branches = new List<string>(){ "integration/assets-test-branch" }
                    },
                    new RepoConfiguration("azure/azure-sdk-assets-integration")
                    {
                        Branches = new List<string>(){
                            "integration/assets-branch-1",
                            "integration/assets-branch-2"
                        }
                    }
                }
            };
        }

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestBasicScanSingleBranch()
        {
            var scanner = new AssetsScanner();
            var config = GetRunConfiguration();
            config.Repos.RemoveAt(1);

            var results = scanner.Scan(config, null);

            Assert.IsNotNull(results);
            Assert.GreaterOrEqual(1, results.Results.Count());

            Assert.Equals(5, results.ByTargetTag.Keys.Count());
        }

        [Test]
        public void TestBasicScanMultipleBranches()
        {
            var scanner = new AssetsScanner();
            var config = GetRunConfiguration();
            config.Repos.RemoveAt(0);
            var results = scanner.Scan(config, null);

            Assert.IsNotNull(results);
            Assert.GreaterOrEqual(1, results.Results.Count());
        }

        [Test]
        public void TestBasicScanMultipleBranchesMutipleRepos()
        {
            var scanner = new AssetsScanner();
            var config = GetRunConfiguration();
            var results = scanner.Scan(config, null);

            Assert.IsNotNull(results);
            Assert.GreaterOrEqual(1, results.Results.Count());
        }


        [Test]
        public void TestBasicScanHonorsPreviousScanResults()
        {
            throw new NotImplementedException("Need to implement");
        }
    }
}
