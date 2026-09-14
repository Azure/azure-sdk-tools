// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using Moq;
using AdoIdentity = Microsoft.VisualStudio.Services.Identity.Identity;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class ReleasePlanAuthorizationTests
{
    private Mock<IDevOpsConnection> _connection = null!;
    private Mock<IdentityHttpClient> _identities = null!;
    private Mock<ProjectHttpClient> _projects = null!;
    private AdoIdentity _caller = null!;
    private AdoIdentity _group = null!;
    private DevOpsService _service = null!;
    private readonly Guid _projectId = Guid.Parse("7cd33468-73af-4ca4-8ac5-4e5f9b70d3c1");

    [SetUp]
    public void SetUp()
    {
        _connection = new Mock<IDevOpsConnection>(MockBehavior.Strict);
        _identities = new Mock<IdentityHttpClient>(new Uri("https://dev.azure.com/test"), new VssCredentials());
        _projects = new Mock<ProjectHttpClient>(new Uri("https://dev.azure.com/test"), new VssCredentials());
        _caller = new AdoIdentity
        {
            Id = Guid.Parse("3b025eb1-3206-4615-a9f1-a6d71e88c751"),
            Descriptor = new IdentityDescriptor("Microsoft.IdentityModel.Claims.ClaimsIdentity", "caller"),
            IsActive = true,
            MemberOf = []
        };
        _group = new AdoIdentity
        {
            Id = Guid.Parse("af6f4e09-f1b2-494e-9758-a1fafdfd0b4d"),
            Descriptor = new IdentityDescriptor("Microsoft.TeamFoundation.Identity", "release-project-admins"),
            IsContainer = true,
            IsActive = true
        };
        _connection.Setup(c => c.GetAuthenticatedIdentityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_caller);
        _connection.Setup(c => c.GetProjectClient(It.IsAny<CancellationToken>())).Returns(_projects.Object);
        _connection.Setup(c => c.GetIdentityClient(It.IsAny<CancellationToken>())).Returns(_identities.Object);
        _projects.Setup(p => p.GetProject(Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT, null, false, null))
            .ReturnsAsync(new TeamProject { Id = _projectId, Name = Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT });
        SetGroups(new IdentitiesCollection([_group]));
        SetMembership(_caller);
        _service = new DevOpsService(new TestLogger<DevOpsService>(), _connection.Object);
    }

    private void SetGroups(IdentitiesCollection groups) => _identities
        .Setup(i => i.ReadIdentitiesAsync(IdentitySearchFilter.AdministratorsGroup, _projectId.ToString(),
            It.IsAny<ReadIdentitiesOptions>(), QueryMembership.None, null, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(groups);

    private void SetMembership(AdoIdentity? identity) => _identities
        .Setup(i => i.ReadIdentitiesAsync(It.Is<IList<Guid>>(ids => ids.Count == 1 && ids[0] == _caller.Id),
            QueryMembership.ExpandedUp, null, false, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new IdentitiesCollection(identity == null ? [] : [identity]));

    [Test]
    public async Task ExpandedMembershipAllowsAdministrator()
    {
        _caller.MemberOf = [_group.Descriptor];

        Assert.That(await _service.IsReleasePlanAdminAsync(CancellationToken.None), Is.True);
        _identities.Verify(i => i.ReadIdentitiesAsync(It.Is<IList<Guid>>(ids => ids.Count == 1 && ids[0] == _caller.Id),
            QueryMembership.ExpandedUp, null, false, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task NonMemberCannotAbandon()
    {
        Assert.That(await _service.IsReleasePlanAdminAsync(CancellationToken.None), Is.False);
    }

    [Test]
    public async Task AdministratorOfAnotherProjectCannotAbandon()
    {
        _caller.MemberOf = [new IdentityDescriptor("Microsoft.TeamFoundation.Identity", "other-project-admins")];

        Assert.That(await _service.IsReleasePlanAdminAsync(CancellationToken.None), Is.False);
    }

    [Test]
    public void MissingAuthenticatedIdentityFailsClosed()
    {
        _connection.Setup(c => c.GetAuthenticatedIdentityAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AdoIdentity)null!);

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.IsReleasePlanAdminAsync(CancellationToken.None));
        _connection.Verify(c => c.GetProjectClient(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void EmptyAuthenticatedIdentityFailsClosed()
    {
        _caller.Id = Guid.Empty;

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.IsReleasePlanAdminAsync(CancellationToken.None));
    }

    [TestCase(0)]
    [TestCase(2)]
    public void MissingOrAmbiguousAdministratorGroupFailsClosed(int groupCount)
    {
        SetGroups(new IdentitiesCollection(Enumerable.Repeat(_group, groupCount).ToList()));

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.IsReleasePlanAdminAsync(CancellationToken.None));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void InactiveOrNonGroupIdentityFailsClosed(bool isActive, bool isContainer)
    {
        _group.IsActive = isActive;
        _group.IsContainer = isContainer;

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.IsReleasePlanAdminAsync(CancellationToken.None));
    }

    [Test]
    public void MissingMembershipFailsClosed()
    {
        SetMembership(null);

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.IsReleasePlanAdminAsync(CancellationToken.None));
    }

    [Test]
    public void MembershipForDifferentIdentityFailsClosed()
    {
        SetMembership(new AdoIdentity { Id = Guid.NewGuid(), IsActive = true, MemberOf = [_group.Descriptor] });

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.IsReleasePlanAdminAsync(CancellationToken.None));
    }

    [Test]
    public void InactiveCallerFailsClosed()
    {
        _caller.IsActive = false;
        _caller.MemberOf = [_group.Descriptor];

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.IsReleasePlanAdminAsync(CancellationToken.None));
    }

    [Test]
    public void MissingMembershipDataFailsClosed()
    {
        _caller.MemberOf = null;

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.IsReleasePlanAdminAsync(CancellationToken.None));
    }

    [Test]
    public void IdentityLookupFailureDoesNotGrantAccess()
    {
        _connection.Setup(c => c.GetAuthenticatedIdentityAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Identity lookup failed"));

        Assert.ThrowsAsync<HttpRequestException>(() => _service.IsReleasePlanAdminAsync(CancellationToken.None));
    }

    [Test]
    public void CancellationStopsBeforeIdentityLookup()
    {
        Assert.ThrowsAsync<OperationCanceledException>(() => _service.IsReleasePlanAdminAsync(new CancellationToken(true)));
        _connection.Verify(c => c.GetAuthenticatedIdentityAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}