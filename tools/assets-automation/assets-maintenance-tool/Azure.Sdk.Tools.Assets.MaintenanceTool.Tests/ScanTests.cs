global using NUnit.Framework;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Scan;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Tests
{
    public class ScanTests
    {
        // These tests assume the presence of three integration branches for the purposes of testing.
        // Azure/azure-sdk-tools
          // https://github.com/Azure/azure-sdk-tools/tree/integration/assets-test-branch
        // Azure/azure-sdk-assets-integration
          // This repo is non-public, and as such a valid GIT_TOKEN must be set or the default git account
          // on the invoking machine should have permissions to Azure/azure-sdk-assets-integration.
          // 
          // https://github.com/Azure/azure-sdk-assets-integration/tree/integration/assets-branch-1
          // https://github.com/Azure/azure-sdk-assets-integration/tree/integration/assets-branch-2
        private RunConfiguration RunConfiguration
        {
            get
            {
                return new RunConfiguration()
                {
                    Repos = new List<RepoConfiguration>
                    {
                        new RepoConfiguration("azure/azure-sdk-tools")
                        {
                            Branches = new List<string>() { "integration/assets-test-branch" }
                        },
                        new RepoConfiguration("azure/azure-sdk-assets-integration")
                        {
                            Branches = new List<string>()
                            {
                                "integration/assets-branch-1",
                                "integration/assets-branch-2"
                            }
                        }
                    }
                };
            }
        }

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
        [GitTokenSkip]
        public void TestBasicScanSingleBranch()
        {
            var scanner = new AssetsScanner(TestDirectory);
            var config = RunConfiguration;
            config.Repos.RemoveAt(0);
            config.Repos.First().Branches.RemoveAt(1);
            var results = scanner.Scan(config);

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
        [GitTokenSkip]
        public void TestBasicScanMultipleBranches()
        {
            var scanner = new AssetsScanner(TestDirectory);
            var config = RunConfiguration;
            config.Repos.RemoveAt(0);
            var results = scanner.Scan(config);

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
        [GitTokenSkip]
        public void TestBasicScanMultipleBranchesMultipleRepos()
        {
            var scanner = new AssetsScanner(TestDirectory);
            var config = RunConfiguration;
            var results = scanner.Scan(config);

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
        [GitTokenSkip]
        public void TestScanHonorsPreviousResults()
        {
            var specificDirectory = Path.Combine(TestDirectory, "TestResources", "basic_output");
            var scanner = new AssetsScanner(specificDirectory);

            var config = RunConfiguration;
            var resultSet = scanner.Scan(config);
            var scanDate = DateTime.UtcNow.AddSeconds(-10);
            Assert.That(resultSet, Is.Not.Null);

            // The only elements that _could_ change (given that git is immutable per commit)
            // are the BackupURIs and the ScanDates. If we have properly loaded previous results,
            // these should align.
            var resultsForCommit48b = resultSet.ByOriginSHA["48bca526a2a9972e4219ec87d29a7aa31438581a"];
            var resultsForf1 = resultSet.ByOriginSHA["f139c4ddf7aaa4d637282ae7da4466b473044281"];
            var resultsFore6 = resultSet.ByOriginSHA["eeeee9e00cc0d0111edf7471962b0da826d9a5cc"];

            Assert.That(resultsForCommit48b[0].BackupUri, Is.Null);
            Assert.That(resultsForCommit48b[0].ScanDate, Is.EqualTo(DateTime.Parse("2023-05-11T11:05:59")));
            Assert.That(resultsForCommit48b[1].BackupUri, Is.Null);
            Assert.That(resultsForCommit48b[1].ScanDate, Is.EqualTo(DateTime.Parse("2023-05-10T00:00:00")));
            Assert.That(resultsForCommit48b.Count(), Is.EqualTo(2));

            Assert.That(resultsForf1[0].BackupUri, Is.EqualTo("https://github.com/azure-sdk/azure-sdk-assets/tree/backup_js/agrifood/arm-agrifood_4f244d09c7"));
            Assert.That(resultsForf1[0].ScanDate, Is.EqualTo(DateTime.Parse("2023-05-08T00:00:00")));
            Assert.That(resultsForf1.Count(), Is.EqualTo(1));

            Assert.That(resultsFore6[0].AssetsLocation, Is.EqualTo("sdk/formrecognizer/azure-ai-formrecognizer/assets.json"));
            Assert.That(resultsFore6[0].Commit, Is.EqualTo("eeeee9e00cc0d0111edf7471962b0da826d9a5cc"));
            Assert.That(resultsFore6[0].Repo, Is.EqualTo("azure/azure-sdk-tools"));
            Assert.That(resultsFore6[0].Tag, Is.EqualTo("python/formrecognizer/azure-ai-formrecognizer_f60081bf10"));
            Assert.That(resultsFore6[0].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));
            Assert.That(resultsFore6[0].ScanDate, Is.GreaterThanOrEqualTo(scanDate));

            Assert.That(resultsFore6[1].AssetsLocation, Is.EqualTo("sdk/keyvault/assets.json"));
            Assert.That(resultsFore6[1].Commit, Is.EqualTo("eeeee9e00cc0d0111edf7471962b0da826d9a5cc"));
            Assert.That(resultsFore6[1].Repo, Is.EqualTo("azure/azure-sdk-tools"));
            Assert.That(resultsFore6[1].Tag, Is.EqualTo("python/keyvault/azure-keyvault-administration_f6e776f55f"));
            Assert.That(resultsFore6[1].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));
            Assert.That(resultsFore6[1].ScanDate, Is.GreaterThanOrEqualTo(scanDate));
            Assert.That(resultsFore6.Count(), Is.EqualTo(2));
        }

        [Test]
        [GitTokenSkip]
        public void TestParsePreviouslyOutputResults()
        {
            var specificDirectory = Path.Combine(TestDirectory, "TestResources", "basic_output");
            var scanner = new AssetsScanner(specificDirectory);

            // ensure that we can parse a default set of existing results
            var resultSet = scanner.ParseExistingResults();

            Assert.That(resultSet, Is.Not.Null);

            Assert.That(resultSet.Results[0].AssetsLocation, Is.EqualTo("sdk/agrifood/arm-agrifood/assets.json"));
            Assert.That(resultSet.Results[0].Commit, Is.EqualTo("48bca526a2a9972e4219ec87d29a7aa31438581a"));
            Assert.That(resultSet.Results[0].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(resultSet.Results[0].Tag, Is.EqualTo("js/agrifood/arm-agrifood_4f244d09c7"));
            Assert.That(resultSet.Results[0].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));
            Assert.That(resultSet.Results[0].BackupUri, Is.Null);
            Assert.That(resultSet.Results[0].ScanDate, Is.EqualTo(DateTime.Parse("2023-05-11T11:05:59")));

            Assert.That(resultSet.Results[1].AssetsLocation, Is.EqualTo("sdk/storage/storage-blob/assets.json"));
            Assert.That(resultSet.Results[1].Commit, Is.EqualTo("48bca526a2a9972e4219ec87d29a7aa31438581a"));
            Assert.That(resultSet.Results[1].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(resultSet.Results[1].Tag, Is.EqualTo("js/storage/storage-blob_5d5a32b74a"));
            Assert.That(resultSet.Results[1].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));
            Assert.That(resultSet.Results[1].BackupUri, Is.Null);
            Assert.That(resultSet.Results[1].ScanDate, Is.EqualTo(DateTime.Parse("2023-05-10T00:00:00")));

            Assert.That(resultSet.Results[2].AssetsLocation, Is.EqualTo("sdk/agrifood/arm-agrifood/assets.json"));
            Assert.That(resultSet.Results[2].Commit, Is.EqualTo("f139c4ddf7aaa4d637282ae7da4466b473044281"));
            Assert.That(resultSet.Results[2].Repo, Is.EqualTo("azure/azure-sdk-assets-integration"));
            Assert.That(resultSet.Results[2].Tag, Is.EqualTo("js/agrifood/arm-agrifood_4f244d09c7"));
            Assert.That(resultSet.Results[2].TagRepo, Is.EqualTo("Azure/azure-sdk-assets"));
            Assert.That(resultSet.Results[2].BackupUri, Is.EqualTo("https://github.com/azure-sdk/azure-sdk-assets/tree/backup_js/agrifood/arm-agrifood_4f244d09c7"));
            Assert.That(resultSet.Results[2].ScanDate, Is.EqualTo(DateTime.Parse("2023-05-08T00:00:00")));
        }

        [Test]
        [GitTokenSkip]
        public void TestScanOutputsResults()
        {
            var scanner = new AssetsScanner(TestDirectory);
            var config = RunConfiguration;
            config.Repos.RemoveAt(0);
            config.Repos.First().Branches.RemoveAt(1);
            var results = scanner.Scan(config);

            // now we need to confirm that the output file exists
            var fileThatShouldExist = Path.Combine(TestDirectory, "output.json");

            Assert.That(File.Exists(fileThatShouldExist), Is.EqualTo(true));

            var parsedNewResults = scanner.ParseExistingResults();

            if (parsedNewResults != null)
            {
                AreResultsSame(results, parsedNewResults);
            }
            else
            {
                Assert.NotNull(parsedNewResults);
            }
        }

        private bool AreResultsSame(AssetsResultSet a, AssetsResultSet b)
        {
            if (a.Results.Count() != b.Results.Count())
            {
                return false;
            }

            for (int i = 0; i < a.Results.Count(); i++)
            {
                AssetsResult aResult = a.Results[i];
                AssetsResult bResult = b.Results[i];

                Assert.That(bResult.ScanDate, Is.EqualTo(aResult.ScanDate));
                Assert.That(bResult.AssetsLocation, Is.EqualTo(aResult.AssetsLocation));
                Assert.That(bResult.Tag, Is.EqualTo(aResult.Tag));
                Assert.That(bResult.TagRepo, Is.EqualTo(aResult.TagRepo));
                Assert.That(bResult.Commit, Is.EqualTo(aResult.Commit));
                Assert.That(bResult.Repo, Is.EqualTo(aResult.Repo));
                Assert.That(bResult.BackupUri, Is.EqualTo(aResult.BackupUri));
            }

            return true;
        }
    }
}
