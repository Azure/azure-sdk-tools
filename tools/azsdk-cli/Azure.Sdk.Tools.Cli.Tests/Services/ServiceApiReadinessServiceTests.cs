using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.AspNetCore.WebUtilities;
using Moq;
using Moq.Protected;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class ServiceApiReadinessServiceTests
{
    private const string Head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SpecCommit = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Version = "2026-04-01";
    private const string Project = "specification/example/resource-manager/Example";
    private const string SpecRepository = "Azure/azure-rest-api-specs";
    private const string Package = "sdk/example/azure-resourcemanager-example";
    private const string Metadata = "src/main/resources/META-INF/azure-resourcemanager-example_metadata.json";
    private const string SwaggerDirectory = "specification/example/resource-manager/swagger";
    private readonly Dictionary<string, string> _files = [];
    private readonly Dictionary<string, JsonArray> _directories = [];
    private readonly List<string> _changedFiles = [];
    private readonly List<string> _labels = [];
    private readonly List<(HttpMethod Method, Uri Uri, string? Token)> _requests = [];
    private Mock<IGitHubService> _github = null!;
    private ServiceApiReadinessService _service = null!;
    private string _repository = "helen229/azure-sdk-for-java";
    private string[] _advertisedVersions = [Version];
    private HttpStatusCode _githubStatus;
    private HttpStatusCode _armStatus;
    private int _pullRequestReads;
    private bool _changeHead;
    private bool _truncatedFiles;

    [SetUp]
    public void SetUp()
    {
        _files.Clear();
        _directories.Clear();
        _changedFiles.Clear();
        _labels.Clear();
        _requests.Clear();
        _repository = "helen229/azure-sdk-for-java";
        _advertisedVersions = [Version];
        _githubStatus = HttpStatusCode.OK;
        _armStatus = HttpStatusCode.OK;
        _pullRequestReads = 0;
        _changeHead = false;
        _truncatedFiles = false;
        _github = new Mock<IGitHubService>();
        _github.Setup(service => service.GetAuthToken()).Returns("github-test-token");
        var credential = new Mock<TokenCredential>();
        credential.Setup(value => value.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<AccessToken>(new AccessToken("azure-test-token", DateTimeOffset.UtcNow.AddHours(1))));
        var azure = new Mock<IAzureService>();
        azure.Setup(service => service.GetCredential(It.IsAny<string?>())).Returns(credential.Object);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Respond(request));
            });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(ServiceApiReadinessService.HttpClientName)).Returns(new HttpClient(handler.Object));
        _service = new ServiceApiReadinessService(_github.Object, azure.Object, factory.Object);
        AddPackage(Package, Metadata, Version);
    }

    [TestCase("java", Package, Metadata)]
    [TestCase("net", "sdk/example/Azure.ResourceManager.Example", "metadata.json")]
    [TestCase("js", "sdk/example/arm-example", "metadata.json")]
    [TestCase("python", "sdk/example/azure-mgmt-example", "_metadata.json")]
    [TestCase("go", "sdk/resourcemanager/example/armexample", "testdata/_metadata.json")]
    public async Task ReadsExactCurrentHeadMetadataForEachSdkLayout(string language, string package, string metadata)
    {
        _files.Clear();
        _changedFiles.Clear();
        _repository = $"helen229/azure-sdk-for-{language}";
        AddPackage(package, metadata, Version);
        var response = await CheckAsync();
        Assert.Multiple(() =>
        {
            Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
            Assert.That(response.HeadSha, Is.EqualTo(Head));
            Assert.That(response.Projects.Single().Source.PackagePath, Is.EqualTo(package));
            Assert.That(response.Projects.Single().Source.ApiVersion, Is.EqualTo(Version));
            Assert.That(response.Projects.Single().Source.CommitSha, Is.EqualTo(SpecCommit));
            Assert.That(response.Projects.Single().Matches.Single().Regions, Is.EqualTo(new[] { "East US" }));
            Assert.That(_requests.All(request => request.Method == HttpMethod.Get), Is.True);
            Assert.That(_requests.Where(request => request.Uri.Host == "api.github.com").All(request => request.Token == "github-test-token"), Is.True);
            Assert.That(_requests.Where(request => request.Uri.Host == "management.azure.com").All(request => request.Token == "azure-test-token"), Is.True);
        });
    }

    [Test]
    public async Task ReportsNotDetectedOnlyAfterMappingApplicableResourceTypes()
    {
        _advertisedVersions = ["2025-01-01"];
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.NotDetected));
        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.Projects.Single().Detail, Does.Contain("not a confirmed deployment failure"));
    }

    [TestCase("js", "sdk/example/arm-example", "metadata.json")]
    [TestCase("python", "sdk/example/azure-mgmt-example", "_metadata.json")]
    public async Task SingularApiVersionUsesThePinnedProviderNamespace(string language, string package, string metadata)
    {
        _files.Clear();
        _changedFiles.Clear();
        _repository = $"helen229/azure-sdk-for-{language}";
        AddPackage(package, metadata, Version);
        SetFile(_repository, Head, $"{package}/{metadata}", JsonSerializer.Serialize(new { apiVersion = Version }));
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.Projects.Single().Source.ExpectedNamespace, Is.EqualTo("Microsoft.Example"));
        Assert.That(response.Projects.Single().Source.ApiVersion, Is.EqualTo(Version));
    }

    [Test]
    public async Task UnambiguousVersionArrayUsesSharedMetadataParser()
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", "{\"apiVersions\":{\"Microsoft.Example\":[\"2026-04-01\"]}}");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
    }

    [Test]
    public async Task MissingVersionMapDoesNotUseDescriptionNotes()
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", "{}");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Detail, Does.Contain("apiVersions"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task ConflictingVersionsNeverSelectTheNewest()
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", "{\"apiVersions\":{\"Microsoft.Example\":[\"2026-04-01\",\"2026-03-01\"]}}");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Detail, Does.Contain("conflicting API versions"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [TestCase("{broken json")]
    [TestCase("{\"apiVersions\":{\"Microsoft.Example\":42}}")]
    [TestCase("{\"apiVersions\":{\"Microsoft.Example\":\"2026-04-01\"},\"commit\":\"conflicting\"}")]
    public async Task InvalidOrConflictingSdkMetadataIsInconclusive(string metadata)
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", metadata);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task MissingLocationIsInconclusive()
    {
        _files.Remove(Key(_repository, Head, $"{Package}/tsp-location.yaml"));
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Detail, Does.Contain("tsp-location.yaml"));
    }

    [Test]
    public async Task UntrustedSpecRepositoryIsNotContacted()
    {
        SetFile(_repository, Head, $"{Package}/tsp-location.yaml", $"directory: {Project}\ncommit: {SpecCommit}\nrepo: https://untrusted.example/specs\n");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(_requests.All(request => request.Uri.Host == "api.github.com"), Is.True);
    }

    [Test]
    public async Task DataPlaneLocationSkipsArm()
    {
        SetFile(_repository, Head, $"{Package}/tsp-location.yaml", $"directory: specification/example/data-plane/Example\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        var response = await CheckAsync();
        Assert.That(response.Plane, Is.EqualTo("data-plane"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task DataPlaneLabelIsFallbackWhenMetadataIsMissing()
    {
        _labels.Add("data-plane");
        _files.Remove(Key(_repository, Head, $"{Package}/tsp-location.yaml"));
        var response = await CheckAsync();
        Assert.That(response.Plane, Is.EqualTo("data-plane"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task ManagementMetadataIsNotOverriddenByOnlyADataPlaneLabel()
    {
        _labels.Add("data-plane");
        var response = await CheckAsync();
        Assert.That(response.Plane, Is.EqualTo("management-plane"));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
    }

    [Test]
    public async Task ConflictingPlaneLabelsAreInconclusive()
    {
        _labels.AddRange(["data-plane", "resource-manager"]);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task UnavailableProviderIsInconclusive()
    {
        _armStatus = HttpStatusCode.NotFound;
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Projects.Single().Detail, Does.Contain("unavailable"));
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Redirect)]
    public async Task AzureTransportFailuresAreErrorsWithoutPrivateDetails(HttpStatusCode status)
    {
        _armStatus = status;
        var response = await CheckAsync();
        var json = JsonSerializer.Serialize(response);
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(json, Does.Not.Contain("private transport details"));
        Assert.That(json, Does.Not.Contain("azure-test-token"));
        Assert.That(json, Does.Not.Contain("github-test-token"));
    }

    [Test]
    public async Task GitHubAccessFailureIsNotReportedAsMissingMetadata()
    {
        _githubStatus = HttpStatusCode.Forbidden;
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(response.Detail, Does.Contain("403"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task TokenResolutionFailureDoesNotExposeTheToken()
    {
        _github.Setup(service => service.GetAuthToken()).Throws(new InvalidOperationException("private token value"));
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(JsonSerializer.Serialize(response), Does.Not.Contain("private token value"));
        Assert.That(_requests, Is.Empty);
    }

    [Test]
    public async Task ChangedExpectedHeadDoesNotReadSdkFilesOrArm()
    {
        var response = await _service.CheckAsync("helen229", "azure-sdk-for-java", 1, new string('c', 40), CancellationToken.None);
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Detail, Does.Contain("head changed"));
        Assert.That(_requests.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task HeadMovementDuringCheckRejectsAnOtherwisePassingResult()
    {
        _changeHead = true;
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Detail, Does.Contain("changed during"));
        Assert.That(response.Projects.Single().ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
    }

    [Test]
    public async Task SharedNamespaceUsesOneArmLookupAndAllProjectsMustPass()
    {
        AddPackage("sdk/second/azure-resourcemanager-second", "src/main/resources/META-INF/azure-resourcemanager-second_metadata.json", "2026-03-01");
        var response = await CheckAsync();
        Assert.That(response.Projects.Count, Is.EqualTo(2));
        Assert.That(response.Projects.Select(project => project.ReadinessStatus), Is.EquivalentTo(new[] { ServiceApiReadinessResponse.Passed, ServiceApiReadinessResponse.NotDetected }));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.NotDetected));
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task UnresolvedLiteralChildCannotPassUsingItsParent()
    {
        SetSwagger(Version, "/providers/Microsoft.Example/widgets/{name}/config/web", true);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Projects.Single().Detail, Does.Contain("resource-type mapping"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task PostActionRetainsItsOwningResourceType()
    {
        SetSwagger(Version, "/providers/Microsoft.Example/widgets/{name}/restart", false);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
    }

    [Test]
    public async Task UnresolvedProviderExpressionIsNotGuessedFromNamespace()
    {
        SetFile(SpecRepository, SpecCommit, $"{Project}/main.tsp", "@armProviderNamespace(providerName)\nnamespace Microsoft.Example;");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [TestCase("{bad json")]
    [TestCase("{\"info\":{\"version\":\"2025-01-01\"},\"paths\":{}}")]
    public async Task InvalidOrWrongVersionSwaggerIsInconclusive(string swagger)
    {
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json", swagger);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task InheritedSwaggerMappingIsNotGuessed()
    {
        SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml", "extends: ../tspconfig.yaml\n");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task WildcardDiscoveryReadsOnlyTheMappedPinnedSwaggerDirectory()
    {
        SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml", Config("{service-name}/{version-status}/{version}/example.json"));
        _directories[Key(SpecRepository, SpecCommit, SwaggerDirectory)] = JsonSerializer.SerializeToNode(new[] { new { name = "Example", type = "dir" } })!.AsArray();
        var swagger = _files[Key(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json")];
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/Example/stable/{Version}/example.json", swagger);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
    }

    [Test]
    public async Task TruncatedChangedFilesCannotProducePassed()
    {
        _truncatedFiles = true;
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Detail, Does.Contain("3000-file limit"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task CanceledWorkReturnsStructuredError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var response = await _service.CheckAsync("helen229", "azure-sdk-for-java", 1, Head, cancellation.Token);
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(response.Detail, Does.Contain("canceled"));
    }

    private Task<ServiceApiReadinessResponse> CheckAsync() => _service.CheckAsync("helen229", _repository.Split('/')[1], 1, Head, CancellationToken.None);
    private IEnumerable<(HttpMethod Method, Uri Uri, string? Token)> ArmRequests() => _requests.Where(request => request.Uri.Host == "management.azure.com");
    private static string Key(string repository, string commit, string path) => $"{repository}|{commit}|{path}";
    private void SetFile(string repository, string commit, string path, string content) => _files[Key(repository, commit, path)] = content;

    private void AddPackage(string package, string metadataFile, string version)
    {
        _changedFiles.Add($"{package}/Generated.cs");
        SetFile(_repository, Head, $"{package}/tsp-location.yaml", $"directory: {Project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(_repository, Head, $"{package}/{metadataFile}", JsonSerializer.Serialize(new { apiVersions = new Dictionary<string, string> { ["Microsoft.Example"] = version } }));
        SetFile(SpecRepository, SpecCommit, $"{Project}/main.tsp", "@armProviderNamespace\n@service\nnamespace Microsoft.Example;");
        SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml", Config("{version-status}/{version}/example.json"));
        SetSwagger(version, "/providers/Microsoft.Example/widgets/{name}", true);
    }

    private static string Config(string output) => $"options:\n  \"@azure-tools/typespec-autorest\":\n    emitter-output-dir: \"{{project-root}}/../swagger\"\n    output-file: \"{output}\"\n";

    private void SetSwagger(string version, string route, bool collection)
    {
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{version}/example.json", JsonSerializer.Serialize(new
        {
            info = new { version },
            paths = new Dictionary<string, object> { [route] = new Dictionary<string, object> { [collection ? "get" : "post"] = new { } } }
        }));
    }

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var uri = request.RequestUri!;
        _requests.Add((request.Method, uri, request.Headers.Authorization?.Parameter));
        if (uri.Host == "management.azure.com")
        {
            return _armStatus == HttpStatusCode.OK ? Json(new
            {
                @namespace = "Microsoft.Example",
                resourceTypes = new[] { new { resourceType = "widgets", apiVersions = _advertisedVersions, locations = new[] { "East US" } } }
            }) : Json(new { error = "private transport details" }, _armStatus);
        }
        if (_githubStatus != HttpStatusCode.OK)
        {
            return Json(new { error = "private transport details" }, _githubStatus);
        }
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (path == $"/repos/{_repository}/pulls/1")
        {
            _pullRequestReads++;
            return Json(new
            {
                state = "open",
                body = "Configurations: human notes; API version 2026-04-01. This is not evidence.",
                head = new { sha = _changeHead && _pullRequestReads > 1 ? new string('c', 40) : Head, repo = new { full_name = _repository } },
                labels = _labels.Select(label => new { name = label }).ToArray()
            });
        }
        if (path == $"/repos/{_repository}/pulls/1/files")
        {
            var files = _changedFiles.Select(filename => new { filename, status = "modified" }).ToArray();
            return Json(_truncatedFiles ? Enumerable.Repeat(files[0], 100).ToArray() : files);
        }
        var marker = "/contents/";
        var contentIndex = path.IndexOf(marker, StringComparison.Ordinal);
        if (contentIndex > 0)
        {
            var repository = path["/repos/".Length..contentIndex];
            var filePath = path[(contentIndex + marker.Length)..];
            var commit = QueryHelpers.ParseQuery(uri.Query)["ref"].ToString();
            var key = Key(repository, commit, filePath);
            if (_files.TryGetValue(key, out var file))
            {
                return Json(new { type = "file", encoding = "base64", content = Convert.ToBase64String(Encoding.UTF8.GetBytes(file)) });
            }
            if (_directories.TryGetValue(key, out var directory))
            {
                return Json(directory);
            }
        }
        return Json(new { message = "Not found" }, HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };
}
