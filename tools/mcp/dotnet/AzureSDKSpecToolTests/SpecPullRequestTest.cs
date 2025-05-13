using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureSDKDevToolsMCP.Helpers;
using AzureSDKDevToolsMCP.Services;
using AzureSDKDSpecTools.Helpers;
using AzureSDKDSpecTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureSDKSpecToolTests
{
    public class SpecPullRequestTest
    {
        private ISpecPullRequestHelper specHelper;
        private IGitHelper gitHelper;
        private IGitHubService githubService;
        public SpecPullRequestTest()
        {

            var serviceProvider = new ServiceCollection()
             .AddLogging(configure => configure.AddConsole())
             .BuildServiceProvider();
            var specLogger = serviceProvider.GetService<ILogger<SpecPullRequestHelper>>();
            var gitLogger = serviceProvider.GetService<ILogger<GitHelper>>();
            var githubServiceLogger = serviceProvider.GetService<ILogger<GitHubService>>();
            githubService = new GitHubService(githubServiceLogger);
            var gitHelper = new GitHelper(githubService, gitLogger);
            specHelper = new SpecPullRequestHelper(specLogger, gitHelper);
        }

        [Fact]
        public void TestFindApiviewLinks()
        {
            var comments = """
                                ## API Change Check

                APIView identified API level changes in this PR and created the following API reviews

                | Language | API Review for Package |
                |----------|---------|
                | Swagger | [Microsoft.Migrate-AssessmentProjects](https://apiview.dev/Assemblies/Review/5626c72c559b4933a6d3a7e473462853?revisionId=9f31fffd3920476f835cc7967522e25f) |
                | TypeSpec | [Microsoft.Migrate](https://spa.apiview.dev/review/ce4a22eff33f4f22bbc2c0a06738aab9?activeApiRevisionId=112613c6b87c40b7a5ac7eadf8349a96) |
                | JavaScript | [@azure/arm-migrate](https://spa.apiview.dev/review/a6c0df0710bd494da97b9684677a0996?activeApiRevisionId=27eeb0ab7b30446f999b0f2feedc4b43) |
                | Java | [com.azure.resourcemanager:azure-resourcemanager-migrate](https://spa.apiview.dev/review/2564bfad86e74e16bd3c13f52c1cd084?activeApiRevisionId=c12554d2a9f044e7b06c4da452b44cae) |
                | Python | [azure-mgmt-migrate](https://spa.apiview.dev/review/18d1539b86d843f69a51d69c27b20b73?activeApiRevisionId=eb51d67fd13b4006887cbdd5fa1df483) |
                <!-- Fetch URI: https://apiview.dev/api/pullrequests?pullRequestNumber=34163&repoName=Azure/azure-rest-api-specs&commitSHA=c78bdc0c286aa7c24389c4c44464f24f8db69289 -->
                """;
            var apiLinks = specHelper.FindApiReviewLinks([comments]);
            Assert.Equal(5, apiLinks.Count);
        }
    }
}
