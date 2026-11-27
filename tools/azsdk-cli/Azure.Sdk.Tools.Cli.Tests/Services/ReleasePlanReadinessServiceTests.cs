using System.Net;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class ReleasePlanReadinessServiceTests
{
    private const string Field = "Custom.ServiceReadinessDetails";
    private const string StatusField = "Custom.ServiceApiVersionReadinessStatus";
    private const string CheckedAtField = "Custom.ServiceApiVersionReadinessCheckedAt";
    private const string Project = "specification/example/Example.Management";
    private const string Version = "2026-04-01";
    private static readonly string Commit = new('b', 40);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-12T16:00:00Z");
    private readonly Dictionary<string, string?> _environment = [];
    private Mock<IDevOpsService> _ado = null!;
    private Mock<IServiceApiReadinessService> _readiness = null!;
    private ReleasePlanReadinessService _service = null!;
    private WorkItem _plan = null!;
    private WorkItem _specification = null!;
    private DateTimeOffset _scopeChangedAt;

    [SetUp]
    public void SetUp()
    {
        foreach (var name in new[] { "AZSDK_SERVICE_READINESS_ENVIRONMENT" })
        {
            _environment[name] = Environment.GetEnvironmentVariable(name);
        }
        Environment.SetEnvironmentVariable("AZSDK_SERVICE_READINESS_ENVIRONMENT", "validation");
        _scopeChangedAt = Now.AddHours(-2);
        _specification = new WorkItem
        {
            Id = 456,
            Rev = 3,
            Fields = new Dictionary<string, object>
            {
                ["System.WorkItemType"] = "API Spec",
                ["Custom.APISpecversion"] = Version
            }
        };
        _plan = new WorkItem
        {
            Id = 123,
            Rev = 5,
            Relations = [new WorkItemRelation
            {
                Rel = "System.LinkTypes.Hierarchy-Forward",
                Url = "https://dev.azure.com/example/_apis/wit/workItems/456"
            }],
            Fields = new Dictionary<string, object>
            {
                ["System.WorkItemType"] = "Release Plan",
                ["System.State"] = "In Progress",
                ["Custom.SDKPullRequestForJava"] = "https://github.com/Azure/azure-sdk-for-java/pull/12",
                ["Custom.ApiSpecProjectPath"] = Project,
                [ReleasePlanWorkItem.SpecCommitSHAField] = Commit,
                [StatusField] = "Ready",
                [CheckedAtField] = Now.AddMinutes(-10).ToString("O"),
                [Field] = Stored("Passed", "validation", "widgets", Now.AddMinutes(-10))
            }
        };
        _ado = new Mock<IDevOpsService>(MockBehavior.Strict);
        _ado.Setup(service => service.GetReleasePlansBySdkPullRequestAsync(It.IsAny<string>(), "Java", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReleasePlanWorkItem { WorkItemId = 123, ApiSpecWorkItemId = 456 }]);
        _ado.Setup(service => service.GetWorkItemsByIdsAsync(It.IsAny<IEnumerable<int>>(), 200, WorkItemExpand.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<int> ids, int batchSize, WorkItemExpand expand, CancellationToken ct) =>
                new[] { _plan, _specification }.Where(item => ids.Contains(item.Id!.Value)).ToList());
        _ado.Setup(service => service.GetWorkItemUpdatesAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [new WorkItemUpdate
            {
                Rev = _plan.Rev.GetValueOrDefault(),
                Fields = new Dictionary<string, WorkItemFieldUpdate>
                {
                    ["Custom.ApiSpecProjectPath"] = new() { NewValue = Project },
                    ["System.ChangedDate"] = new() { NewValue = _scopeChangedAt.ToString("O") }
                }
            }]);
        _ado.Setup(service => service.GetWorkItemUpdatesAsync(456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [new WorkItemUpdate
            {
                Rev = _specification.Rev.GetValueOrDefault(),
                Fields = new Dictionary<string, WorkItemFieldUpdate>
                {
                    ["Custom.APISpecversion"] = new() { NewValue = Version },
                    ["System.ChangedDate"] = new() { NewValue = _scopeChangedAt.ToString("O") }
                }
            }]);
        _ado.Setup(service => service.GetReleasePlanReadinessAsync(It.IsAny<string>(), "Java", It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(async (url, language, ct) =>
            {
                var plans = await _ado.Object.GetReleasePlansBySdkPullRequestAsync(url, language, ct: ct);
                if (plans.Count != 1)
                {
                    return null;
                }
                var items = await _ado.Object.GetWorkItemsByIdsAsync([plans[0].WorkItemId, plans[0].ApiSpecWorkItemId], ct: ct);
                var plan = items.Single(item => item.Id == plans[0].WorkItemId);
                var specification = items.Single(item => item.Id == plans[0].ApiSpecWorkItemId);
                var planUpdates = await _ado.Object.GetWorkItemUpdatesAsync(plan.Id!.Value, ct);
                var specificationUpdates = await _ado.Object.GetWorkItemUpdatesAsync(specification.Id!.Value, ct);
                return await MapSnapshot(plan, specification, planUpdates, specificationUpdates);
            });
        _readiness = new Mock<IServiceApiReadinessService>(MockBehavior.Strict);
        _readiness.Setup(service => service.GetPackageMetadataAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result(false));
        _readiness.Setup(service => service.CheckAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result(true));
        _readiness.Setup(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceApiReadinessResponse metadata, CancellationToken ct) =>
            {
                metadata.ReadinessStatus = ServiceApiReadinessResponse.Passed;
                foreach (var project in metadata.Projects)
                {
                    project.ReadinessStatus = ServiceApiReadinessResponse.Passed;
                    project.Matches = [new() { ResourceType = "widgets" }];
                }
                return metadata;
            });
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
        Environment.SetEnvironmentVariable("AZSDK_SERVICE_READINESS_ENVIRONMENT", null);
        Assert.That((await Check(false)).ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        _ado.VerifyNoOtherCalls();
        _readiness.Verify(service => service.GetPackageMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExplicitRecordingRequiresValidationEnvironment()
    {
        Environment.SetEnvironmentVariable("AZSDK_SERVICE_READINESS_ENVIRONMENT", null);

        var result = await Check(true);

        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(result.Detail, Does.Contain("AZSDK_SERVICE_READINESS_ENVIRONMENT"));
        Assert.That(result.ResponseError, Is.EqualTo(result.Detail));
        _ado.VerifyNoOtherCalls();
        _readiness.VerifyNoOtherCalls();
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

    [Test]
    public async Task ReadOnlyCheckUsesOneCompleteDevOpsLookup()
    {
        var planUpdates = await _ado.Object.GetWorkItemUpdatesAsync(123);
        var specificationUpdates = await _ado.Object.GetWorkItemUpdatesAsync(456);
        _ado.Invocations.Clear();
        _ado.Setup(service => service.GetReleasePlanReadinessAsync(It.IsAny<string>(), "Java", It.IsAny<CancellationToken>()))
            .ReturnsAsync(await MapSnapshot(_plan, _specification, planUpdates, specificationUpdates));

        var result = await Check(false);

        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        _ado.Verify(service => service.GetReleasePlanReadinessAsync("https://github.com/Azure/azure-sdk-for-java/pull/12", "Java", It.IsAny<CancellationToken>()), Times.Once);
        _ado.VerifyNoOtherCalls();
    }

    [TestCase("Passed", "2025-01-01", "validation", "widgets", -10)]
    [TestCase("Passed", Version, "other", "widgets", -10)]
    [TestCase("Passed", Version, "validation", "widgets", 1)]
    public async Task UnusableObservationValidatesTheAlreadyReadMetadata(string status, string version, string environment, string resourceType, int minutes)
    {
        _specification.Fields["Custom.APISpecversion"] = version;
        _plan.Fields[Field] = Stored(status, environment, resourceType, Now.AddMinutes(minutes));
        Assert.That((await Check(false)).Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.GetPackageMetadataAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Once);
        _readiness.Verify(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()), Times.Once);
        _readiness.Verify(service => service.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("Passed", 2400, true)]
    [TestCase("Not detected", 23.99, true)]
    [TestCase("Not detected", 24, false)]
    [TestCase("Inconclusive", 23.99, true)]
    [TestCase("Inconclusive", 24, false)]
    [TestCase("Error", 23.99, true)]
    [TestCase("Error", 24, false)]
    public async Task WorkItemCacheLifetimeDependsOnThePersistedResult(string status, double hours, bool reuse)
    {
        var checkedAt = Now.AddHours(-hours);
        _scopeChangedAt = checkedAt.AddDays(-1);
        SetStored(status, "validation", "unrelatedType", checkedAt);

        var result = await Check(false);

        Assert.That(result.Detail.Contains("Reused"), Is.EqualTo(reuse));
        Assert.That(result.ReadinessStatus, Is.EqualTo(reuse ? status : ServiceApiReadinessResponse.Passed));
        if (reuse)
        {
            Assert.That(result.Projects.Single().Matches.Single().ResourceType, Is.EqualTo("unrelatedType"));
            Assert.That(result.Projects.Single().Matches.Single().Regions, Is.EqualTo(new[] { "East US", "West US" }));
            Assert.That(result.CheckedAt, Is.EqualTo(checkedAt));
        }
        _readiness.Verify(service => service.GetPackageMetadataAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Once);
        _readiness.Verify(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()), reuse ? Times.Never() : Times.Once());
        _ado.Verify(service => service.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(61)]
    [TestCase(86400)]
    public async Task MatchingEvidenceDoesNotExpireAsTimePasses(int minutes)
    {
        var checkedAt = Now.AddMinutes(-minutes);
        _scopeChangedAt = checkedAt.AddHours(-1);
        SetStored("Passed", "validation", "widgets", checkedAt);

        var result = await Check(false);

        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(result.Detail, Does.Contain("Reused"));
        Assert.That(result.CheckedAt, Is.EqualTo(checkedAt));
        _readiness.Verify(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()), Times.Never);
        _readiness.Verify(service => service.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NewBuildOrRetryReusesServiceEvidenceForTheCurrentPackageRequirements(bool retry)
    {
        await Check(false);
        var metadata = Result(false);
        metadata.HeadSha = new string(retry ? 'a' : 'c', 40);
        metadata.Packages.Single().DirectoryPath = "sdk/example/current-package";
        _readiness.Setup(service => service.GetPackageMetadataAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        _plan.Rev = 6;
        _ado.Setup(service => service.GetWorkItemUpdatesAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WorkItemUpdate { Rev = 5, Fields = new Dictionary<string, WorkItemFieldUpdate>
                {
                    ["Custom.ApiSpecProjectPath"] = new() { NewValue = Project },
                    ["System.ChangedDate"] = new() { NewValue = _scopeChangedAt.ToString("O") }
                } },
                new WorkItemUpdate { Rev = 6, Fields = new Dictionary<string, WorkItemFieldUpdate>
                {
                    ["Custom.SDKGenerationPipelineForJava"] = new() { NewValue = "https://dev.azure.com/example/_build/results?buildId=456" },
                    ["System.ChangedDate"] = new() { NewValue = Now.ToString("O") }
                } }
            ]);

        var result = await Check(false);

        Assert.That(result, Is.SameAs(metadata));
        Assert.That(result.HeadSha, Is.EqualTo(new string(retry ? 'a' : 'c', 40)));
        Assert.That(result.CheckedAt, Is.EqualTo(Now.AddMinutes(-10)));
        Assert.That(result.Detail, Does.Contain("Reused"));
        _readiness.Verify(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()), Times.Never);
        _readiness.Verify(service => service.GetPackageMetadataAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestCase("Custom.ApiSpecProjectPath", "string")]
    [TestCase("Custom.ApiSpecProjectPath", "dateTime")]
    [TestCase("Custom.ApiSpecProjectPath", "unspecified")]
    [TestCase("Custom.ApiSpecProjectPath", "offset")]
    [TestCase("Custom.APISpecversion", "string")]
    [TestCase("Custom.APISpecversion", "dateTime")]
    [TestCase("Custom.APISpecversion", "unspecified")]
    [TestCase("Custom.APISpecversion", "offset")]
    [TestCase(ReleasePlanWorkItem.SpecCommitSHAField, "string")]
    [TestCase(ReleasePlanWorkItem.SpecCommitSHAField, "dateTime")]
    [TestCase(ReleasePlanWorkItem.SpecCommitSHAField, "unspecified")]
    [TestCase(ReleasePlanWorkItem.SpecCommitSHAField, "offset")]
    public async Task ExistingAdoHistoryInvalidatesEvidenceAfterSpecScopeChanges(string field, string timeKind)
    {
        var checkedAt = Now.AddMinutes(-1).AddMilliseconds(100);
        var scopeChangedAt = checkedAt.AddMilliseconds(800);
        SetStored("Passed", "validation", "widgets", checkedAt);
        object changedDate = timeKind switch
        {
            "dateTime" => scopeChangedAt.UtcDateTime,
            "unspecified" => DateTime.SpecifyKind(scopeChangedAt.UtcDateTime, DateTimeKind.Unspecified),
            "offset" => scopeChangedAt.ToOffset(TimeSpan.FromHours(5.5)),
            _ => scopeChangedAt.ToString("O")
        };
        var item = field == "Custom.APISpecversion" ? _specification : _plan;
        _ado.Setup(service => service.GetWorkItemUpdatesAsync(item.Id!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkItemUpdate
            {
                Rev = item.Rev!.Value,
                Fields = new Dictionary<string, WorkItemFieldUpdate>
                {
                    [field] = new() { NewValue = item.Fields[field] },
                    ["System.ChangedDate"] = new() { NewValue = changedDate }
                }
            }]);
        Assert.That((await Check(false)).Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UnavailableHistoryFallsBackWithoutPretendingTheCacheIsCurrent()
    {
        _ado.Setup(service => service.GetWorkItemUpdatesAsync(123, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("private response"));
        var result = await Check(false);
        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(result.Detail, Does.Contain("unavailable").And.Not.Contains("private response"));
        _readiness.Verify(service => service.GetPackageMetadataAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Never);
        _readiness.Verify(service => service.CheckAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UnsupportedFutureMetadataIsNeverOverwritten()
    {
        _plan.Fields[Field] = "{\"schemaVersion\":5,\"results\":[]}";
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
        _readiness.Verify(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OnlyExplicitRecordingWritesAllThreeProvisionedFields(bool record)
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
        Assert.That(written!.Keys, Is.EquivalentTo(new[] { StatusField, CheckedAtField, Field }));
        Assert.That(written[StatusField], Is.EqualTo("Ready"));
        Assert.That(DateTimeOffset.Parse(written[CheckedAtField]), Is.EqualTo(result.CheckedAt));
        using var json = JsonDocument.Parse(WebUtility.HtmlDecode(written[Field]));
        Assert.That(json.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(4));
        Assert.That(json.RootElement.GetProperty("results")[0].EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[]
        {
            "resourceProvider", "status", "matchedResourceTypes", "validationEnvironment", "checkedAt"
        }));
    }

    [TestCase("")]
    [TestCase("2025-01-01")]
    public async Task MissingOrDifferentExistingPlanApiVersionCannotReuseOrRecordEvidence(string version)
    {
        _specification.Fields["Custom.APISpecversion"] = version;

        var result = await Check(true);

        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(result.Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.ValidateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
        _ado.Verify(service => service.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(false, null)]
    [TestCase(true, null)]
    [TestCase(false, 0)]
    [TestCase(true, 0)]
    public async Task MissingWorkItemRevisionCannotReuseOrRecordEvidence(bool specification, int? revision)
    {
        (specification ? _specification : _plan).Rev = revision;

        var result = await Check(true);

        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(result.Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.ValidateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
        _ado.Verify(service => service.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public async Task OlderMetadataRequiresFreshValidationBeforeExplicitReplacement(int schema)
    {
        _plan.Fields[Field] = JsonSerializer.Serialize(new { schemaVersion = schema, results = new[] { new { apiVersion = Version } } });
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_plan);

        var result = await Check(true);

        Assert.That(result.Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.ValidateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
        _ado.Verify(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MissingSpecAssociationOrSpecHistoryCannotReuseOrRecordEvidence(bool missingHistory)
    {
        if (missingHistory)
        {
            _ado.Setup(service => service.GetWorkItemUpdatesAsync(456, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        }
        else
        {
            _plan.Relations = [];
        }

        var result = await Check(true);

        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(result.Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.ValidateAsync(result, It.IsAny<CancellationToken>()), missingHistory ? Times.Once() : Times.Never());
        _readiness.Verify(service => service.CheckAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), missingHistory ? Times.Never() : Times.Once());
        _ado.Verify(service => service.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task<ServiceApiReadinessResponse> Check(bool record) =>
        _service.CheckAsync("Azure", "azure-sdk-for-java", 12, record, CancellationToken.None);

    [TestCase(StatusField, "")]
    [TestCase(StatusField, "Error")]
    [TestCase(StatusField, "Failed")]
    [TestCase(CheckedAtField, "")]
    [TestCase(CheckedAtField, "invalid")]
    [TestCase(CheckedAtField, "2026-11-12T15:55:00Z")]
    public async Task MissingOrInconsistentSummaryCannotReuseJsonAlone(string field, string value)
    {
        _plan.Fields[field] = value;

        var result = await Check(false);

        Assert.That(result.Detail, Does.Not.Contain("Reused"));
        _readiness.Verify(service => service.ValidateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NativeAdoDateTimePreservesObservationPrecision(bool offset)
    {
        var checkedAt = Now.AddMinutes(-10).AddTicks(1234567);
        SetStored("Passed", "validation", "widgets", checkedAt);
        _plan.Fields[CheckedAtField] = offset ? (object)checkedAt : checkedAt.UtcDateTime;

        var result = await Check(false);

        Assert.That(result.Detail, Does.Contain("Reused"));
        Assert.That(result.CheckedAt, Is.EqualTo(checkedAt));
    }

    [TestCase(-2, true)]
    [TestCase(2, true)]
    [TestCase(-4, false)]
    [TestCase(4, false)]
    public async Task NativeAdoTimestampRoundingDoesNotChangeEvidenceTime(int milliseconds, bool reuse)
    {
        var checkedAt = Now.AddMinutes(-10);
        SetStored("Passed", "validation", "widgets", checkedAt);
        _plan.Fields[CheckedAtField] = checkedAt.AddMilliseconds(milliseconds).UtcDateTime;

        var result = await Check(false);

        Assert.That(result.Detail.Contains("Reused"), Is.EqualTo(reuse));
        if (reuse)
        {
            Assert.That(result.CheckedAt, Is.EqualTo(checkedAt));
        }
        _readiness.Verify(service => service.ValidateAsync(result, It.IsAny<CancellationToken>()), reuse ? Times.Never() : Times.Once());
    }

    [TestCase("Passed")]
    [TestCase("Not detected")]
    [TestCase("Inconclusive")]
    [TestCase("Error")]
    public async Task AllThreeFieldsRepresentTheActualRecordedOutcome(string status)
    {
        _plan.Fields[Field] = "";
        _readiness.Setup(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceApiReadinessResponse metadata, CancellationToken ct) =>
            {
                metadata.ReadinessStatus = status;
                var project = metadata.Projects.Single();
                project.ReadinessStatus = status;
                project.Matches = status == "Passed" ? [new() { ResourceType = "widgets" }] : [];
                return metadata;
            });
        Dictionary<string, string>? written = null;
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), 5, It.IsAny<CancellationToken>()))
            .Callback<int, Dictionary<string, string>, int, CancellationToken>((id, fields, revision, ct) => written = fields)
            .ReturnsAsync(_plan);

        var result = await Check(true);

        Assert.That(result.ReadinessStatus, Is.EqualTo(status));
        Assert.That(written![StatusField], Is.EqualTo(status switch
        {
            "Passed" => "Ready",
            "Not detected" => "Note Ready",
            _ => "Unknown"
        }));
        Assert.That(DateTimeOffset.Parse(written[CheckedAtField]), Is.EqualTo(Now));
        using var json = JsonDocument.Parse(WebUtility.HtmlDecode(written[Field]));
        Assert.That(json.RootElement.GetProperty("results")[0].GetProperty("status").GetString(), Is.EqualTo(status));
    }

    [TestCase("Not detected")]
    [TestCase("Inconclusive")]
    [TestCase("Error")]
    public async Task OlderResultDoesNotReplaceNewerSummaryOrDetails(string status)
    {
        var storedTime = Now.AddMinutes(-1);
        SetStored("Passed", "validation", "widgets", storedTime);
        _plan.Fields.Remove(StatusField);
        _readiness.Setup(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceApiReadinessResponse metadata, CancellationToken ct) =>
            {
                metadata.CheckedAt = Now.AddMinutes(-5);
                metadata.ReadinessStatus = status;
                metadata.Projects.Single().ReadinessStatus = status;
                return metadata;
            });
        Dictionary<string, string>? written = null;
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), 5, It.IsAny<CancellationToken>()))
            .Callback<int, Dictionary<string, string>, int, CancellationToken>((id, fields, revision, ct) => written = fields)
            .ReturnsAsync(_plan);

        await Check(true);

        Assert.That(written![StatusField], Is.EqualTo("Ready"));
        Assert.That(DateTimeOffset.Parse(written[CheckedAtField]), Is.EqualTo(storedTime));
        using var json = JsonDocument.Parse(WebUtility.HtmlDecode(written[Field]));
        Assert.That(json.RootElement.GetProperty("results")[0].GetProperty("checkedAt").GetDateTimeOffset(), Is.EqualTo(storedTime));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CacheMissReturnsTheSameMetadataAndPackageObjects(bool malformed)
    {
        _plan.Fields[Field] = malformed ? "not json" : "";
        var metadata = Result(false);
        var package = metadata.Packages.Single();
        _readiness.Setup(service => service.GetPackageMetadataAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var result = await Check(false);

        Assert.That(result, Is.SameAs(metadata));
        Assert.That(result.Packages.Single(), Is.SameAs(package));
        _readiness.Verify(service => service.GetPackageMetadataAsync("Azure", "azure-sdk-for-java", 12, It.IsAny<CancellationToken>()), Times.Once);
        _readiness.Verify(service => service.ValidateAsync(metadata, It.IsAny<CancellationToken>()), Times.Once);
        _readiness.Verify(service => service.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

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
        _ado.Verify(service => service.GetReleasePlanReadinessAsync(It.IsAny<string>(), "Java", It.IsAny<CancellationToken>()), Times.Exactly(3));
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task OptionalWriteDeadlinePreservesPrimaryButCallerCancellationIsHonored(bool cancelCaller)
    {
        _plan.Fields[Field] = "";
        using var caller = new CancellationTokenSource();
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<int, Dictionary<string, string>, int, CancellationToken>((id, fields, revision, ct) =>
            {
                if (cancelCaller)
                {
                    caller.Cancel();
                }
                else
                {
                    var budget = (CancellationTokenSource)typeof(CancellationToken)
                        .GetField("_source", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(ct)!;
                    budget.Cancel();
                }
                return Task.FromCanceled<WorkItem>(ct);
            });

        if (cancelCaller)
        {
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await _service.CheckAsync("Azure", "azure-sdk-for-java", 12, true, caller.Token));
        }
        else
        {
            var result = await _service.CheckAsync("Azure", "azure-sdk-for-java", 12, true, caller.Token);
            Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
            Assert.That(result.Detail, Does.Contain("optional release-plan update failed"));
            Assert.That(caller.IsCancellationRequested, Is.False);
        }
    }

    [TestCase("Note Ready")]
    [TestCase("Not Ready")]
    [TestCase("Not ready")]
    public async Task NotReadyStorageSpellingDoesNotChangeTheRecordedOutcome(string storedStatus)
    {
        SetStored("Not detected", "validation", "widgets", Now.AddMinutes(-10));
        _plan.Fields[StatusField] = storedStatus;

        var result = await Check(false);

        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.NotDetected));
        Assert.That(result.Detail, Does.Contain("Reused"));
    }

    [TestCase(262145)]
    [TestCase(1000001)]
    public async Task AdoPayloadRejectionPreservesTheCompletedReadinessResult(int length)
    {
        _plan.Fields[Field] = "";
        var fresh = Result(true);
        fresh.Projects.Single().Matches = [new() { ResourceType = new string('x', length) }];
        _readiness.Setup(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fresh);
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), 5, It.IsAny<CancellationToken>()))
            .Callback<int, Dictionary<string, string>, int, CancellationToken>((id, fields, revision, ct) =>
                Assert.That(fields[Field].Length, Is.GreaterThan(length)))
            .ThrowsAsync(new InvalidOperationException("ADO rejected the payload size"));

        var result = await Check(true);

        Assert.That(result, Is.SameAs(fresh));
        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(result.Detail, Does.Contain("optional release-plan update failed"));
        _ado.Verify(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task MultilineRecordingAndReusePreserveLiteralEntitiesAndLongEvidence()
    {
        const string resourceType = "widgets <type> &quot; &amp;";
        _plan.Fields[Field] = "";
        var fresh = Result(true);
        fresh.Projects.Single().Matches = [new() { ResourceType = resourceType, Regions = ["East US", "West US"] }];
        _readiness.Setup(service => service.ValidateAsync(It.IsAny<ServiceApiReadinessResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fresh);
        Dictionary<string, string>? written = null;
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), 5, It.IsAny<CancellationToken>()))
            .Callback<int, Dictionary<string, string>, int, CancellationToken>((id, fields, revision, ct) => written = fields)
            .ReturnsAsync(_plan);

        await Check(true);

        Assert.That(written![Field], Does.Contain("&quot;").And.Length.GreaterThan(255));
        foreach (var entry in written)
        {
            _plan.Fields[entry.Key] = entry.Value;
        }
        var reused = await Check(false);

        Assert.That(reused.Detail, Does.Contain("Reused"));
        Assert.That(reused.Projects.Single().Matches.Single().ResourceType, Is.EqualTo(resourceType));
        Assert.That(reused.Projects.Single().Matches.Single().Regions, Is.EqualTo(new[] { "East US", "West US" }));
    }

    [Test]
    public async Task OldShortDetailsFieldIsNeitherReusedNorOverwritten()
    {
        const string oldField = "Custom.ServiceApiVersionReadinessDetails";
        var oldValue = _plan.Fields[Field];
        _plan.Fields.Remove(Field);
        _plan.Fields[oldField] = oldValue;
        Dictionary<string, string>? written = null;
        _ado.Setup(service => service.UpdateWorkItemAsync(123, It.IsAny<Dictionary<string, string>>(), 5, It.IsAny<CancellationToken>()))
            .Callback<int, Dictionary<string, string>, int, CancellationToken>((id, fields, revision, ct) => written = fields)
            .ReturnsAsync(_plan);

        var result = await Check(true);

        Assert.That(result.Detail, Does.Not.Contain("Reused"));
        Assert.That(written!.ContainsKey(Field), Is.True);
        Assert.That(written.ContainsKey(oldField), Is.False);
        Assert.That(_plan.Fields[oldField], Is.EqualTo(oldValue));
    }

    private async Task<(ReleasePlanWorkItem Plan, ApiSpecWorkItem Specification, List<WorkItemUpdate> PlanUpdates, List<WorkItemUpdate> SpecificationUpdates)?> MapSnapshot(
        WorkItem plan, WorkItem specification, List<WorkItemUpdate> planUpdates, List<WorkItemUpdate> specificationUpdates)
    {
        if (plan.Fields["System.WorkItemType"]?.ToString() != "Release Plan" ||
            specification.Fields["System.WorkItemType"]?.ToString() != "API Spec" ||
            plan.Relations?.Any(relation => relation.Rel == "System.LinkTypes.Hierarchy-Forward" &&
                relation.Url.TrimEnd('/').EndsWith($"/{specification.Id}", StringComparison.Ordinal)) != true)
        {
            return null;
        }
        var connection = new DevOpsServiceTests.TestDevOpsConnection();
        connection.AddWorkItemToQuery(plan);
        connection.AddWorkItem(plan);
        connection.AddWorkItem(specification);
        connection.SetWorkItemUpdates(plan.Id!.Value, planUpdates);
        connection.SetWorkItemUpdates(specification.Id!.Value, specificationUpdates);
        var mapper = new DevOpsService(new TestLogger<DevOpsService>(), connection);
        return await mapper.GetReleasePlanReadinessAsync("https://github.com/Azure/azure-sdk-for-java/pull/12", "Java");
    }

    private void SetStored(string status, string environment, string resourceType, DateTimeOffset checkedAt)
    {
        _plan.Fields[StatusField] = status switch
        {
            "Passed" => "Ready",
            "Not detected" => "Note Ready",
            _ => "Unknown"
        };
        _plan.Fields[CheckedAtField] = checkedAt.ToString("O");
        _plan.Fields[Field] = Stored(status, environment, resourceType, checkedAt);
    }

    private static string Stored(string status, string environment, string resourceType, DateTimeOffset checkedAt) =>
        WebUtility.HtmlEncode(JsonSerializer.Serialize(new
        {
            schemaVersion = 4,
            results = new[] { new
        {
            resourceProvider = "Microsoft.Example", status,
            matchedResourceTypes = new[] { new { resourceType, regions = new[] { "East US", "West US" } } }, validationEnvironment = environment, checkedAt
        } }
        }));

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
            HeadSha = new string('a', 40),
            Plane = "management-plane",
            CheckedAt = Now,
            ReadinessStatus = ready ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.Inconclusive,
            Packages = [package],
            Projects = [new ServiceApiReadinessProject
            {
                Package = package, ApiVersion = new(Version, "Microsoft.Example"), Namespace = "Microsoft.Example",
                ReadinessStatus = ready ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.Inconclusive,
                Matches = ready ? [new() { ResourceType = "widgets" }] : []
            }]
        };
    }
}
