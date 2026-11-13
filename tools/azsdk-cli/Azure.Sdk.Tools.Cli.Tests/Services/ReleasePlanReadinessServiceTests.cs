using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class ReleasePlanReadinessServiceTests
{
    private const string Field = "Custom.ExampleReadiness";
    private const string Project = "specification/example/Example.Management";
    private const string Version = "2026-04-01";
    private static readonly string Commit = new('b', 40);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-12T16:00:00Z");
    private readonly Dictionary<string, string?> _environment = [];
    private Mock<IDevOpsService> _ado = null!;
    private Mock<IServiceApiReadinessService> _readiness = null!;
    private ReleasePlanReadinessService _service = null!;
    private WorkItem _plan = null!;
    private DateTimeOffset _scopeChangedAt;

    [SetUp]
    public void SetUp()
    {
        foreach (var name in new[] { "AZSDK_SERVICE_READINESS_FIELD", "AZSDK_SERVICE_READINESS_ENVIRONMENT", "AZSDK_SERVICE_READINESS_MAX_AGE_SECONDS" })
        {
            _environment[name] = Environment.GetEnvironmentVariable(name);
        }
        Environment.SetEnvironmentVariable("AZSDK_SERVICE_READINESS_FIELD", Field);
        Environment.SetEnvironmentVariable("AZSDK_SERVICE_READINESS_ENVIRONMENT", "validation");
        Environment.SetEnvironmentVariable("AZSDK_SERVICE_READINESS_MAX_AGE_SECONDS", "3600");
        _scopeChangedAt = Now.AddHours(-2);
        _plan = new WorkItem
        {
            Id = 123,
            Rev = 5,
            Fields = new Dictionary<string, object>
            {
                ["System.WorkItemType"] = "Release Plan",
                ["System.State"] = "In Progress",
                ["Custom.SDKPullRequestForJava"] = "https://github.com/Azure/azure-sdk-for-java/pull/12",
                ["Custom.ApiSpecProjectPath"] = Project,
                [ReleasePlanWorkItem.SpecCommitSHAField] = Commit,
                [Field] = Stored("Passed", Version, "validation", "widgets", Now.AddMinutes(-10))
            }
        };
        _ado = new Mock<IDevOpsService>(MockBehavior.Strict);
        _ado.Setup(service => service.GetReleasePlansBySdkPullRequestAsync(It.IsAny<string>(), "Java", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReleasePlanWorkItem { WorkItemId = 123 }]);
        _ado.Setup(service => service.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), 200, WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [_plan]);
        _ado.Setup(service => service.GetWorkItemUpdatesAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [new WorkItemUpdate
            {
                Rev = _plan.Rev!.Value,
                Fields = new Dictionary<string, WorkItemFieldUpdate>
                {
                    ["Custom.ApiSpecProjectPath"] = new() { NewValue = Project },
                    ["System.ChangedDate"] = new() { NewValue = _scopeChangedAt.ToString("O") }
                }
            }]);
        _readiness = new Mock<IServiceApiReadinessService>(MockBehavior.Strict);
        _readiness.Setup(service => service.GetPackageMetadataAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result(false));
        _readiness.Setup(service => service.CheckAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result(true));
        var clock = new Mock<TimeProvider>();
        clock.Setup(provider => provider.GetUtcNow()).Returns(Now);
        _service = new ReleasePlanReadinessService(_ado.Object, _readiness.Object, clock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var entry in _environment)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
    }

    [Test]
    public async Task UnconfiguredCheckDoesNotAccessAdo()
    {
        Environment.SetEnvironmentVariable("AZSDK_SERVICE_READINESS_FIELD", null);
        Assert.That((await Check(false)).ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        _ado.VerifyNoOtherCalls();
        _readiness.Verify(service => service.GetPackageMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReusesCompactEvidenceUsingExistingPlanValuesAndHistory()
    {
        var result = await Check(false);
        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(result.Detail, Does.Contain("Reused"));
        Assert.That(result.HeadSha, Is.EqualTo(new string('a', 40)));
        Assert.That(result.CheckedAt, Is.EqualTo(Now.AddMinutes(-10)));
        _readiness.Verify(service => service.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _ado.Verify(service => service.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("Error", Version, "validation", "widgets", -10)]
    [TestCase("Passed", "2025-01-01", "validation", "widgets", -10)]
    [TestCase("Passed", Version, "other", "widgets", -10)]
    [TestCase("Passed", Version, "validation", "otherType", -10)]
    [TestCase("Passed", Version, "validation", "widgets", -61)]
    [TestCase("Passed", Version, "validation", "widgets", 1)]
    public async Task UnusableObservationFallsBackToTheStatelessCheck(string status, string version, string environment, string resourceType, int minutes)
    {
        _plan.Fields[Field] = Stored(status, version, environment, resourceType, Now.AddMinutes(minutes));
        Assert.That((await Check(false)).Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.CheckAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExistingAdoHistoryInvalidatesEvidenceAfterProjectOrCommitChanges()
    {
        _scopeChangedAt = Now.AddMinutes(-5);
        Assert.That((await Check(false)).Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.CheckAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UnavailableHistoryFallsBackWithoutPretendingTheCacheIsCurrent()
    {
        _ado.Setup(service => service.GetWorkItemUpdatesAsync(123, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("private response"));
        var result = await Check(false);
        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(result.Detail, Does.Contain("unavailable").And.Not.Contains("private response"));
    }

    [Test]
    public async Task UnsupportedFutureMetadataIsNeverOverwritten()
    {
        _plan.Fields[Field] = "{\"schemaVersion\":3,\"results\":[]}";
        var result = await Check(true);
        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        _ado.Verify(service => service.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(4)]
    [TestCase(5)]
    public async Task IncompleteOrUnscopedHistoryCannotValidateAnOldObservation(int revision)
    {
        _ado.Setup(service => service.GetWorkItemUpdatesAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkItemUpdate { Rev = revision }]);
        Assert.That((await Check(false)).Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.CheckAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OnlyExplicitRecordingWritesSixObservationFields(bool record)
    {
        _plan.Fields[Field] = "";
        Dictionary<string, string>? written = null;
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), 5, It.IsAny<CancellationToken>()))
            .Callback<int, Dictionary<string, string>, int, CancellationToken>((id, fields, revision, ct) => written = fields)
            .ReturnsAsync(_plan);
        var result = await Check(record);
        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        if (!record)
        {
            Assert.That(written, Is.Null);
            return;
        }
        Assert.That(written!.Keys, Is.EqualTo(new[] { Field }));
        using var json = JsonDocument.Parse(written[Field]);
        Assert.That(json.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(2));
        Assert.That(json.RootElement.GetProperty("results")[0].EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[]
        {
            "resourceProvider", "apiVersion", "status", "matchedResourceTypes", "validationEnvironment", "checkedAt"
        }));
    }

    private Task<ServiceApiReadinessResponse> Check(bool record) =>
        _service.CheckAsync("Azure", "azure-sdk-for-java", 12, record, CancellationToken.None);

    [Test]
    public async Task RevisionConflictIsRereadBeforeOneRetry()
    {
        _plan.Fields[Field] = "";
        var writes = 0;
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<int, Dictionary<string, string>, int, CancellationToken>((id, fields, revision, ct) =>
            {
                writes++;
                if (writes == 1)
                {
                    _plan.Rev = 6;
                    throw new InvalidOperationException("Revision conflict");
                }
                Assert.That(revision, Is.EqualTo(6));
                return Task.FromResult(_plan);
            });
        Assert.That((await Check(true)).ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(writes, Is.EqualTo(2));
    }

    [Test]
    public async Task FailedOptionalWriteDoesNotReplaceThePrimaryResult()
    {
        _plan.Fields[Field] = "";
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("private ADO diagnostic"));
        var result = await Check(true);
        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(result.Detail, Does.Contain("optional release-plan update failed").And.Not.Contains("private ADO"));
        _ado.Verify(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static string Stored(string status, string version, string environment, string resourceType, DateTimeOffset checkedAt) =>
        JsonSerializer.Serialize(new { schemaVersion = 2, results = new[] { new
        {
            resourceProvider = "Microsoft.Example", apiVersion = version, status,
            matchedResourceTypes = new[] { resourceType }, validationEnvironment = environment, checkedAt
        } } });

    private static ServiceApiReadinessResponse Result(bool ready)
    {
        var package = new PackageInfo
        {
            DirectoryPath = "sdk/example/package",
            TypeSpecLocation = new(Project, Commit, "Azure/azure-rest-api-specs"),
            ResourceTypes = [new("Microsoft.Example", Version, "widgets")]
        };
        return new ServiceApiReadinessResponse
        {
            HeadSha = new string('a', 40), Plane = "management-plane", CheckedAt = Now,
            ReadinessStatus = ready ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.Inconclusive,
            Packages = [package], Projects = [new ServiceApiReadinessProject
            {
                Package = package, ApiVersion = new(Version, "Microsoft.Example"), Namespace = "Microsoft.Example",
                ReadinessStatus = ready ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.Inconclusive,
                Matches = ready ? [new() { ResourceType = "widgets" }] : []
            }]
        };
    }
}