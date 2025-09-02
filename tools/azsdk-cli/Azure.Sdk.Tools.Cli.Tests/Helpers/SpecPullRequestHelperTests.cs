using System.Resources;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Octokit;
using static System.Net.WebRequestMethods;
using static Microsoft.Azure.Pipelines.WebApi.PipelinesResources;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    internal class SpecPullRequestHelperTests
    {
        private ISpecPullRequestHelper specPullRequestHelper;

        [SetUp]
        public void setup()
        {
            var logger = new TestLogger<SpecPullRequestHelper>();
            specPullRequestHelper = new SpecPullRequestHelper(logger);
        }

        [Test]
        public void Verify_FindApiReviewLinks()
        {

            List<string> comments = ["""            
                ## API Change Check
            

                APIView identified API level changes in this PR and created the following API reviews

                | Language | API Review for Package |
                | ----------| ---------|
                | Go | [sdk / resourcemanager / purestorage / armpurestorage](https://test.dev/review/8389fe) |
                | C# | [Azure.ResourceManager.PureStorage](https://test.dev/review/d0827e98c) |
                | Python | [azure - mgmt - purestorage](https://test.dev/review/5df3dbc5392) |
                | Java | [com.azure.resourcemanager:azure - resourcemanager - purestorage](https://spa.apiview.dev/review/c278c0) |
                | JavaScript | [@azure / arm - purestorage](https://test.dev/review/cbc56875a42e4) |
                < !--Fetch URI: https://apiview.dev/api/pullrequests?pullRequestNumber=35446&repoName=Azure/azure-rest-api-specs&commitSHA=4e6b1f2ee89eac9ae904b1035e4d207982315ce9
            
                """
             ];
            var result = specPullRequestHelper.FindApiReviewLinks(comments);
            Assert.That(result.Count, Is.EqualTo(5));
            Assert.That(result[0].Language, Is.EqualTo("Go"));
            Assert.That(result[0].PackageName, Is.EqualTo("sdk / resourcemanager / purestorage / armpurestorage"));
            Assert.That(result[0].ApiviewLink, Is.EqualTo("https://test.dev/review/8389fe"));
            Assert.That(result[1].Language, Is.EqualTo("C#"));
            Assert.That(result[1].PackageName, Is.EqualTo("Azure.ResourceManager.PureStorage"));
            Assert.That(result[1].ApiviewLink, Is.EqualTo("https://test.dev/review/d0827e98c"));
        }
    }
}
