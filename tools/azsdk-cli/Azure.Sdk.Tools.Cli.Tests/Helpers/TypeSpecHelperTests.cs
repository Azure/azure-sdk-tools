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

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/feature-branch/specification/contoso/Contoso.Management")]
        [TestCase("https://github.com/myorg/azure-rest-api-specs/blob/main/specification/test/Test.Service")]
        [Test]
        public void Verify_IsValidTypeSpecProject_WithUrls(string url)
        {
            var result = typeSpecHelper.IsValidTypeSpecProjectPath(url);
            Assert.That(result, Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr/blob/main/specification/test/Test.Service")]
        [TestCase("https://github.com/Azure/wrong-repo/blob/main/specification/test/Test.Service")]
        [TestCase("https://example.com/specification/test/Test.Service")]
        [TestCase("not-a-url")]
        [Test]
        public void Verify_IsValidTypeSpecProject_WithInvalidUrls(string url)
        {
            var result = typeSpecHelper.IsValidTypeSpecProjectPath(url);
            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_IsTypeSpecProjectForMgmtPlane()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(testCodeFilePath);
            Assert.That(result, Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contoso/resource-manager/Contoso.Service")]
        [Test]
        public void Verify_IsTypeSpecProjectForMgmtPlane_WithUrls(string url)
        {
            var result = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(url);
            Assert.That(result, Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contoso/Contoso.DataPlane")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/test/Test.Service")]
        [Test]
        public void Verify_IsTypeSpecProjectForMgmtPlane_WithDataPlaneUrls(string url)
        {
            var result = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(url);
            Assert.That(result, Is.False);
        }

        [Test]
        public void Test_GetSpecRepoPath()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.GetSpecRepoRootPath(testCodeFilePath);
            Assert.That(result.EndsWith("TypeSpecTestData"), Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management")]
        [Test]
        public void Test_GetSpecRepoPath_WithUrl(string url)
        {
            var result = typeSpecHelper.GetSpecRepoRootPath(url);
            Assert.That(result, Is.EqualTo(url));
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management", "specification/dell/Dell.Storage.Management")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/feature/specification/contoso/Contoso.Service", "specification/contoso/Contoso.Service")]
        [Test]
        public void Test_GetTypeSpecProjectRelativePath_WithUrl(string url, string expected)
        {
            var result = typeSpecHelper.GetTypeSpecProjectRelativePath(url);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs.git")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs")]
        [TestCase("https://github.com/myuser/azure-rest-api-specs.git")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr.git")]
        [TestCase("https://github.com/myuser/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:Azure/azure-rest-api-specs.git")]
        [TestCase("git@github.com:myuser/azure-rest-api-specs.git")]
        [Test]
        public void Test_IsRepoPathForSpecRepo(Uri repo)
        {
            var gitHelper = CreateGitHelper(repo);
            var helper = new TypeSpecHelper(gitHelper);
            Assert.That(helper.IsRepoPathForSpecRepo("unused because of mock"), "is a specs repo (public or private)");
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr.git")]
        [TestCase("https://github.com/myuser/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:Azure/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:myuser/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:Azure/azure-sdk-for-php.git")]
        [Test]
        public void Test_IsRepoPathForPublicSpecRepo(Uri repo)
        {
            var helper = new TypeSpecHelper(CreateGitHelper(repo));
            Assert.That(!helper.IsRepoPathForPublicSpecRepo("unused because of the mock"), "not the public specs repo");
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management")]
        [TestCase("https://github.com/myorg/azure-rest-api-specs/blob/main/specification/test/Test.Service")]
        [Test]
        public void Test_IsRepoPathForPublicSpecRepo_WithValidUrls(string url)
        {
            var result = typeSpecHelper.IsRepoPathForPublicSpecRepo(url);
            Assert.That(result, Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr/blob/main/specification/test/Test.Service")]
        [TestCase("https://example.com/specification/test/Test.Service")]
        [Test]
        public void Test_IsRepoPathForPublicSpecRepo_WithInvalidUrls(string url)
        {
            var result = typeSpecHelper.IsRepoPathForPublicSpecRepo(url);
            Assert.That(result, Is.False);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs/blob/main/specification/dell/Dell.Storage.Management")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/tree/main/specification/contoso/Contoso.Service")]
        [TestCase("https://github.com/myorg/azure-rest-api-specs/blob/feature/specification/test/Test.Service")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr/blob/main/specification/test/Test.Service")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr/tree/main/specification/test/Test.Service")]
        [Test]
        public void Test_IsRepoPathForSpecRepo_WithValidUrls(string url)
        {
            var result = typeSpecHelper.IsRepoPathForSpecRepo(url);
            Assert.That(result, Is.True);
        }

        [TestCase("https://github.com/Azure/wrong-repo/blob/main/specification/test/Test.Service")]
        [TestCase("https://example.com/specification/test/Test.Service")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/main/specification/test")]  // missing blob/tree
        [Test]
        public void Test_IsRepoPathForSpecRepo_WithInvalidUrls(string url)
        {
            var result = typeSpecHelper.IsRepoPathForSpecRepo(url);
            Assert.That(result, Is.False);
        }

        private static IGitHelper CreateGitHelper(Uri getRepoRemoteUri)
        {
            var gitHelperMock = new Mock<IGitHelper>();
            gitHelperMock.Setup(ghm => ghm.GetRepoRemoteUri(It.IsAny<string>())).Returns(getRepoRemoteUri);
            return gitHelperMock.Object;
        }
    }
}
