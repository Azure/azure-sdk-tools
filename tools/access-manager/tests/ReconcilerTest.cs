namespace Azure.Sdk.Tools.AccessManager.Tests;

using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.Graph.Models;

public class ReconcilerTest
{
    ApplicationCollectionResponse AppResult { get; set; }
    AccessConfig BaseAccessConfig { get; set; }
    AccessConfig FederatedCredentialsOnlyConfig { get; set; }
    AccessConfig RbacOnlyConfig { get; set; }
    Mock<IGraphClient> GraphClientMock { get; set; }
    Mock<IRbacClient> RbacClientMock { get; set; }
    Application TestApplication { get; set; }
    ServicePrincipal TestServicePrincipal { get; set; }

    [OneTimeSetUp]
    public void Before()
    {
        BaseAccessConfig = AccessConfig.Create("./test-configs/access-config.json");
        FederatedCredentialsOnlyConfig = AccessConfig.Create("./test-configs/federated-credentials-only-config.json");
        RbacOnlyConfig = AccessConfig.Create("./test-configs/rbac-only-config.json");
    }

    [SetUp]
    public void BeforeEach()
    {
        GraphClientMock = new Mock<IGraphClient>();
        RbacClientMock = new Mock<IRbacClient>();

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
    public async Task TestReconcileWithExistingApp()
    {
        var reconciler = new Reconciler(GraphClientMock.Object, RbacClientMock.Object);
        TestApplication.DisplayName = BaseAccessConfig.ApplicationAccessConfigs.First().AppDisplayName;
        TestServicePrincipal.DisplayName = TestApplication.DisplayName;

        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns(TestApplication);
        GraphClientMock.Setup(c => c.GetApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns(TestServicePrincipal);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>())).Throws(new Exception("Application should not be created"));
        GraphClientMock.Setup(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>())).Throws(new Exception("Service Principal should not be created"));

        var (app, servicePrincipal) = await reconciler.ReconcileApplication(BaseAccessConfig.ApplicationAccessConfigs.First());
        app.DisplayName.Should().Be(TestApplication.DisplayName);
        app.AppId.Should().Be(TestApplication.AppId);
        app.Id.Should().Be(TestApplication.Id);

        servicePrincipal.DisplayName.Should().Be(TestServicePrincipal.DisplayName);
        servicePrincipal.AppId.Should().Be(TestServicePrincipal.AppId);
    }

    [Test]
    public async Task TestReconcileWithNewApp()
    {
        var reconciler = new Reconciler(GraphClientMock.Object, RbacClientMock.Object);
        TestApplication.DisplayName = BaseAccessConfig.ApplicationAccessConfigs.First().AppDisplayName;
        TestServicePrincipal.DisplayName = TestApplication.DisplayName;

        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns<Application>(null);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>()).Result).Returns(TestApplication);
        GraphClientMock.Setup(c => c.GetApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns<ServicePrincipal>(null);
        GraphClientMock.Setup(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns(TestServicePrincipal);

        var (app, servicePrincipal) = await reconciler.ReconcileApplication(BaseAccessConfig.ApplicationAccessConfigs.First());
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
        var reconciler = new Reconciler(GraphClientMock.Object, RbacClientMock.Object);
        TestApplication.DisplayName = BaseAccessConfig.ApplicationAccessConfigs.First().AppDisplayName;
        TestServicePrincipal.DisplayName = TestApplication.DisplayName;

        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns<Application>(null);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>()).Result).Returns(TestApplication);
        GraphClientMock.Setup(c => c.GetApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns<ServicePrincipal>(null);
        GraphClientMock.Setup(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns(TestServicePrincipal);

        var (app, servicePrincipal) = await reconciler.ReconcileApplication(BaseAccessConfig.ApplicationAccessConfigs.First());
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
        var reconciler = new Reconciler(GraphClientMock.Object, RbacClientMock.Object);
        var configApp = FederatedCredentialsOnlyConfig.ApplicationAccessConfigs.First();
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
        var reconciler = new Reconciler(GraphClientMock.Object, RbacClientMock.Object);
        var configApp = FederatedCredentialsOnlyConfig.ApplicationAccessConfigs.First();
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
        var reconciler = new Reconciler(GraphClientMock.Object, RbacClientMock.Object);
        var configApp = RbacOnlyConfig.ApplicationAccessConfigs.First();
        TestApplication.DisplayName = configApp.AppDisplayName;
        TestServicePrincipal.DisplayName = configApp.AppDisplayName;

        await reconciler.ReconcileRoleBasedAccessControls(TestServicePrincipal, configApp);

        RbacClientMock.Verify(c => c.CreateRoleAssignment(
            It.IsAny<ServicePrincipal>(), It.IsAny<RoleBasedAccessControl>()), Times.Exactly(3));
    }

    [Test]
    public async Task TestReconcileFromEmpty()
    {
        var reconciler = new Reconciler(GraphClientMock.Object, RbacClientMock.Object);
        var configApp = BaseAccessConfig.ApplicationAccessConfigs.First();
        TestApplication.DisplayName = configApp.AppDisplayName;
        TestServicePrincipal.DisplayName = configApp.AppDisplayName;

        // Application mocks
        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns<Application>(null);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>()).Result).Returns(TestApplication);
        GraphClientMock.Setup(c => c.GetApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns<ServicePrincipal>(null);
        GraphClientMock.Setup(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>()).Result).Returns(TestServicePrincipal);
        // Federated Identity Credential mocks
        GraphClientMock.Setup(c => c.ListFederatedIdentityCredentials(
            It.IsAny<Application>()).Result).Returns(new List<FederatedIdentityCredential>());

        await reconciler.Reconcile(BaseAccessConfig);

        // Create application and service principal
        GraphClientMock.Verify(c => c.CreateApplication(It.IsAny<Application>()), Times.Once);
        GraphClientMock.Verify(c => c.CreateApplicationServicePrincipal(It.IsAny<Application>()), Times.Once);

        // Delete zero, keep zero, create three
        GraphClientMock.Verify(c => c.DeleteFederatedIdentityCredential(
            It.IsAny<Application>(), It.IsAny<FederatedIdentityCredential>()), Times.Never);
        GraphClientMock.Verify(c => c.CreateFederatedIdentityCredential(
            It.Is<Application>(a => a.DisplayName == configApp.AppDisplayName),
            It.IsAny<FederatedIdentityCredential>()), Times.Exactly(3));

        // Create 2
        RbacClientMock.Verify(c => c.CreateRoleAssignment(
            It.IsAny<ServicePrincipal>(), It.IsAny<RoleBasedAccessControl>()), Times.Exactly(2));
    }
}
