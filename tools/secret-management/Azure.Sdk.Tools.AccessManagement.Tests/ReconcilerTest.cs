using System.Reflection;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;

namespace Azure.Sdk.Tools.AccessManagement.Tests;

public class ReconcilerTest
{
    ApplicationCollectionResponse AppResult { get; set; } = default!;
    Application TestApplication { get; set; } = default!;
    ServicePrincipal TestServicePrincipal { get; set; } = default!;
    Mock<IGraphClient> GraphClientMock { get; set; } = default!;
    Mock<IRbacClient> RbacClientMock { get; set; } = default!;
    Mock<IGitHubClient> GitHubClientMock { get; set; } = default!;

    AccessConfig NoGitHubAccessConfig { get; set; } = default!;
    AccessConfig FederatedCredentialsOnlyConfig { get; set; } = default!;
    AccessConfig TemplateAccessConfig { get; set; } = default!;
    AccessConfig GithubAccessConfig { get; set; } = default!;
    AccessConfig RbacOnlyConfig { get; set; } = default!;
    AccessConfig FullAccessConfig { get; set; } = default!;
    AccessConfig MultiAccessConfig { get; set; } = default!;
    AccessConfig TemplateMissingPropertyAccessConfig { get; set; } = default!;

    [OneTimeSetUp]
    public void Before()
    {
        NoGitHubAccessConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{"./test-configs/no-github-access-config.json"});
        FederatedCredentialsOnlyConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{"./test-configs/federated-credentials-only-config.json"});
        TemplateAccessConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{"./test-configs/access-config-template.json"});
        GithubAccessConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{"./test-configs/github-only-config.json"});
        RbacOnlyConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{"./test-configs/rbac-only-config.json"});
        FullAccessConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{"./test-configs/full-access-config.json"});
        MultiAccessConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{
            "./test-configs/rbac-only-config.json",
            "./test-configs/github-only-config.json",
            "./test-configs/federated-credentials-only-config.json",
        });
        TemplateMissingPropertyAccessConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{
            "./test-configs/access-config-template-missing-1.json",
            "./test-configs/access-config-template-missing-2.json",
        });
    }

    [SetUp]
    public void BeforeEach()
    {
        GraphClientMock = new Mock<IGraphClient>();
        RbacClientMock = new Mock<IRbacClient>();
        GitHubClientMock = new Mock<IGitHubClient>();

        TestApplication = new Application
        {
            DisplayName = "",
            AppId = "00000000-0000-0000-0000-000000000000",
            Id = "00000000-0000-0000-0000-000000000000",
        };

        TestServicePrincipal = new ServicePrincipal
        {
            DisplayName = "",
            AppId = TestApplication.AppId,
            Id = "00000000-0000-0000-0000-000000000000",
        };
    }

    [Test]
    public void TestReconcileWithTemplate()
    {
        TemplateAccessConfig.Configs.Count().Should().Be(1);
        TemplateAccessConfig.Configs.First().ApplicationAccessConfig.RoleBasedAccessControls.First().Scope
            .Should().Be("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-testfoobaraccessmanager");

        TemplateAccessConfig.Configs.First().ApplicationAccessConfig.FederatedIdentityCredentials.First().Subject
            .Should().Be("repo:testfoobaraccessmanager/azure-sdk-tools:ref:refs/heads/main");
    }

    [Test]
    public async Task TestReconcileWithTemplateMissingProperties()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), GraphClientMock.Object, RbacClientMock.Object, GitHubClientMock.Object);
        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns(TestApplication);
        GraphClientMock.Setup(c => c.GetApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns(TestServicePrincipal);
        GraphClientMock.Setup(c => c.ListFederatedIdentityCredentials(It.IsAny<Application>()).Result).Returns(new List<FederatedIdentityCredential>());

        Func<Task> func = async () => await reconciler.Reconcile(TemplateMissingPropertyAccessConfig);
        await func.Should().ThrowAsync<AggregateException>();
    }

    [Test]
    public async Task TestReconcileWithGithubSecrets()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), GraphClientMock.Object, RbacClientMock.Object, GitHubClientMock.Object);

        GitHubClientMock.Setup(c => c.SetRepositorySecret(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await reconciler.ReconcileGithubRepositorySecrets(TestApplication, GithubAccessConfig.Configs.First().ApplicationAccessConfig);

        var config = GithubAccessConfig.Configs.First().ApplicationAccessConfig;

        foreach (var secret in config.GithubRepositorySecrets.First().Secrets)
        {
            GitHubClientMock.Verify(c => c.SetRepositorySecret(
                "testfoobaraccessmanager", "azure-sdk-test-foobar", secret.Key, secret.Value), Times.Exactly(1));
            GitHubClientMock.Verify(c => c.SetRepositorySecret(
                "testfoobaraccessmanager-fork", "azure-sdk-test-foobar", secret.Key, secret.Value), Times.Exactly(1));
        }

        foreach (var secret in config.GithubRepositorySecrets.ElementAt(1).Secrets)
        {
            GitHubClientMock.Verify(c => c.SetRepositorySecret(
                "microsoft-testfoobaraccessmanager", "azure-sdk-test-baz", secret.Key, secret.Value), Times.Exactly(1));
        }

        GitHubClientMock.Verify(c => c.SetRepositorySecret(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(9));
    }

    [Test]
    public async Task TestReconcileWithExistingApp()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), GraphClientMock.Object, RbacClientMock.Object, GitHubClientMock.Object);
        TestApplication.DisplayName = NoGitHubAccessConfig.Configs.First().ApplicationAccessConfig.AppDisplayName;
        TestServicePrincipal.DisplayName = TestApplication.DisplayName;

        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns(TestApplication);
        GraphClientMock.Setup(c => c.GetApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns(TestServicePrincipal);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>())).Throws(new Exception("Application should not be created"));
        GraphClientMock.Setup(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>())).Throws(new Exception("Service Principal should not be created"));

        var (app, servicePrincipal) = await reconciler.ReconcileApplication(NoGitHubAccessConfig.Configs.First().ApplicationAccessConfig);
        app.DisplayName.Should().Be(TestApplication.DisplayName);
        app.AppId.Should().Be(TestApplication.AppId);
        app.Id.Should().Be(TestApplication.Id);

        servicePrincipal.DisplayName.Should().Be(TestServicePrincipal.DisplayName);
        servicePrincipal.AppId.Should().Be(TestServicePrincipal.AppId);
    }

    [Test]
    public async Task TestReconcileWithNewApp()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), GraphClientMock.Object, RbacClientMock.Object, GitHubClientMock.Object);
        TestApplication.DisplayName = NoGitHubAccessConfig.Configs.First().ApplicationAccessConfig.AppDisplayName;
        TestServicePrincipal.DisplayName = TestApplication.DisplayName;

        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns<Application>(null);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>()).Result).Returns(TestApplication);
        GraphClientMock.Setup(c => c.GetApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns<ServicePrincipal>(null);
        GraphClientMock.Setup(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns(TestServicePrincipal);

        var (app, servicePrincipal) = await reconciler.ReconcileApplication(NoGitHubAccessConfig.Configs.First().ApplicationAccessConfig);
        app.DisplayName.Should().Be(TestApplication.DisplayName);
        app.AppId.Should().Be(TestApplication.AppId);
        app.Id.Should().Be(TestApplication.Id);
        servicePrincipal.DisplayName.Should().Be(TestServicePrincipal.DisplayName);
        servicePrincipal.AppId.Should().Be(TestServicePrincipal.AppId);
        servicePrincipal.Id.Should().Be(TestServicePrincipal.Id);
    }

    [Test]
    public async Task TestReconcileWithMissingServicePrincipal()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), GraphClientMock.Object, RbacClientMock.Object, GitHubClientMock.Object);
        TestApplication.DisplayName = NoGitHubAccessConfig.Configs.First().ApplicationAccessConfig.AppDisplayName;
        TestServicePrincipal.DisplayName = TestApplication.DisplayName;

        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns<Application>(null);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>()).Result).Returns(TestApplication);
        GraphClientMock.Setup(c => c.GetApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns<ServicePrincipal>(null);
        GraphClientMock.Setup(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns(TestServicePrincipal);

        var (app, servicePrincipal) = await reconciler.ReconcileApplication(NoGitHubAccessConfig.Configs.First().ApplicationAccessConfig);
        GraphClientMock.Verify(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>()), Times.Once);

        app.DisplayName.Should().Be(TestApplication.DisplayName);
        app.AppId.Should().Be(TestApplication.AppId);
        app.Id.Should().Be(TestApplication.Id);
        servicePrincipal.DisplayName.Should().Be(TestServicePrincipal.DisplayName);
        servicePrincipal.AppId.Should().Be(TestServicePrincipal.AppId);
        servicePrincipal.Id.Should().Be(TestServicePrincipal.Id);
    }

    [Test]
    public async Task TestReconcileWithEmptyFederatedIdentityCredentials()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), GraphClientMock.Object, RbacClientMock.Object, GitHubClientMock.Object);
        var configApp = FederatedCredentialsOnlyConfig.Configs.First().ApplicationAccessConfig;
        TestApplication.DisplayName = configApp.AppDisplayName;

        GraphClientMock.Setup(c => c.ListFederatedIdentityCredentials(It.IsAny<Application>()).Result).Returns(new List<FederatedIdentityCredential>());

        await reconciler.ReconcileFederatedIdentityCredentials(TestApplication, configApp);

        GraphClientMock.Verify(c => c.DeleteFederatedIdentityCredential(It.IsAny<Application>(), It.IsAny<FederatedIdentityCredential>()), Times.Never);
        GraphClientMock.Verify(
            c => c.CreateFederatedIdentityCredential(
                It.Is<Application>(a => a.DisplayName == configApp.AppDisplayName),
                It.IsAny<FederatedIdentityCredential>()), Times.Exactly(2));
    }

    [Test]
    public async Task TestReconcileMergingFederatedIdentityCredentials()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), GraphClientMock.Object, RbacClientMock.Object, GitHubClientMock.Object);
        var configApp = FederatedCredentialsOnlyConfig.Configs.First().ApplicationAccessConfig;
        TestApplication.DisplayName = configApp.AppDisplayName;

        var existingCredentials = new List<FederatedIdentityCredential>
        {
            configApp.FederatedIdentityCredentials.First(),
            new FederatedIdentityCredential(),
            new FederatedIdentityCredential
            {
                Audiences = new List<string> { "api://azureadtokenexchange" },
                Description = "Test PreExisting To Replace",
                Issuer = "https://token.actions.githubusercontent.com",
                Name = "test-pre-existing-replace-1",
                Subject = "repo:accessmanagertest/azure-sdk-tools:ref:refs/heads/main"
            },
        };

        GraphClientMock.Setup(c => c.ListFederatedIdentityCredentials(It.IsAny<Application>()).Result).Returns(existingCredentials);

        await reconciler.ReconcileFederatedIdentityCredentials(TestApplication, configApp);

        // Delete two, keep one, create one
        GraphClientMock.Verify(c => c.DeleteFederatedIdentityCredential(
            It.IsAny<Application>(), It.IsAny<FederatedIdentityCredential>()), Times.Exactly(2));
        GraphClientMock.Verify(c => c.CreateFederatedIdentityCredential(
            It.Is<Application>(a => a.DisplayName == configApp.AppDisplayName),
            It.IsAny<FederatedIdentityCredential>()), Times.Once);
    }

    [Test]
    public async Task TestReconcileRoleBasedAccessControl()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), GraphClientMock.Object, RbacClientMock.Object, GitHubClientMock.Object);
        var configApp = RbacOnlyConfig.Configs.First().ApplicationAccessConfig;
        TestApplication.DisplayName = configApp.AppDisplayName;
        TestServicePrincipal.DisplayName = configApp.AppDisplayName;

        await reconciler.ReconcileRoleBasedAccessControls(TestServicePrincipal, configApp);

        RbacClientMock.Verify(c => c.CreateRoleAssignment(
            It.IsAny<ServicePrincipal>(), It.IsAny<RoleBasedAccessControlsConfig>()), Times.Exactly(3));
    }

    [Test]
    [TestCase("FullAccessConfig", 1, 1, 3, 1, 0, 3, 2)]
    [TestCase("MultiAccessConfig", 3, 3, 9, 2, 0, 2, 3)]
    public async Task TestReconcileWithMultipleConfigs(
        string accessConfigName,
        int createAppCount,
        int createServicePrincipalCount,
        int setRepositorySecretCount,
        int setClientIdCount,
        int deleteFederatedIdentityCredentialCount,
        int createFederatedIdentityCredentialCount,
        int createRoleAssignmentCount
    ) {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), GraphClientMock.Object, RbacClientMock.Object, GitHubClientMock.Object);
        TestApplication.DisplayName = "access-config-multi-test";
        TestServicePrincipal.DisplayName = TestApplication.DisplayName;
        // Override AppId to ensure we inject it into downstream properties and rendered template values
        TestApplication.AppId = "11111111-1111-1111-1111-111111111111";
        var prop = this.GetType().GetProperty(accessConfigName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop?.GetValue(this) is not AccessConfig)
        {
            Assert.Fail($"Access config {accessConfigName} not found.");
        }

        var accessConfig = prop!.GetValue(this) as AccessConfig;
        foreach (var cfg in accessConfig!.Configs)
        {
            cfg.ApplicationAccessConfig.AppDisplayName = TestApplication.DisplayName;
        }

        // Application mocks
        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns<Application>(null);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>()).Result).Returns(TestApplication);
        GraphClientMock.Setup(c => c.GetApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns<ServicePrincipal>(null);
        GraphClientMock.Setup(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns(TestServicePrincipal);
        // GitHub mocks
        GitHubClientMock.Setup(c => c.SetRepositorySecret(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        // Federated Identity Credential mocks
        GraphClientMock.Setup(c => c.ListFederatedIdentityCredentials(
            It.IsAny<Application>()).Result).Returns(new List<FederatedIdentityCredential>());

        await reconciler.Reconcile(accessConfig);

        // Create application and service principal
        GraphClientMock.Verify(c => c.CreateApplication(It.IsAny<Application>()), Times.Exactly(createAppCount));
        GraphClientMock.Verify(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>()), Times.Exactly(createServicePrincipalCount));

        // Create repository secrets
        GitHubClientMock.Verify(c => c.SetRepositorySecret(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(setRepositorySecretCount));
        GitHubClientMock.Verify(c => c.SetRepositorySecret(
            It.IsAny<string>(), It.IsAny<string>(), "AZURE_CLIENT_ID", TestApplication.AppId), Times.Exactly(setClientIdCount));

        // Delete zero, keep zero, create three
        GraphClientMock.Verify(c => c.DeleteFederatedIdentityCredential(
            It.IsAny<Application>(), It.IsAny<FederatedIdentityCredential>()), Times.Exactly(deleteFederatedIdentityCredentialCount));
        GraphClientMock.Verify(c => c.CreateFederatedIdentityCredential(
            It.Is<Application>(a => a.DisplayName == TestApplication.DisplayName),
            It.IsAny<FederatedIdentityCredential>()), Times.Exactly(createFederatedIdentityCredentialCount));

        // Create 2
        RbacClientMock.Verify(c => c.CreateRoleAssignment(
            It.IsAny<ServicePrincipal>(), It.IsAny<RoleBasedAccessControlsConfig>()), Times.Exactly(createRoleAssignmentCount));
    }
}
