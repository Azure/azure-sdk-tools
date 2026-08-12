using System.Reflection;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.AccessManagement.Tests;

public class ReconcilerTest
{
    ManagedIdentityInfo TestIdentityInfo { get; set; } = default!;
    Mock<IManagedIdentityClient> ManagedIdentityClientMock { get; set; } = default!;
    Mock<IRbacClient> RbacClientMock { get; set; } = default!;

    AccessConfig NoGitHubAccessConfig { get; set; } = default!;
    AccessConfig FederatedCredentialsOnlyConfig { get; set; } = default!;
    AccessConfig TemplateAccessConfig { get; set; } = default!;
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
        RbacOnlyConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{"./test-configs/rbac-only-config.json"});
        FullAccessConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{"./test-configs/full-access-config.json"});
        MultiAccessConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{
            "./test-configs/rbac-only-config.json",
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
        ManagedIdentityClientMock = new Mock<IManagedIdentityClient>();
        RbacClientMock = new Mock<IRbacClient>();

        TestIdentityInfo = new ManagedIdentityInfo(
            ClientId: Guid.Parse("00000000-0000-0000-0000-000000000000"),
            PrincipalId: Guid.Parse("00000000-0000-0000-0000-000000000000"),
            TenantId: Guid.Parse("00000000-0000-0000-0000-000000000000"));
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
        var reconciler = new Reconciler(Mock.Of<ILogger>(), ManagedIdentityClientMock.Object, RbacClientMock.Object);
        ManagedIdentityClientMock.Setup(c => c.GetManagedIdentity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(TestIdentityInfo);
        ManagedIdentityClientMock.Setup(c => c.ListFederatedIdentityCredentials(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<FederatedCredentialInfo>());

        Func<Task> func = async () => await reconciler.Reconcile(TemplateMissingPropertyAccessConfig, new ReconcileOptions());
        await func.Should().ThrowAsync<AggregateException>();
    }

    [Test]
    public async Task TestReconcileWithExistingIdentity()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), ManagedIdentityClientMock.Object, RbacClientMock.Object);

        ManagedIdentityClientMock.Setup(c => c.GetManagedIdentity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(TestIdentityInfo);
        ManagedIdentityClientMock.Setup(c => c.CreateManagedIdentity(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new Exception("Managed identity should not be created"));

        var identityInfo = await reconciler.ReconcileManagedIdentity(
            NoGitHubAccessConfig.Configs.First().ApplicationAccessConfig, new ReconcileOptions());

        identityInfo.Should().NotBeNull();
        identityInfo!.ClientId.Should().Be(TestIdentityInfo.ClientId);
        identityInfo.PrincipalId.Should().Be(TestIdentityInfo.PrincipalId);
    }

    [Test]
    public async Task TestReconcileWithNewIdentity()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), ManagedIdentityClientMock.Object, RbacClientMock.Object);

        ManagedIdentityClientMock.Setup(c => c.GetManagedIdentity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((ManagedIdentityInfo?)null);
        ManagedIdentityClientMock.Setup(c => c.CreateManagedIdentity(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(TestIdentityInfo);

        var identityInfo = await reconciler.ReconcileManagedIdentity(
            NoGitHubAccessConfig.Configs.First().ApplicationAccessConfig, new ReconcileOptions());

        identityInfo.Should().NotBeNull();
        identityInfo!.ClientId.Should().Be(TestIdentityInfo.ClientId);
        identityInfo.PrincipalId.Should().Be(TestIdentityInfo.PrincipalId);
        ManagedIdentityClientMock.Verify(c => c.CreateManagedIdentity(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task TestReconcileWithEmptyFederatedIdentityCredentials()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), ManagedIdentityClientMock.Object, RbacClientMock.Object);
        var configApp = FederatedCredentialsOnlyConfig.Configs.First().ApplicationAccessConfig;

        ManagedIdentityClientMock.Setup(c => c.ListFederatedIdentityCredentials(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<FederatedCredentialInfo>());

        await reconciler.ReconcileFederatedIdentityCredentials(configApp, new ReconcileOptions());

        ManagedIdentityClientMock.Verify(c => c.DeleteFederatedIdentityCredential(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        ManagedIdentityClientMock.Verify(
            c => c.CreateFederatedIdentityCredential(
                configApp.SubscriptionId!, configApp.ResourceGroup!, configApp.IdentityName!,
                It.IsAny<FederatedIdentityCredentialsConfig>()), Times.Exactly(2));
    }

    [Test]
    public async Task TestReconcileMergingFederatedIdentityCredentials()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), ManagedIdentityClientMock.Object, RbacClientMock.Object);
        var configApp = FederatedCredentialsOnlyConfig.Configs.First().ApplicationAccessConfig;

        var firstConfig = configApp.FederatedIdentityCredentials.First();
        var existingCredentials = new List<FederatedCredentialInfo>
        {
            new FederatedCredentialInfo(firstConfig.Name!, firstConfig.Issuer!, firstConfig.Subject!, firstConfig.Audiences!),
            new FederatedCredentialInfo("extra-empty", "", "", new List<string>()),
            new FederatedCredentialInfo(
                "test-pre-existing-replace-1",
                "https://token.actions.githubusercontent.com",
                "repo:accessmanagertest/azure-sdk-tools:ref:refs/heads/main",
                new List<string> { "api://azureadtokenexchange" }),
        };

        ManagedIdentityClientMock.Setup(c => c.ListFederatedIdentityCredentials(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(existingCredentials);

        await reconciler.ReconcileFederatedIdentityCredentials(configApp, new ReconcileOptions());

        // Delete two, keep one, create one
        ManagedIdentityClientMock.Verify(c => c.DeleteFederatedIdentityCredential(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
        ManagedIdentityClientMock.Verify(c => c.CreateFederatedIdentityCredential(
            configApp.SubscriptionId!, configApp.ResourceGroup!, configApp.IdentityName!,
            It.IsAny<FederatedIdentityCredentialsConfig>()), Times.Once);
    }

    [Test]
    public async Task TestReconcileRoleBasedAccessControl()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), ManagedIdentityClientMock.Object, RbacClientMock.Object);
        var configApp = RbacOnlyConfig.Configs.First().ApplicationAccessConfig;

        await reconciler.ReconcileRoleBasedAccessControls(TestIdentityInfo.PrincipalId, configApp, new ReconcileOptions());

        RbacClientMock.Verify(c => c.CreateRoleAssignment(
            It.IsAny<Guid>(), It.IsAny<RoleBasedAccessControlsConfig>()), Times.Exactly(3));
    }

    [Test]
    public async Task TestReconcileNoDeleteSkipsDeletion()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), ManagedIdentityClientMock.Object, RbacClientMock.Object);
        var configApp = FederatedCredentialsOnlyConfig.Configs.First().ApplicationAccessConfig;

        var firstConfig = configApp.FederatedIdentityCredentials.First();
        var existingCredentials = new List<FederatedCredentialInfo>
        {
            new FederatedCredentialInfo(firstConfig.Name!, firstConfig.Issuer!, firstConfig.Subject!, firstConfig.Audiences!),
            new FederatedCredentialInfo(
                "extra-to-delete",
                "https://token.actions.githubusercontent.com",
                "repo:test/azure-sdk:ref:refs/heads/main",
                new List<string> { "api://azureadtokenexchange" }),
        };

        ManagedIdentityClientMock.Setup(c => c.ListFederatedIdentityCredentials(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(existingCredentials);

        var options = new ReconcileOptions { NoDelete = true };
        await reconciler.ReconcileFederatedIdentityCredentials(configApp, options);

        // Should NOT delete the extra credential
        ManagedIdentityClientMock.Verify(c => c.DeleteFederatedIdentityCredential(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        // Should still create the missing credential
        ManagedIdentityClientMock.Verify(c => c.CreateFederatedIdentityCredential(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<FederatedIdentityCredentialsConfig>()), Times.Once);
    }

    [Test]
    public async Task TestReconcileDryRunSkipsWrites()
    {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), ManagedIdentityClientMock.Object, RbacClientMock.Object);

        ManagedIdentityClientMock.Setup(c => c.GetManagedIdentity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(TestIdentityInfo);
        ManagedIdentityClientMock.Setup(c => c.ListFederatedIdentityCredentials(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<FederatedCredentialInfo>
            {
                new FederatedCredentialInfo("existing-cred", "https://example.com", "test-subject", new List<string> { "api://test" })
            });

        var accessConfig = new AccessConfig(Mock.Of<ILogger>(), new List<string>{"./test-configs/full-access-config.json"});
        var options = new ReconcileOptions { DryRun = true };
        await reconciler.Reconcile(accessConfig, options);

        // Should call read operations
        ManagedIdentityClientMock.Verify(c => c.GetManagedIdentity(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        ManagedIdentityClientMock.Verify(c => c.ListFederatedIdentityCredentials(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        // Should NOT call any write operations
        ManagedIdentityClientMock.Verify(c => c.CreateManagedIdentity(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        ManagedIdentityClientMock.Verify(c => c.CreateFederatedIdentityCredential(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FederatedIdentityCredentialsConfig>()), Times.Never);
        ManagedIdentityClientMock.Verify(c => c.DeleteFederatedIdentityCredential(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        RbacClientMock.Verify(c => c.CreateRoleAssignment(
            It.IsAny<Guid>(), It.IsAny<RoleBasedAccessControlsConfig>()), Times.Never);
    }

    [Test]
    [TestCase("FullAccessConfig", 1, 0, 3, 2)]
    [TestCase("MultiAccessConfig", 2, 0, 2, 3)]
    public async Task TestReconcileWithMultipleConfigs(
        string accessConfigName,
        int createIdentityCount,
        int deleteFederatedIdentityCredentialCount,
        int createFederatedIdentityCredentialCount,
        int createRoleAssignmentCount
    ) {
        var reconciler = new Reconciler(Mock.Of<ILogger>(), ManagedIdentityClientMock.Object, RbacClientMock.Object);
        // Override ClientId to ensure we inject it into downstream properties and rendered template values
        TestIdentityInfo = new ManagedIdentityInfo(
            ClientId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            PrincipalId: Guid.Parse("00000000-0000-0000-0000-000000000000"),
            TenantId: Guid.Parse("00000000-0000-0000-0000-000000000000"));

        var prop = this.GetType().GetProperty(accessConfigName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop?.GetValue(this) is not AccessConfig)
        {
            Assert.Fail($"Access config {accessConfigName} not found.");
        }

        var accessConfig = prop!.GetValue(this) as AccessConfig;
        foreach (var cfg in accessConfig!.Configs)
        {
            cfg.ApplicationAccessConfig.IdentityName = "access-config-multi-test";
        }

        // Managed Identity mocks
        ManagedIdentityClientMock.Setup(c => c.GetManagedIdentity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((ManagedIdentityInfo?)null);
        ManagedIdentityClientMock.Setup(c => c.CreateManagedIdentity(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(TestIdentityInfo);
        // Federated Identity Credential mocks
        ManagedIdentityClientMock.Setup(c => c.ListFederatedIdentityCredentials(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<FederatedCredentialInfo>());

        await reconciler.Reconcile(accessConfig, new ReconcileOptions());

        // Create managed identity
        ManagedIdentityClientMock.Verify(c => c.CreateManagedIdentity(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(createIdentityCount));

        // Federated identity credentials
        ManagedIdentityClientMock.Verify(c => c.DeleteFederatedIdentityCredential(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(deleteFederatedIdentityCredentialCount));
        ManagedIdentityClientMock.Verify(c => c.CreateFederatedIdentityCredential(
            It.IsAny<string>(), It.IsAny<string>(), "access-config-multi-test",
            It.IsAny<FederatedIdentityCredentialsConfig>()), Times.Exactly(createFederatedIdentityCredentialCount));

        // RBAC
        RbacClientMock.Verify(c => c.CreateRoleAssignment(
            It.IsAny<Guid>(), It.IsAny<RoleBasedAccessControlsConfig>()), Times.Exactly(createRoleAssignmentCount));
    }
}
