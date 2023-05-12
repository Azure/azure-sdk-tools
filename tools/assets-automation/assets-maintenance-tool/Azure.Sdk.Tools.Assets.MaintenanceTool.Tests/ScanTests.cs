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
                    new RepoConfiguration("azure/azure-sdk-tools")
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

            Assert.That(results.Results[0].AssetsLocation, Is.EqualTo("sdk/agrifood/arm-agrifood/assets.json"));
            Assert.That(results.Results[0].Commit, Is.EqualTo("48bca526a2a9972e4219ec87d29a7aa31438581a"));
            Assert.That(results.Results[0].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(results.Results[0].Tag, Is.EqualTo("js/agrifood/arm-agrifood_4f244d09c7"));
            Assert.That(results.Results[0].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));

            Assert.That(results.Results[1].AssetsLocation, Is.EqualTo("sdk/storage/storage-blob/assets.json"));
            Assert.That(results.Results[1].Commit, Is.EqualTo("48bca526a2a9972e4219ec87d29a7aa31438581a"));
            Assert.That(results.Results[1].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(results.Results[1].Tag, Is.EqualTo("js/storage/storage-blob_5d5a32b74a"));
            Assert.That(results.Results[1].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));

            Assert.That(results.Results[2].AssetsLocation, Is.EqualTo("sdk/agrifood/arm-agrifood/assets.json"));
            Assert.That(results.Results[2].Commit, Is.EqualTo("f139c4ddf7aaa4d637282ae7da4466b473044281"));
            Assert.That(results.Results[2].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(results.Results[2].Tag, Is.EqualTo("js/agrifood/arm-agrifood_4f244d09c7"));
            Assert.That(results.Results[2].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));

            Assert.That(results.Results[3].AssetsLocation, Is.EqualTo("sdk/appconfiguration/app-configuration/assets.json"));
            Assert.That(results.Results[3].Commit, Is.EqualTo("4b6ee6ea00af2384c0dcc0558e9b96d8051aa8cf"));
            Assert.That(results.Results[3].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(results.Results[3].Tag, Is.EqualTo("js/appconfiguration/app-configuration_61261605e2"));
            Assert.That(results.Results[3].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));

            Assert.That(results.Results[4].AssetsLocation, Is.EqualTo("sdk/keyvault/keyvault-certificates/assets.json"));
            Assert.That(results.Results[4].Commit, Is.EqualTo("4b6ee6ea00af2384c0dcc0558e9b96d8051aa8cf"));
            Assert.That(results.Results[4].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(results.Results[4].Tag, Is.EqualTo("js/keyvault/keyvault-certificates_43821e21b3"));
            Assert.That(results.Results[4].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));

            Assert.That(results.Results[5].AssetsLocation, Is.EqualTo("sdk/keyvault/keyvault-keys/assets.json"));
            Assert.That(results.Results[5].Commit, Is.EqualTo("4b6ee6ea00af2384c0dcc0558e9b96d8051aa8cf"));
            Assert.That(results.Results[5].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(results.Results[5].Tag, Is.EqualTo("js/keyvault/keyvault-keys_b69a5239e9"));
            Assert.That(results.Results[5].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));
        }

        [Test]
        public void TestBasicScanMultipleBranchesMultipleRepos()
        {
            var scanner = new AssetsScanner();
            var config = GetRunConfiguration();
            var results = scanner.Scan(config, null);

            Assert.IsNotNull(results);
            Assert.That(results.Results.Count(), Is.EqualTo(8));

            Assert.That(results.ByRepo.Keys.Count(), Is.EqualTo(2));

            Assert.That(results.ByRepo["azure/azure-sdk-tools"][0].AssetsLocation, Is.EqualTo("sdk/formrecognizer/azure-ai-formrecognizer/assets.json"));
            Assert.That(results.ByRepo["azure/azure-sdk-tools"][0].Commit, Is.EqualTo("eeeee9e00cc0d0111edf7471962b0da826d9a5cc"));
            Assert.That(results.ByRepo["azure/azure-sdk-tools"][0].Repo, Is.EqualTo("azure/azure-sdk-tools"));
            Assert.That(results.ByRepo["azure/azure-sdk-tools"][0].Tag, Is.EqualTo("python/formrecognizer/azure-ai-formrecognizer_f60081bf10"));
            Assert.That(results.ByRepo["azure/azure-sdk-tools"][0].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));

            Assert.That(results.ByRepo["azure/azure-sdk-tools"][1].AssetsLocation, Is.EqualTo("sdk/keyvault/assets.json"));
            Assert.That(results.ByRepo["azure/azure-sdk-tools"][1].Commit, Is.EqualTo("eeeee9e00cc0d0111edf7471962b0da826d9a5cc"));
            Assert.That(results.ByRepo["azure/azure-sdk-tools"][1].Repo, Is.EqualTo("azure/azure-sdk-tools"));
            Assert.That(results.ByRepo["azure/azure-sdk-tools"][1].Tag, Is.EqualTo("python/keyvault/azure-keyvault-administration_f6e776f55f"));
            Assert.That(results.ByRepo["azure/azure-sdk-tools"][1].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));

            Assert.That(results.ByRepo["azure/azure-sdk-assets-integration"][5].AssetsLocation, Is.EqualTo("sdk/keyvault/keyvault-keys/assets.json"));
            Assert.That(results.ByRepo["azure/azure-sdk-assets-integration"][5].Commit, Is.EqualTo("4b6ee6ea00af2384c0dcc0558e9b96d8051aa8cf"));
            Assert.That(results.ByRepo["azure/azure-sdk-assets-integration"][5].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(results.ByRepo["azure/azure-sdk-assets-integration"][5].Tag, Is.EqualTo("js/keyvault/keyvault-keys_b69a5239e9"));
            Assert.That(results.ByRepo["azure/azure-sdk-assets-integration"][5].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));
        }

        [Test]
        public void TestScanHonorsPreviousResults()
        {
            throw new NotImplementedException("Need to implement");
        }

        [Test]
        public void VerifyScanOutputResults()
        {
            var targetDirectory = Directory.GetCurrentDirectory();

            var scanner = new AssetsScanner(targetDirectory);
            var config = GetRunConfiguration();
            config.Repos.RemoveAt(0);
            config.Repos.First().Branches.RemoveAt(1);
            var results = scanner.Scan(config, null);

            // now we need to confirm that the output file exists
            var fileThatShouldExist = Path.Combine(targetDirectory, "output.json");

            Assert.That(File.Exists(fileThatShouldExist), Is.EqualTo(true));
        }

        [Test]
        public void VerifyScannerCanRetrieveExistingResults()
        {
            throw new NotImplementedException("Need to implement");
        }
    }
}
