using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.AspNetCore.WebUtilities;
using Moq;
using Moq.Protected;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class ServiceApiReadinessServiceTests
{
    private const string Head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string BaseCommit = "cccccccccccccccccccccccccccccccccccccccc";
    private const string SpecCommit = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Version = "2026-04-01";
    private const string Project = "specification/example/resource-manager/Example";
    private const string SpecRepository = "Azure/azure-rest-api-specs";
    private const string Package = "sdk/example/azure-resourcemanager-example";
    private const string Metadata = "src/main/resources/META-INF/azure-resourcemanager-example_metadata.json";
    private const string SwaggerDirectory = "specification/example/resource-manager/swagger";
    private const string ManagementConfiguration = "linter:\n  extends: ['@azure-tools/typespec-azure-rulesets/resource-manager']\n";
    private const string DataConfiguration = "linter:\n  extends: ['@azure-tools/typespec-azure-rulesets/data-plane']\n";
    private readonly Dictionary<string, string> _files = [];
    private readonly Dictionary<string, JsonArray> _directories = [];
    private readonly HashSet<string> _rawFiles = [];
    private readonly List<string> _changedFiles = [];
    private readonly HashSet<string> _removedFiles = [];
    private readonly Dictionary<string, string> _renamedFiles = [];
    private readonly List<string> _labels = [];
    private readonly List<(HttpMethod Method, Uri Uri, string? Token)> _requests = [];
    private Mock<IGitHubService> _github = null!;
    private Mock<IAzureService> _azure = null!;
    private ServiceApiReadinessService _service = null!;
    private HttpClient _http = null!;
    private string _repository = "helen229/azure-sdk-for-java";
    private string[] _advertisedVersions = [Version];
    private readonly Dictionary<string, string[]> _providerVersions = [];
    private HttpStatusCode _githubStatus;
    private HttpStatusCode _evidenceStatus;
    private HttpStatusCode _armStatus;
    private HttpStatusCode _rawStatus;
    private int _pullRequestReads;
    private bool _changeHead;
    private bool _missingHeadRepository;
    private bool _truncatedFiles;

    [SetUp]
    public void SetUp()
    {
        _files.Clear();
        _directories.Clear();
        _rawFiles.Clear();
        _changedFiles.Clear();
        _removedFiles.Clear();
        _renamedFiles.Clear();
        _labels.Clear();
        _requests.Clear();
        _repository = "helen229/azure-sdk-for-java";
        _advertisedVersions = [Version];
        _providerVersions.Clear();
        _githubStatus = HttpStatusCode.OK;
        _evidenceStatus = HttpStatusCode.OK;
        _armStatus = HttpStatusCode.OK;
        _rawStatus = HttpStatusCode.OK;
        _pullRequestReads = 0;
        _changeHead = false;
        _missingHeadRepository = false;
        _truncatedFiles = false;
        _github = new Mock<IGitHubService>();
        _github.Setup(service => service.GetAuthToken(It.IsAny<CancellationToken>())).Returns("github-test-token");
        var credential = new Mock<TokenCredential>();
        credential.Setup(value => value.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<AccessToken>(new AccessToken("azure-test-token", DateTimeOffset.UtcNow.AddHours(1))));
        _azure = new Mock<IAzureService>();
        _azure.Setup(service => service.GetCredential(It.IsAny<string?>())).Returns(credential.Object);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Respond(request));
            });
        var factory = new Mock<IHttpClientFactory>();
        _http = new HttpClient(handler.Object);
        factory.Setup(value => value.CreateClient(ServiceApiReadinessService.HttpClientName)).Returns(_http);
        _service = new ServiceApiReadinessService(_github.Object, _azure.Object, factory.Object,
            new PackageApiMetadataService(_github.Object, factory.Object));
        AddPackage(Package, Metadata, Version);
    }

    [TearDown]
    public void TearDown() => _http.Dispose();

    [Test]
    public async Task ReadinessConsumesTheInjectedMetadataInterface()
    {
        var package = new PackageInfo
        {
            DirectoryPath = Package,
            SdkType = SdkType.Management,
            ApiVersions = [new(Version, "Microsoft.Example")],
            ResolvedApiVersions = [new(Version, "Microsoft.Example")],
            ResourceTypes = [new("Microsoft.Example", Version, "widgets")]
        };
        var metadata = new Mock<IPackageApiMetadataService>(MockBehavior.Strict);
        metadata.Setup(service => service.ReadLocalGenerationEvidenceAsync(package, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        metadata.Setup(service => service.PopulateResourceTypesAsync(package, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(clients => clients.CreateClient(ServiceApiReadinessService.HttpClientName)).Returns(_http);
        var service = new ServiceApiReadinessService(_github.Object, _azure.Object, factory.Object, metadata.Object);

        var result = await service.CheckPackageAsync(package, CancellationToken.None);

        Assert.That(result.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        metadata.Verify(service => service.ReadLocalGenerationEvidenceAsync(package, It.IsAny<CancellationToken>()), Times.Once);
        metadata.Verify(service => service.PopulateResourceTypesAsync(package, It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(service => service.GetAuthToken(It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReadinessProjectsReferencePackageInfoAndPreserveSourceJson(bool scalarVersion)
    {
        if (scalarVersion)
        {
            SetFile(_repository, Head, $"{Package}/{Metadata}", JsonSerializer.Serialize(new { apiVersion = Version }));
        }

        var response = await CheckAsync();
        var package = response.Packages.Single();
        var project = response.Projects.Single();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var serializedProject = json.RootElement.GetProperty("projects")[0];
        var source = serializedProject.GetProperty("source");

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(project.Package, Is.SameAs(package));
        Assert.That(project.ApiVersion, Is.EqualTo(package.ResolvedApiVersions.Single()));
        Assert.That(source.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[]
        {
            "packagePath", "configuration", "commitSha", "specRepository", "apiVersion", "expectedNamespace", "metadataFiles"
        }));
        Assert.That(source.GetProperty("packagePath").GetString(), Is.EqualTo(Package));
        Assert.That(source.GetProperty("configuration").GetString(), Is.EqualTo($"{Project}/tspconfig.yaml"));
        Assert.That(source.GetProperty("commitSha").GetString(), Is.EqualTo(SpecCommit));
        Assert.That(source.GetProperty("specRepository").GetString(), Is.EqualTo(SpecRepository));
        Assert.That(source.GetProperty("apiVersion").GetString(), Is.EqualTo(Version));
        Assert.That(source.GetProperty("expectedNamespace").GetString(), Is.EqualTo("Microsoft.Example"));
        Assert.That(source.GetProperty("metadataFiles").Deserialize<string[]>(), Is.EqualTo(package.MetadataFiles));
        Assert.That(serializedProject.TryGetProperty("Package", out _), Is.False);
        Assert.That(json.RootElement.GetProperty("packages")[0].TryGetProperty("ResolvedApiVersions", out _), Is.False);
    }

    [Test]
    public async Task MetadataOnlyReturnsCurrentHeadRequirementsWithoutArmAuthentication()
    {
        var response = await _service.GetPackageMetadataAsync("helen229", "azure-sdk-for-java", 1, CancellationToken.None);

        Assert.That(response.HeadSha, Is.EqualTo(Head));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Packages.Single().ResourceTypes, Has.Length.EqualTo(1));
        Assert.That(response.Projects.Single().Package, Is.SameAs(response.Packages.Single()));
        Assert.That(response.Detail, Does.Contain("no ARM"));
        Assert.That(ArmRequests(), Is.Empty);
        _azure.Verify(service => service.GetCredential(It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task ValidatingPopulatedMetadataDoesNotReadSdkOrSpecificationFilesAgain()
    {
        var metadata = await _service.GetPackageMetadataAsync("helen229", "azure-sdk-for-java", 1, CancellationToken.None);
        var package = metadata.Packages.Single();
        var githubReads = _requests.Count(request => request.Uri.Host != "management.azure.com");

        var response = await _service.ValidateAsync(metadata, CancellationToken.None);

        Assert.That(response, Is.SameAs(metadata));
        Assert.That(response.Packages.Single(), Is.SameAs(package));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(_requests.Count(request => request.Uri.Host != "management.azure.com"), Is.EqualTo(githubReads));
        Assert.That(_pullRequestReads, Is.EqualTo(1));
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
    }

    [TestCase("missing")]
    [TestCase("data-plane")]
    [TestCase("error")]
    public async Task UnusableMetadataCannotBecomePassingOrStartMoreEvidenceReads(string kind)
    {
        if (kind == "missing")
        {
            _files.Remove(Key(_repository, Head, $"{Package}/{Metadata}"));
        }
        else if (kind == "data-plane")
        {
            SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml", DataConfiguration);
        }
        else
        {
            _githubStatus = HttpStatusCode.Forbidden;
        }
        var metadata = await _service.GetPackageMetadataAsync("helen229", "azure-sdk-for-java", 1, CancellationToken.None);
        var status = metadata.ReadinessStatus;
        var reads = _requests.Count;

        var response = await _service.ValidateAsync(metadata, CancellationToken.None);

        Assert.That(response, Is.SameAs(metadata));
        Assert.That(response.ReadinessStatus, Is.EqualTo(status).And.Not.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(_requests, Has.Count.EqualTo(reads));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PartialSwaggerEvidenceCannotProducePassingMetadataOrReadiness(bool invalidPaths)
    {
        SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml", Config("{service-name}/{version-status}/{version}/example.json"));
        _directories[Key(SpecRepository, SpecCommit, SwaggerDirectory)] = JsonSerializer.SerializeToNode(new[]
        {
            new { name = "first", type = "dir" }, new { name = "second", type = "dir" }
        })!.AsArray();
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/first/stable/{Version}/example.json",
            _files[Key(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json")]);
        if (invalidPaths)
        {
            SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/second/stable/{Version}/example.json",
                JsonSerializer.Serialize(new { info = new { version = Version }, paths = 42 }));
        }

        var response = await CheckAsync();

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Packages.Single().MetadataIssues, Is.Not.Empty);
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task DataPlaneEnrichmentPreservesExistingPackageApiVersions()
    {
        using var directory = TempDirectory.Create("DataPlaneMetadataPreservation");
        await File.WriteAllTextAsync(Path.Combine(directory.DirectoryPath, "tsp-location.yaml"),
            $"directory: {Project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml", DataConfiguration);
        var package = new PackageInfo
        {
            PackagePath = directory.DirectoryPath,
            Language = SdkLanguage.JavaScript,
            ApiVersions = [new(Version)]
        };

        var result = await new SdkPackageMetadataHelper(_http, "github-test-token").ReadLocalGenerationEvidenceAsync(package);

        Assert.That(result, Is.SameAs(package));
        Assert.That(result.SdkType, Is.EqualTo(SdkType.Dataplane));
        Assert.That(result.ApiVersions.Single().Version, Is.EqualTo(Version));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task PopulatesEveryPackageInfoBeforeCheckingArmReadiness()
    {
        AddPackage($"{Package}-second", Metadata, Version);
        var populatedPackages = new List<PackageInfo>();
        var packageMetadataService = new Mock<IPackageApiMetadataService>(MockBehavior.Strict);
        packageMetadataService.Setup(service => service.PopulateResourceTypesAsync(It.IsAny<PackageInfo>(), It.IsAny<CancellationToken>()))
            .Returns<PackageInfo, CancellationToken>((package, cancellationToken) =>
            {
                Assert.That(ArmRequests(), Is.Empty, "Every package must be enriched before ARM validation starts.");
                Assert.That(package.TypeSpecLocation?.Commit, Is.EqualTo(SpecCommit));
                package.ResourceTypes = [new("Microsoft.Example", Version, "widgets")];
                populatedPackages.Add(package);
                return Task.FromResult(package);
            });
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(factory => factory.CreateClient(ServiceApiReadinessService.HttpClientName)).Returns(_http);
        var service = new ServiceApiReadinessService(_github.Object, _azure.Object, clients.Object, packageMetadataService.Object);

        var response = await service.CheckAsync("helen229", "azure-sdk-for-java", 1, CancellationToken.None);

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed), response.Detail);
        Assert.That(populatedPackages, Has.Count.EqualTo(2));
        Assert.That(response.Packages, Is.EqualTo(populatedPackages));
        Assert.That(response.Projects, Has.Count.EqualTo(2));
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
    }

    [TestCase(SdkLanguage.Java, "src/main/resources/META-INF", "*_metadata.json")]
    [TestCase(SdkLanguage.JavaScript, "", "metadata.json")]
    [TestCase(SdkLanguage.DotNet, "", "metadata.json")]
    [TestCase(SdkLanguage.Python, "", "_metadata.json")]
    [TestCase(SdkLanguage.Go, "testdata", "_metadata.json")]
    [TestCase(SdkLanguage.Unknown, "", "")]
    public void MetadataLocationsAreSharedByLocalAndRemoteReaders(SdkLanguage language, string directory, string pattern)
    {
        Assert.That(GeneratedSdkMetadataHelper.GetMetadataFileLocation(language), Is.EqualTo((directory, pattern)));
    }

    [TestCase("java", Package, Metadata)]
    [TestCase("net", "sdk/example/Azure.ResourceManager.Example", "metadata.json")]
    [TestCase("js", "sdk/example/arm-example", "metadata.json")]
    [TestCase("python", "sdk/example/azure-mgmt-example", "_metadata.json")]
    [TestCase("go", "sdk/resourcemanager/example/armexample", "testdata/_metadata.json")]
    [TestCase("java", "sdk/example/generated/azure-resourcemanager-example", Metadata)]
    [TestCase("go", "sdk/resourcemanager/example/armexample/v2", "testdata/_metadata.json")]
    public async Task ReadsExactCurrentHeadMetadataForEachSdkLayout(string language, string package, string metadata)
    {
        _files.Clear();
        _changedFiles.Clear();
        _repository = $"helen229/azure-sdk-for-{language}";
        AddPackage(package, metadata, Version);
        var response = await CheckAsync();
        var providerRequest = _requests.Single(request => request.Uri.Host == "management.azure.com").Uri;
        Assert.Multiple(() =>
        {
            Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
            Assert.That(providerRequest.AbsolutePath, Is.EqualTo("/providers/Microsoft.Example"));
            Assert.That(QueryHelpers.ParseQuery(providerRequest.Query)["api-version"].ToString(), Is.EqualTo("2021-04-01"));
            Assert.That(response.HeadSha, Is.EqualTo(Head));
            Assert.That(response.Projects.Single().Package.DirectoryPath.ToString(), Is.EqualTo(package));
            Assert.That(response.Projects.Single().ApiVersion.Version, Is.EqualTo(Version));
            Assert.That(response.Projects.Single().Package.TypeSpecLocation?.Commit, Is.EqualTo(SpecCommit));
            Assert.That(response.Projects.Single().Matches.Single().Regions, Is.EqualTo(new[] { "East US" }));
            Assert.That(_pullRequestReads, Is.EqualTo(1));
            Assert.That(_requests.All(request => request.Method == HttpMethod.Get), Is.True);
            Assert.That(_requests.Where(request => request.Uri.Host == "api.github.com").All(request => request.Token == "github-test-token"), Is.True);
            Assert.That(_requests.Where(request => request.Uri.Host == "management.azure.com").All(request => request.Token == "azure-test-token"), Is.True);
        });
    }

    [Test]
    public async Task PackageChangelogOnlyChangeStillIdentifiesItsPackage()
    {
        _changedFiles.Clear();
        _changedFiles.Add($"{Package}/CHANGELOG.md");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed), response.Detail);
        Assert.That(response.Projects.Single().Package.DirectoryPath.ToString(), Is.EqualTo(Package));
    }

    [Test]
    public async Task DiscoversAllPackageInfoObjectsBeforeReadingGenerationMetadata()
    {
        var secondPackage = $"{Package}-second";
        AddPackage(secondPackage, Metadata, Version);
        _rawFiles.Add(Key(_repository, Head, $"{secondPackage}/{Metadata}"));
        _rawStatus = HttpStatusCode.Forbidden;

        var response = await CheckAsync();

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(response.Packages.Select(package => package.DirectoryPath), Is.EquivalentTo(new[] { Package, secondPackage }));
        Assert.That(response.Packages.Single(package => package.DirectoryPath == Package).ApiVersions.Single().Version, Is.EqualTo(Version));
        Assert.That(response.Packages.All(package => package.TypeSpecLocation?.Commit == SpecCommit), Is.True);
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task KeepsGenerationMetadataAssociatedWithEachPackageDirectory()
    {
        var dataPackage = $"{Package}-data";
        var invalidPackage = $"{Package}-invalid";
        var dataProject = "specification/other/data-plane/Other";
        var dataCommit = new string('d', 40);
        AddPackage(dataPackage, Metadata, Version);
        AddPackage(invalidPackage, Metadata, Version);
        SetFile(_repository, Head, $"{dataPackage}/tsp-location.yaml", $"directory: {dataProject}\ncommit: {dataCommit}\nrepo: {SpecRepository}\n");
        SetFile(SpecRepository, dataCommit, $"{dataProject}/tspconfig.yaml", DataConfiguration);
        SetFile(_repository, Head, $"{invalidPackage}/tsp-location.yaml", $"directory: {Project}\ncommit: not-a-commit\nrepo: {SpecRepository}\n");

        var response = await CheckAsync();
        var management = response.Packages.Single(package => package.DirectoryPath == Package);
        var data = response.Packages.Single(package => package.DirectoryPath == dataPackage);
        var invalid = response.Packages.Single(package => package.DirectoryPath == invalidPackage);

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(management.SdkType, Is.EqualTo(SdkType.Management));
        Assert.That(management.TypeSpecLocation?.Commit, Is.EqualTo(SpecCommit));
        Assert.That(management.ApiVersions.Single().Version, Is.EqualTo(Version));
        Assert.That(management.MetadataIssues, Is.Empty);
        Assert.That(data.SdkType, Is.EqualTo(SdkType.Dataplane));
        Assert.That(data.TypeSpecLocation?.Directory, Is.EqualTo(dataProject));
        Assert.That(data.TypeSpecLocation?.Commit, Is.EqualTo(dataCommit));
        Assert.That(data.MetadataIssues, Is.Empty);
        Assert.That(invalid.SdkType, Is.EqualTo(SdkType.Unknown));
        Assert.That(invalid.TypeSpecLocation, Is.Null);
        Assert.That(invalid.MetadataIssues.Single(), Does.Contain(invalidPackage));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [TestCase(SdkLanguage.JavaScript, "metadata.json")]
    [TestCase(SdkLanguage.Java, Metadata)]
    [TestCase(SdkLanguage.Go, "testdata/_metadata.json")]
    [TestCase(SdkLanguage.Python, "_metadata.json")]
    [TestCase(SdkLanguage.DotNet, "metadata.json")]
    public async Task LocalPackageMetadataDoesNotRequireAGitHubPullRequest(SdkLanguage language, string metadataFile)
    {
        using var directory = TempDirectory.Create("LocalPackageMetadata");
        var packagePath = Path.Combine(directory.DirectoryPath, Package);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(packagePath, metadataFile))!);
        await File.WriteAllTextAsync(Path.Combine(packagePath, "tsp-location.yaml"), $"directory: {Project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        await File.WriteAllTextAsync(Path.Combine(packagePath, metadataFile), JsonSerializer.Serialize(new
        {
            apiVersions = new Dictionary<string, string> { ["Microsoft.Example"] = Version, ["Microsoft.Other"] = "2026-03-01" }
        }));
        var package = new PackageInfo { PackagePath = packagePath, RepoRoot = directory.DirectoryPath, Language = language };

        var evidence = await new SdkPackageMetadataHelper(_http, "github-test-token").ReadLocalGenerationEvidenceAsync(package, CancellationToken.None);

        Assert.That(evidence, Is.SameAs(package));
        Assert.That(evidence.SdkType, Is.EqualTo(SdkType.Management));
        Assert.That(evidence.TypeSpecLocation?.Directory, Is.EqualTo(Project));
        Assert.That(evidence.TypeSpecLocation?.Commit, Is.EqualTo(SpecCommit));
        Assert.That(evidence.TypeSpecLocation?.Repository, Is.EqualTo(SpecRepository));
        Assert.That(evidence.MetadataIssues, Is.Empty);
        Assert.That(evidence.ApiVersions, Has.Length.EqualTo(2));
        Assert.That(evidence.DirectoryPath.ToString(), Is.EqualTo(Package));
        Assert.That(evidence.MetadataFiles, Does.Contain($"{Package}/tsp-location.yaml").And.Contain($"{Package}/{metadataFile}"));
        Assert.That(_requests, Has.Count.EqualTo(1));
        Assert.That(Uri.UnescapeDataString(_requests.Single().Uri.AbsolutePath), Does.EndWith($"{Project}/tspconfig.yaml"));
        Assert.That(QueryHelpers.ParseQuery(_requests.Single().Uri.Query)["ref"].ToString(), Is.EqualTo(SpecCommit));
        Assert.That(_pullRequestReads, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SharedMetadataServiceReturnsPackageInfoWithoutAnArmLookup(bool scalarVersion)
    {
        using var directory = TempDirectory.Create("SharedPackageInfo");
        var packagePath = Path.Combine(directory.DirectoryPath, Package);
        Directory.CreateDirectory(packagePath);
        await File.WriteAllTextAsync(Path.Combine(packagePath, "tsp-location.yaml"), $"directory: {Project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        var metadata = scalarVersion
            ? JsonSerializer.Serialize(new { apiVersion = Version })
            : JsonSerializer.Serialize(new { apiVersions = new Dictionary<string, string> { ["Microsoft.Example"] = Version, ["Microsoft.Other"] = Version } });
        await File.WriteAllTextAsync(Path.Combine(packagePath, "metadata.json"), metadata);
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json", JsonSerializer.Serialize(new
        {
            info = new { version = Version },
            paths = new Dictionary<string, object>
            {
                ["/providers/Microsoft.Example/widgets/{name}"] = new { get = new { } },
                ["/providers/Microsoft.Other/widgets/{name}"] = new { get = new { } }
            }
        }));
        var package = new PackageInfo { PackageName = "local-package", PackagePath = packagePath, RepoRoot = directory.DirectoryPath, Language = SdkLanguage.JavaScript };
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(factory => factory.CreateClient(PackageApiMetadataService.HttpClientName)).Returns(_http);

        var result = await new PackageApiMetadataService(_github.Object, clients.Object).GetPackageInfoAsync(package);

        Assert.That(result, Is.SameAs(package));
        Assert.That(result.PackageName, Is.EqualTo("local-package"));
        Assert.That(result.TypeSpecLocation?.Commit, Is.EqualTo(SpecCommit));
        Assert.That(result.ApiVersions.Length, Is.EqualTo(scalarVersion ? 1 : 2));
        Assert.That(result.ResourceTypes.Select(type => type.Namespace), Is.EquivalentTo(new[] { "Microsoft.Example", "Microsoft.Other" }));
        Assert.That(result.ResourceTypes.All(type => type.ApiVersion == Version), Is.True);
        Assert.That(result.ResolvedApiVersions.Select(version => version.Namespace), Is.EquivalentTo(new[] { "Microsoft.Example", "Microsoft.Other" }));
        Assert.That(result.MetadataIssues, Is.Empty);
        Assert.That(ArmRequests(), Is.Empty);
        Assert.That(_pullRequestReads, Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task LocalPackageReadinessUsesTheSharedPackageInfo(bool advertised)
    {
        using var directory = TempDirectory.Create("LocalPackageReadiness");
        var packagePath = Path.Combine(directory.DirectoryPath, Package);
        Directory.CreateDirectory(packagePath);
        await File.WriteAllTextAsync(Path.Combine(packagePath, "tsp-location.yaml"), $"directory: {Project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        await File.WriteAllTextAsync(Path.Combine(packagePath, "metadata.json"), JsonSerializer.Serialize(new
        {
            apiVersions = new Dictionary<string, string> { ["Microsoft.Example"] = Version }
        }));
        var package = new PackageInfo { PackageName = "local-package", PackagePath = packagePath, RepoRoot = directory.DirectoryPath, Language = SdkLanguage.JavaScript };
        _advertisedVersions = advertised ? [Version] : ["2025-01-01"];

        var response = await _service.CheckPackageAsync(package, CancellationToken.None);

        Assert.That(response.ReadinessStatus, Is.EqualTo(advertised ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.NotDetected));
        Assert.That(response.InputKind, Is.EqualTo("package"));
        Assert.That(response.HeadSha, Is.Empty);
        Assert.That(response.Packages.Single(), Is.SameAs(package));
        Assert.That(package.ResourceTypes, Is.EqualTo(new[] { new PackageResourceType("Microsoft.Example", Version, "widgets") }));
        Assert.That(response.Detail, Does.Contain("local SDK metadata").And.Contain("not a PR-head attestation"));
        Assert.That(_pullRequestReads, Is.Zero);
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task ScalarVersionChecksEveryProviderDiscoveredInTheProject()
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", JsonSerializer.Serialize(new { apiVersion = Version }));
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json", JsonSerializer.Serialize(new
        {
            info = new { version = Version },
            paths = new Dictionary<string, object>
            {
                ["/providers/Microsoft.Example/widgets/{name}"] = new { get = new { } },
                ["/providers/Microsoft.Other/widgets/{name}"] = new { get = new { } }
            }
        }));
        _providerVersions["Microsoft.Other"] = ["2025-01-01"];

        var response = await CheckAsync();

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.NotDetected));
        Assert.That(response.Projects, Has.Count.EqualTo(2));
        Assert.That(response.Packages.Single().ApiVersions.Single().Namespace, Is.Null);
        Assert.That(ArmRequests().Count(), Is.EqualTo(2));
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task MixedJavaMetadataKeepsEveryProviderRegardlessOfFilenameOrder(bool mappedFileFirst, bool otherProviderAdvertisesVersion)
    {
        var scalar = JsonSerializer.Serialize(new { apiVersion = Version });
        var mapped = JsonSerializer.Serialize(new { apiVersions = new Dictionary<string, string> { ["Microsoft.Example"] = Version } });
        SetFile(_repository, Head, $"{Package}/{Metadata}", mappedFileFirst ? mapped : scalar);
        SetFile(_repository, Head, $"{Package}/src/main/resources/META-INF/z_other_metadata.json", mappedFileFirst ? scalar : mapped);
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json", JsonSerializer.Serialize(new
        {
            info = new { version = Version },
            paths = new Dictionary<string, object>
            {
                ["/providers/Microsoft.Example/widgets/{name}"] = new { get = new { } },
                ["/providers/Microsoft.Other/widgets/{name}"] = new { get = new { } }
            }
        }));
        _providerVersions["Microsoft.Other"] = otherProviderAdvertisesVersion ? [Version] : ["2025-01-01"];

        var response = await CheckAsync();

        Assert.That(response.ReadinessStatus, Is.EqualTo(otherProviderAdvertisesVersion ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.NotDetected));
        Assert.That(response.Projects.Select(project => project.Namespace), Is.EquivalentTo(new[] { "Microsoft.Example", "Microsoft.Other" }));
        Assert.That(response.Packages.Single().ResourceTypes.Select(type => type.Namespace), Is.EquivalentTo(new[] { "Microsoft.Example", "Microsoft.Other" }));
        Assert.That(ArmRequests().Count(), Is.EqualTo(2));
    }

    [TestCase("/providers/Microsoft.Other/widgets/{name}/{childType}/{childName}")]
    [TestCase("/providers/Microsoft.Other/{resourceType}/{name}")]
    [TestCase("/providers/{providerNamespace}/widgets/{name}")]
    public async Task UnresolvedSecondProviderCannotBeHiddenByAPassingFirstProvider(string unresolvedRoute)
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", JsonSerializer.Serialize(new { apiVersion = Version }));
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json", JsonSerializer.Serialize(new
        {
            info = new { version = Version },
            paths = new Dictionary<string, object>
            {
                ["/providers/Microsoft.Example/widgets/{name}"] = new { get = new { } },
                [unresolvedRoute] = new { get = new { } }
            }
        }));

        var response = await CheckAsync();

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Projects.All(project => project.ReadinessStatus != ServiceApiReadinessResponse.Passed), Is.True);
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task ExplicitProviderMetadataDoesNotRequireAnUnreferencedProviderMapping()
    {
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json", JsonSerializer.Serialize(new
        {
            info = new { version = Version },
            paths = new Dictionary<string, object>
            {
                ["/providers/Microsoft.Example/widgets/{name}"] = new { get = new { } },
                ["/providers/Microsoft.Other/widgets/{name}/{childType}/{childName}"] = new { get = new { } }
            }
        }));

        var response = await CheckAsync();

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.Projects.Single().Namespace, Is.EqualTo("Microsoft.Example"));
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task ExtensionResourceChecksItsOwningProviderRatherThanTheParentScope()
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", JsonSerializer.Serialize(new { apiVersion = Version }));
        SetSwagger(Version, "/providers/Microsoft.Parent/parents/{parentName}/providers/Microsoft.Example/widgets/{name}", true);

        var response = await CheckAsync();

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.Projects.Single().Namespace, Is.EqualTo("Microsoft.Example"));
        Assert.That(response.Packages.Single().ResourceTypes.Single().Namespace, Is.EqualTo("Microsoft.Example"));
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task PackageDiscoveryReusesGenerationLocationReadsAcrossChangedFiles()
    {
        _changedFiles.Add($"{Package}/src/Generated2.cs");
        _changedFiles.Add($"{Package}/src/Generated3.cs");
        var response = await CheckAsync();
        var locations = _requests.Where(request => request.Uri.AbsolutePath.EndsWith("/tsp-location.yaml", StringComparison.Ordinal));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(locations.Count(), Is.EqualTo(2));
        Assert.That(locations.GroupBy(request => request.Uri).All(group => group.Count() == 1), Is.True);
    }

    [Test]
    public async Task PackageDiscoveryHasABoundedDirectoryBudget()
    {
        _changedFiles.Clear();
        _changedFiles.Add($"{Package}/{string.Join('/', Enumerable.Repeat("nested", 129))}/Generated.cs");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(_requests.Count(request => request.Uri.AbsolutePath.EndsWith("/tsp-location.yaml", StringComparison.Ordinal)), Is.EqualTo(128));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [TestCase("CHANGELOG.md")]
    [TestCase("eng/scripts/check.ps1")]
    [TestCase("sdk/test/example/source.cs")]
    public async Task FilesOutsideSdkPackagesDoNotProduceReadinessEvidence(string path)
    {
        _changedFiles.Clear();
        _changedFiles.Add(path);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Projects, Is.Empty);
        Assert.That(ArmRequests(), Is.Empty);
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

    [TestCase(Version, true)]
    [TestCase(Version, false)]
    [TestCase("2026-03-01", true)]
    [TestCase("2026-03-01", false)]
    public async Task OnePackageKeepsEveryProviderAndVersionDistinct(string otherVersion, bool otherProviderAdvertisesVersion)
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", JsonSerializer.Serialize(new
        {
            apiVersions = new Dictionary<string, string> { ["Microsoft.Example"] = Version, ["Microsoft.Other"] = otherVersion }
        }));
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{otherVersion}/example.json", JsonSerializer.Serialize(new
        {
            info = new { version = otherVersion },
            paths = new Dictionary<string, object>
            {
                ["/providers/Microsoft.Example/widgets/{name}"] = new { get = new { } },
                ["/providers/Microsoft.Other/widgets/{name}"] = new { get = new { } }
            }
        }));
        _providerVersions["Microsoft.Other"] = otherProviderAdvertisesVersion ? [otherVersion] : ["2025-01-01"];

        var response = await CheckAsync();
        var package = response.Packages.Single();

        Assert.That(response.ReadinessStatus, Is.EqualTo(otherProviderAdvertisesVersion ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.NotDetected));
        Assert.That(package.ApiVersions, Is.EquivalentTo(new[] { new PackageApiVersion(Version, "Microsoft.Example"), new PackageApiVersion(otherVersion, "Microsoft.Other") }));
        Assert.That(package.ResourceTypes, Is.EquivalentTo(new[] { new PackageResourceType("Microsoft.Example", Version, "widgets"), new PackageResourceType("Microsoft.Other", otherVersion, "widgets") }));
        Assert.That(response.Projects, Has.Count.EqualTo(2));
        Assert.That(ArmRequests().Count(), Is.EqualTo(2));
    }

    [TestCase("js", "sdk/example/arm-example", "metadata.json", Version)]
    [TestCase("python", "sdk/example/azure-mgmt-example", "_metadata.json", Version)]
    [TestCase("js", "sdk/example/arm-example", "metadata.json", " 2026-04-01 ")]
    [TestCase("python", "sdk/example/azure-mgmt-example", "_metadata.json", " 2026-04-01 ")]
    public async Task SingularApiVersionUsesThePinnedProviderNamespace(string language, string package, string metadata, string version)
    {
        _files.Clear();
        _changedFiles.Clear();
        _repository = $"helen229/azure-sdk-for-{language}";
        AddPackage(package, metadata, Version);
        SetFile(_repository, Head, $"{package}/{metadata}", JsonSerializer.Serialize(new { apiVersion = version }));
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.Projects.Single().Namespace, Is.EqualTo("Microsoft.Example"));
        Assert.That(response.Projects.Single().ApiVersion.Version, Is.EqualTo(Version));
    }

    [Test]
    public async Task UnambiguousVersionArrayUsesSharedMetadataParser()
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", "{\"apiVersions\":{\"Microsoft.Example\":[\"2026-04-01\"]}}");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
    }

    [TestCase("alternate_metadata.json")]
    [TestCase("alternate client_metadata.json")]
    public async Task JavaDiscoversAlternateMetadataFilenameAtThePinnedHead(string filename)
    {
        var metadata = _files[Key(_repository, Head, $"{Package}/{Metadata}")];
        _files.Remove(Key(_repository, Head, $"{Package}/{Metadata}"));
        var alternate = Package + "/src/main/resources/META-INF/" + filename;
        SetFile(_repository, Head, alternate, metadata);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.Projects.Single().Package.MetadataFiles, Does.Contain(alternate));
    }

    [Test]
    public async Task JavaConflictingMetadataFileCannotBeIgnored()
    {
        SetFile(_repository, Head, $"{Package}/src/main/resources/META-INF/other_metadata.json",
            "{\"apiVersions\":{\"Microsoft.Example\":\"2026-03-01\"}}");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Detail, Does.Contain("conflicting API versions across SDK metadata files"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task JavaMalformedAdditionalMetadataFilePreventsPassed()
    {
        SetFile(_repository, Head, $"{Package}/src/main/resources/META-INF/other_metadata.json", "{invalid");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task JavaConsistentMetadataFilesAreAllRecordedButDoNotDuplicateTheProject()
    {
        var metadata = _files[Key(_repository, Head, $"{Package}/{Metadata}")];
        const string other = Package + "/src/main/resources/META-INF/other_metadata.json";
        SetFile(_repository, Head, other, metadata);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.Projects.Count, Is.EqualTo(1));
        Assert.That(response.Projects.Single().Package.MetadataFiles, Has.Length.EqualTo(3).And.Contains(other));
    }

    [Test]
    public async Task JavaMetadataDiscoveryIsBounded()
    {
        for (var index = 0; index < 33; index++)
        {
            SetFile(_repository, Head, $"{Package}/src/main/resources/META-INF/extra{index}_metadata.json", "{}");
        }
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Detail, Does.Contain("32-file evidence limit"));
        Assert.That(ArmRequests(), Is.Empty);
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

    [TestCase("{}")]
    [TestCase("{broken json")]
    [TestCase("{\"apiVersions\":{\"Microsoft.Example\":[\"2026-04-01\",\"2026-03-01\"]}}")]
    public async Task IncompleteGenerationKeepsDiagnosticsOnPackageInfo(string metadata)
    {
        SetFile(_repository, Head, $"{Package}/{Metadata}", metadata);

        var response = await CheckAsync();
        var package = response.Packages.Single();

        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(package.DirectoryPath.ToString(), Is.EqualTo(Package));
        Assert.That(package.SdkType, Is.EqualTo(SdkType.Management));
        Assert.That(package.ResolvedApiVersions, Is.Empty);
        Assert.That(package.MetadataIssues, Is.Not.Empty);
        Assert.That(package.MetadataIssues.All(issue => response.Detail.Contains(issue, StringComparison.Ordinal)), Is.True);
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

    [TestCase("invalid-commit", SpecRepository)]
    [TestCase(SpecCommit, "https://untrusted.example/specs")]
    public async Task InvalidRecordedLocationCannotClassifyThePrAsDataPlane(string commit, string repository)
    {
        SetFile(_repository, Head, $"{Package}/tsp-location.yaml", $"directory: specification/example/data-plane/Example\ncommit: {commit}\nrepo: {repository}\n");
        var response = await CheckAsync();
        Assert.That(response.Plane, Is.EqualTo("unknown"));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Projects, Is.Empty);
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task DataPlaneLocationSkipsArm()
    {
        SetFile(_repository, Head, $"{Package}/tsp-location.yaml", $"directory: specification/example/data-plane/Example\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(SpecRepository, SpecCommit, "specification/example/data-plane/Example/tspconfig.yaml", DataConfiguration);
        var response = await CheckAsync();
        Assert.That(response.Plane, Is.EqualTo("data-plane"));
        Assert.That(ArmRequests(), Is.Empty);
        Assert.That(_requests.Where(request => request.Uri.AbsolutePath.Contains("/META-INF", StringComparison.Ordinal)), Is.Empty);
    }

    [Test]
    public async Task DataPlaneClassificationDoesNotRequireVersionMetadata()
    {
        _files.Remove(Key(_repository, Head, $"{Package}/{Metadata}"));
        SetFile(_repository, Head, $"{Package}/tsp-location.yaml", $"directory: specification/example/data-plane/Example\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(SpecRepository, SpecCommit, "specification/example/data-plane/Example/tspconfig.yaml", DataConfiguration);
        var response = await CheckAsync();
        Assert.That(response.Plane, Is.EqualTo("data-plane"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MissingPackageEvidenceCannotMakeAMixedPrDataPlane(bool missingLocation)
    {
        const string dataPackage = "sdk/data/azure-resourcemanager-data";
        AddPackage(dataPackage, "src/main/resources/META-INF/data_metadata.json", Version);
        SetFile(_repository, Head, $"{dataPackage}/tsp-location.yaml", $"directory: specification/example/data-plane/Example\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(SpecRepository, SpecCommit, "specification/example/data-plane/Example/tspconfig.yaml", DataConfiguration);
        _files.Remove(Key(_repository, Head, $"{Package}/{Metadata}"));
        if (missingLocation)
        {
            _files.Remove(Key(_repository, Head, $"{Package}/tsp-location.yaml"));
        }
        var response = await CheckAsync();
        Assert.That(response.Plane, Is.EqualTo("unknown"));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task DataPlaneLabelDoesNotReplaceMissingMetadata()
    {
        _labels.Add("data-plane");
        _files.Remove(Key(_repository, Head, $"{Package}/tsp-location.yaml"));
        var response = await CheckAsync();
        Assert.That(response.Plane, Is.EqualTo("unknown"));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task PinnedConfigurationIsAuthoritativeBeforeLabels(bool dataPlaneLabel)
    {
        const string project = "specification/testcontoso/Contoso.Management";
        if (dataPlaneLabel)
        {
            _labels.Add("data-plane");
        }
        SetFile(_repository, Head, $"{Package}/tsp-location.yaml", $"directory: {project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(SpecRepository, SpecCommit, $"{project}/main.tsp", "@armProviderNamespace\nnamespace Microsoft.Example;");
        SetFile(SpecRepository, SpecCommit, $"{project}/tspconfig.yaml", ManagementConfiguration + "options:\n  \"@azure-tools/typespec-autorest\":\n    emitter-output-dir: \"{project-root}\"\n    output-file: \"example.json\"\n");
        SetFile(SpecRepository, SpecCommit, $"{project}/example.json", _files[Key(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json")]);
        var response = await CheckAsync();
        Assert.That(response.Plane, Is.EqualTo("management-plane"));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
    }

    [TestCase("specification/example/data-plane/Example", ManagementConfiguration, "management-plane", ServiceApiReadinessResponse.Passed)]
    [TestCase(Project, DataConfiguration, "data-plane", ServiceApiReadinessResponse.Inconclusive)]
    [TestCase(Project, "options: {}\n", "unknown", ServiceApiReadinessResponse.Inconclusive)]
    [TestCase(Project, "options: [", "unknown", ServiceApiReadinessResponse.Inconclusive)]
    [TestCase(Project, null, "unknown", ServiceApiReadinessResponse.Inconclusive)]
    public async Task PinnedConfigurationDeterminesPlaneInsteadOfTheDirectoryName(string project, string? configuration, string expectedPlane, string expectedStatus)
    {
        SetFile(_repository, Head, $"{Package}/tsp-location.yaml", $"directory: {project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(SpecRepository, SpecCommit, $"{project}/main.tsp", "@armProviderNamespace\nnamespace Microsoft.Example;");
        SetFile(SpecRepository, SpecCommit, $"{project}/example.json", _files[Key(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json")]);
        if (configuration is null)
        {
            _files.Remove(Key(SpecRepository, SpecCommit, $"{project}/tspconfig.yaml"));
        }
        else
        {
            var outputMapping = expectedPlane == "management-plane"
                ? "options:\n  '@azure-tools/typespec-autorest':\n    emitter-output-dir: '{project-root}'\n    output-file: example.json\n" : "";
            SetFile(SpecRepository, SpecCommit, $"{project}/tspconfig.yaml", configuration + outputMapping);
        }

        var response = await CheckAsync();

        Assert.That(response.Plane, Is.EqualTo(expectedPlane));
        Assert.That(response.ReadinessStatus, Is.EqualTo(expectedStatus));
        Assert.That(ArmRequests().Count(), Is.EqualTo(expectedPlane == "management-plane" ? 1 : 0));
        if (expectedPlane != "management-plane")
        {
            Assert.That(_requests.Where(request => request.Uri.AbsolutePath.Contains("/META-INF", StringComparison.Ordinal)), Is.Empty);
        }
    }

    [Test]
    public async Task RecordedProjectDirectoryCannotEscapeTheSpecificationTree()
    {
        SetFile(_repository, Head, $"{Package}/tsp-location.yaml", $"directory: specification/example/../resource-manager/Example\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Projects, Is.Empty);
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task RecordedProjectDirectoryIsNotRejectedByItsFileLikeSuffix()
    {
        const string project = "specification/example/resource-manager/project.json";
        SetFile(_repository, Head, $"{Package}/tsp-location.yaml", $"directory: {project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(SpecRepository, SpecCommit, $"{project}/main.tsp", "@armProviderNamespace\nnamespace Microsoft.Example;");
        SetFile(SpecRepository, SpecCommit, $"{project}/tspconfig.yaml", ManagementConfiguration + "options:\n  \"@azure-tools/typespec-autorest\":\n    emitter-output-dir: \"{project-root}\"\n    output-file: \"example.json\"\n");
        SetFile(SpecRepository, SpecCommit, $"{project}/example.json", _files[Key(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json")]);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
    }

    [Test]
    public async Task ConflictingPlaneLabelsDoNotOverrideManagementMetadata()
    {
        _labels.AddRange(["data-plane", "resource-manager"]);
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.Plane, Is.EqualTo("management-plane"));
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
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
    [TestCase(HttpStatusCode.TooManyRequests)]
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
        Assert.That(response.ResponseError, Does.Contain(Package).And.Contain($"HTTP {(int)status}"));
        Assert.That(response.ToString(), Does.Contain(Head).And.Contain(_repository).And.Contain(Package).And.Contain($"HTTP {(int)status}"));
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

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task GitHubFailureAfterHeadCapturePreservesCliTraceability(HttpStatusCode status)
    {
        _evidenceStatus = status;
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(response.ResponseError, Does.Contain(Head).And.Contain(_repository).And.Contain($"HTTP {(int)status}"));
        Assert.That(response.ToString(), Does.Contain(Head).And.Contain(_repository).And.Contain($"HTTP {(int)status}"));
        Assert.That(JsonSerializer.Serialize(response), Does.Not.Contain("private transport details"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task MissingHeadRepositoryDoesNotReadSdkFilesOrArm()
    {
        _missingHeadRepository = true;
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(_requests, Has.Count.EqualTo(1));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task TokenResolutionFailureDoesNotExposeTheToken()
    {
        _github.Setup(service => service.GetAuthToken(It.IsAny<CancellationToken>())).Throws(new InvalidOperationException("private token value"));
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(JsonSerializer.Serialize(response), Does.Not.Contain("private token value"));
        Assert.That(_requests, Is.Empty);
    }

    [Test]
    public async Task ReturnsCapturedHeadEvidenceWithoutRefetchingCurrentPr()
    {
        _changeHead = true;
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.HeadSha, Is.EqualTo(Head));
        Assert.That(_pullRequestReads, Is.EqualTo(1));
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

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task SharedNamespaceReusesFailedArmLookup(HttpStatusCode status)
    {
        AddPackage("sdk/second/azure-resourcemanager-second", "src/main/resources/META-INF/second_metadata.json", Version);
        _armStatus = status;
        var response = await CheckAsync();
        Assert.That(ArmRequests().Count(), Is.EqualTo(1));
        Assert.That(response.Projects, Has.Count.EqualTo(2));
        Assert.That(response.Projects.All(project => project.ReadinessStatus == ServiceApiReadinessResponse.Error), Is.True);
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
    }

    [Test]
    public async Task PackageWithOnlyDeletedFilesIsStillCheckedWhenItRemainsAtHead()
    {
        _removedFiles.Add(_changedFiles.Single());
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.Projects.Single().Package.DirectoryPath.ToString(), Is.EqualTo(Package));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeletedFileCannotUseBaseGenerationMetadataAsCurrentEvidence(bool parentPackageExists)
    {
        _removedFiles.Add(_changedFiles.Single());
        if (parentPackageExists)
        {
            AddPackage("sdk/example", Metadata, Version);
        }
        _files.Remove(Key(_repository, Head, $"{Package}/tsp-location.yaml"));
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(response.Detail, Does.Contain("missing SDK generation metadata"));
        Assert.That(ArmRequests(), Is.Empty);
    }

    [Test]
    public async Task DeletedFileInSecondSurvivingPackageCannotBeHiddenByPassingFirstPackage()
    {
        const string second = "sdk/second/azure-resourcemanager-second";
        AddPackage(second, "src/main/resources/META-INF/azure-resourcemanager-second_metadata.json", "2026-03-01");
        _removedFiles.Add($"{second}/Generated.cs");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.NotDetected));
        Assert.That(response.Projects.Count, Is.EqualTo(2));
        Assert.That(response.Projects.Single(project => project.Package.DirectoryPath == second).ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.NotDetected));
    }

    [Test]
    public async Task FullyDeletedPackageIsExcludedOnlyAfterItsHeadDirectoryIsAbsent()
    {
        const string removed = "sdk/removed/azure-resourcemanager-removed";
        SetFile(_repository, BaseCommit, $"{removed}/tsp-location.yaml", $"directory: {Project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        _changedFiles.Add($"{removed}/Generated.cs");
        _removedFiles.Add($"{removed}/Generated.cs");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        Assert.That(response.Projects.Single().Package.DirectoryPath.ToString(), Is.EqualTo(Package));
        Assert.That(_requests.Any(request => Uri.UnescapeDataString(request.Uri.AbsolutePath).EndsWith(removed, StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public async Task RenameOutOfAnotherSurvivingPackageStillChecksThatPackage()
    {
        const string previousPackage = "sdk/previous/azure-resourcemanager-previous";
        AddPackage(previousPackage, "src/main/resources/META-INF/azure-resourcemanager-previous_metadata.json", "2026-03-01");
        _changedFiles.Remove($"{previousPackage}/Generated.cs");
        _renamedFiles[_changedFiles.Single()] = $"{previousPackage}/Generated.cs";
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.NotDetected));
        Assert.That(response.Projects.Count, Is.EqualTo(2));
    }

    [TestCase("config/web")]
    [TestCase("{childType}/{childName}")]
    [TestCase("{childType}")]
    public async Task UnresolvedChildCannotPassUsingItsParent(string child)
    {
        SetSwagger(Version, $"/providers/Microsoft.Example/widgets/{{name}}/{child}", true);
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
    [TestCase("{\"info\":[]}")]
    [TestCase("{\"info\":\"2026-04-01\"}")]
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
        SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml", ManagementConfiguration + "extends: ../tspconfig.yaml\n");
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Inconclusive));
        Assert.That(ArmRequests(), Is.Empty);
        Assert.That(response.ToString(), Does.Contain(Head).And.Contain(_repository).And.Contain(Package)
            .And.Contain(response.Projects.Single().Detail));
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExplicitProviderFolderDoesNotReadSiblingSwagger(bool configuredOutputExists)
    {
        SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml",
            Config("{azure-resource-provider-folder}/{version-status}/{version}/example.json") + "    azure-resource-provider-folder: resource-manager\n");
        var swagger = _files[Key(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json")];
        SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/data-plane/stable/{Version}/example.json", swagger);
        if (configuredOutputExists)
        {
            SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/resource-manager/stable/{Version}/example.json", swagger);
        }
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(configuredOutputExists ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.Inconclusive));
        Assert.That(_requests.Any(request => request.Uri.AbsolutePath.Contains("/data-plane/", StringComparison.Ordinal)), Is.False);
    }

    [TestCase(32)]
    [TestCase(33)]
    public async Task WildcardDiscoveryBoundsConcreteSwaggerSuffix(int count)
    {
        SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml", Config("{service-name}/{version-status}/{version}/example.json"));
        _directories[Key(SpecRepository, SpecCommit, SwaggerDirectory)] = JsonSerializer.SerializeToNode(
            Enumerable.Range(0, count).Select(index => new { name = $"service{index}", type = "dir" }))!.AsArray();
        var swagger = _files[Key(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json")];
        for (var index = 0; index < count; index++)
        {
            SetFile(SpecRepository, SpecCommit, $"{SwaggerDirectory}/service{index}/stable/{Version}/example.json", swagger);
        }
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(count == 32 ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.Error));
        Assert.That(_requests.Count(request => request.Uri.AbsolutePath.EndsWith("/example.json", StringComparison.Ordinal)), Is.LessThanOrEqualTo(32));
    }

    [Test]
    public async Task LargeSwaggerUsesBoundedRawContentOnTheSamePinnedApiHost()
    {
        var path = $"{SwaggerDirectory}/stable/{Version}/example.json";
        var document = JsonNode.Parse(_files[Key(SpecRepository, SpecCommit, path)])!.AsObject();
        document["description"] = new string('x', 2 * 1024 * 1024);
        SetFile(SpecRepository, SpecCommit, path, document.ToJsonString());
        _rawFiles.Add(Key(SpecRepository, SpecCommit, path));
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Passed));
        var reads = _requests.Where(request => Uri.UnescapeDataString(request.Uri.AbsolutePath).EndsWith(path, StringComparison.Ordinal)).ToArray();
        Assert.That(reads, Has.Length.EqualTo(2));
        Assert.That(reads.All(request => request.Uri.Host == "api.github.com" && request.Token == "github-test-token" &&
            QueryHelpers.ParseQuery(request.Uri.Query)["ref"] == SpecCommit), Is.True);
        Assert.That(_requests.Any(request => request.Uri.Host == "untrusted.example"), Is.False);
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.Redirect)]
    public async Task RawContentAccessFailureOrRedirectIsAnError(HttpStatusCode status)
    {
        _rawFiles.Add(Key(SpecRepository, SpecCommit, $"{SwaggerDirectory}/stable/{Version}/example.json"));
        _rawStatus = status;
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(ArmRequests(), Is.Empty);
        Assert.That(JsonSerializer.Serialize(response), Does.Not.Contain("private transport details"));
    }

    [Test]
    public async Task OversizedRawSwaggerIsRejectedBeforeDownload()
    {
        var path = $"{SwaggerDirectory}/stable/{Version}/example.json";
        SetFile(SpecRepository, SpecCommit, path, new string('x', 4 * 1024 * 1024 + 1));
        _rawFiles.Add(Key(SpecRepository, SpecCommit, path));
        var response = await CheckAsync();
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(_requests.Count(request => Uri.UnescapeDataString(request.Uri.AbsolutePath).EndsWith(path, StringComparison.Ordinal)), Is.EqualTo(1));
        Assert.That(ArmRequests(), Is.Empty);
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
        var response = await _service.CheckAsync("helen229", "azure-sdk-for-java", 1, cancellation.Token);
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(response.Detail, Does.Contain("canceled"));
        _github.Verify(service => service.GetAuthToken(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CancellationDuringTokenDiscoveryReturnsErrorWithoutStartingEvidenceReads()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var authentication = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _github.Setup(service => service.GetAuthToken(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(token =>
            {
                Assert.That(token.CanBeCanceled, Is.True);
                token.Register(() => authentication.TrySetCanceled(token));
                started.TrySetResult(true);
                return authentication.Task.GetAwaiter().GetResult();
            });
        var checking = Task.Run(() => _service.CheckAsync("helen229", "azure-sdk-for-java", 1, cancellation.Token));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var response = await checking.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(response.ReadinessStatus, Is.EqualTo(ServiceApiReadinessResponse.Error));
        Assert.That(response.Detail, Does.Contain("canceled"));
        Assert.That(_requests, Is.Empty);
    }

    private Task<ServiceApiReadinessResponse> CheckAsync() => _service.CheckAsync("helen229", _repository.Split('/')[1], 1, CancellationToken.None);
    private IEnumerable<(HttpMethod Method, Uri Uri, string? Token)> ArmRequests() => _requests.Where(request => request.Uri.Host == "management.azure.com");
    private static string Key(string repository, string commit, string path) => $"{repository}|{commit}|{path}";
    private void SetFile(string repository, string commit, string path, string content) => _files[Key(repository, commit, path)] = content;

    private void AddPackage(string package, string metadataFile, string version)
    {
        _changedFiles.Add($"{package}/Generated.cs");
        SetFile(_repository, Head, $"{package}/tsp-location.yaml", $"directory: {Project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(_repository, BaseCommit, $"{package}/tsp-location.yaml", $"directory: {Project}\ncommit: {SpecCommit}\nrepo: {SpecRepository}\n");
        SetFile(_repository, Head, $"{package}/{metadataFile}", JsonSerializer.Serialize(new { apiVersions = new Dictionary<string, string> { ["Microsoft.Example"] = version } }));
        SetFile(SpecRepository, SpecCommit, $"{Project}/main.tsp", "@armProviderNamespace\n@service\nnamespace Microsoft.Example;");
        SetFile(SpecRepository, SpecCommit, $"{Project}/tspconfig.yaml", Config("{version-status}/{version}/example.json"));
        SetSwagger(version, "/providers/Microsoft.Example/widgets/{name}", true);
    }

    private static string Config(string output) => ManagementConfiguration + $"options:\n  \"@azure-tools/typespec-autorest\":\n    emitter-output-dir: \"{{project-root}}/../swagger\"\n    output-file: \"{output}\"\n";

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
            var providerNamespace = Uri.UnescapeDataString(uri.Segments[^1]);
            return _armStatus == HttpStatusCode.OK ? Json(new
            {
                @namespace = providerNamespace,
                resourceTypes = new[] { new { resourceType = "widgets", apiVersions = _providerVersions.GetValueOrDefault(providerNamespace, _advertisedVersions), locations = new[] { "East US" } } }
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
                head = new { sha = _changeHead && _pullRequestReads > 1 ? new string('c', 40) : Head, repo = _missingHeadRepository ? null : new { full_name = _repository } },
                @base = new { sha = BaseCommit, repo = new { full_name = _repository } },
                labels = _labels.Select(label => new { name = label }).ToArray()
            });
        }
        if (_evidenceStatus != HttpStatusCode.OK)
        {
            return Json(new { error = "private transport details" }, _evidenceStatus);
        }
        if (path == $"/repos/{_repository}/pulls/1/files")
        {
            var files = _changedFiles.Select(filename => new
            {
                filename,
                status = _removedFiles.Contains(filename) ? "removed" : _renamedFiles.ContainsKey(filename) ? "renamed" : "modified",
                previous_filename = _renamedFiles.GetValueOrDefault(filename)
            }).ToArray();
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
                if (_rawFiles.Contains(key))
                {
                    if (request.Headers.Accept.Any(header => header.MediaType == "application/vnd.github.raw+json"))
                    {
                        return _rawStatus == HttpStatusCode.OK ? new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(file, Encoding.UTF8, "application/json")
                        } : Json(new { error = "private transport details" }, _rawStatus);
                    }
                    return Json(new
                    {
                        type = "file",
                        encoding = "none",
                        content = "",
                        size = Encoding.UTF8.GetByteCount(file),
                        download_url = "https://untrusted.example/file"
                    });
                }
                return Json(new { type = "file", encoding = "base64", content = Convert.ToBase64String(Encoding.UTF8.GetBytes(file)) });
            }
            if (_directories.TryGetValue(key, out var directory))
            {
                return Json(directory);
            }
            var prefix = Key(repository, commit, filePath + "/");
            var children = _files.Keys.Where(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal))
                .Select(candidate => candidate[prefix.Length..].Split('/')[0]).Distinct(StringComparer.Ordinal)
                .Select(name => new
                {
                    name,
                    type = _files.ContainsKey(Key(repository, commit, filePath + "/" + name)) ? "file" : "dir"
                }).ToArray();
            if (children.Length > 0)
            {
                return Json(children);
            }
        }
        return Json(new { message = "Not found" }, HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };
}
