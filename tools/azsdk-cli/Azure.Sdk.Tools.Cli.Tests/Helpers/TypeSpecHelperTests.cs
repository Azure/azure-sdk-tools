using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    internal class TypeSpecHelperTests
    {
        private ITypeSpecHelper typeSpecHelper;
        private IGitHelper gitHelper;
        private Mock<IGitHubService> gitHubService;


        [SetUp]
        public void setup()
        {
            var logger = new TestLogger<GitHelper>();
            gitHubService = new Mock<IGitHubService>();
            gitHelper = new GitHelper(gitHubService.Object, logger);
            typeSpecHelper = new TypeSpecHelper(gitHelper);
        }

        [Test]
        public void Verify_IsValidTypeSpecProject()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.IsValidTypeSpecProjectPath(testCodeFilePath);
            Assert.That(result, Is.True);
            testCodeFilePath = "TypeSpecTestData/specification/testcontoso";
            result = typeSpecHelper.IsValidTypeSpecProjectPath(testCodeFilePath);
            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_IsTypeSpecProjectForMgmtPlane()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(testCodeFilePath);
            Assert.That(result, Is.True);
        }

        [Test]
        public void Test_GetSpecRepoPath()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.GetSpecRepoRootPath(testCodeFilePath);
            Assert.That(result.EndsWith("TypeSpecTestData"), Is.True);
        }

    }
}
