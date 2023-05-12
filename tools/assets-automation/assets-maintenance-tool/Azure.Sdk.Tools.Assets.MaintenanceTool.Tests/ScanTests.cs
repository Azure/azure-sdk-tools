global using NUnit.Framework;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Scan;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Tests
{
    public class Tests
    {
        /*
         * integration/assets-branch-1
         *   f139c4ddf7aaa4d637282ae7da4466b473044281
         *     sdk/agrifood/arm-agrifood/assets.json
         *   48bca526a2a9972e4219ec87d29a7aa31438581a
         *     sdk/agrifood/arm-agrifood/assets.json
         *     sdk/storage/storage-blob/assets.json
         * 
         * integration/assets-branch-2
         *   4b6ee6ea00af2384c0dcc0558e9b96d8051aa8cf
         *     sdk/appconfiguration/app-configuration/assets.json
         *     sdk/keyvault/keyvault-certificates/assets.json
         *     sdk/keyvault/keyvault-keys/assets.json
         *      
         * All told, 
         */
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
            config.Repos.RemoveAt(0);
            config.Repos.First().Branches.RemoveAt(1);

            var results = scanner.Scan(config, null);

            Assert.IsNotNull(results);
            Assert.That(results.Results.Count(), Is.EqualTo(3));

            var payload0 = results.Results[0];
            var payload1 = results.Results[1];
            var payload2 = results.Results[2];

            Assert.That(payload0.AssetsLocation, Is.EqualTo("sdk/agrifood/arm-agrifood/assets.json"));
            Assert.That(payload0.Commit, Is.EqualTo("48bca526a2a9972e4219ec87d29a7aa31438581a"));
            Assert.That(payload0.Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(payload0.Tag, Is.EqualTo("js/agrifood/arm-agrifood_4f244d09c7"));
            Assert.That(payload0.TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));

            Assert.That(payload1.AssetsLocation, Is.EqualTo("sdk/storage/storage-blob/assets.json"));
            Assert.That(payload1.Commit, Is.EqualTo("48bca526a2a9972e4219ec87d29a7aa31438581a"));
            Assert.That(payload1.Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(payload1.Tag, Is.EqualTo("js/storage/storage-blob_5d5a32b74a"));
            Assert.That(payload1.TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));

            Assert.That(payload2.AssetsLocation, Is.EqualTo("sdk/agrifood/arm-agrifood/assets.json"));
            Assert.That(payload2.Commit, Is.EqualTo("f139c4ddf7aaa4d637282ae7da4466b473044281"));
            Assert.That(payload2.Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(payload2.Tag, Is.EqualTo("js/agrifood/arm-agrifood_4f244d09c7"));
            Assert.That(payload2.TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));
        }

        [Test]
        public void TestBasicScanMultipleBranches()
        {
            var scanner = new AssetsScanner();
            var config = GetRunConfiguration();
            config.Repos.RemoveAt(0);
            var results = scanner.Scan(config, null);

            Assert.IsNotNull(results);
            Assert.That(results.Results.Count(), Is.EqualTo(6));

        }

        [Test]
        public void TestBasicScanMultipleBranchesMultipleRepos()
        {
            var scanner = new AssetsScanner();
            var config = GetRunConfiguration();
            var results = scanner.Scan(config, null);

            Assert.IsNotNull(results);
            Assert.GreaterOrEqual(1, results.Results.Count());
        }


        [Test]
        public void TestScanHonorsPreviousResults()
        {
            throw new NotImplementedException("Need to implement");
        }

        [Test]
        public void VerifyScanOutputResults()
        {

        }
    }
}
