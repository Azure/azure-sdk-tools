// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class TspClientHelperTests
{
    private const string AzureSdkNpmRegistry = "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-js/npm/registry/";

    private Mock<INpmHelper> _npmHelper = null!;
    private Mock<ITypeSpecHelper> _typeSpecHelper = null!;
    private Mock<IGitHelper> _gitHelper = null!;
    private TspClientHelper _helper = null!;
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);

        _npmHelper = new Mock<INpmHelper>();
        _typeSpecHelper = new Mock<ITypeSpecHelper>();
        _gitHelper = new Mock<IGitHelper>();

        _gitHelper
            .Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tempDirectory);
        _typeSpecHelper
            .Setup(x => x.IsRepoPathForSpecRepoAsync(_tempDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _helper = new TspClientHelper(
            _npmHelper.Object,
            _typeSpecHelper.Object,
            _gitHelper.Object,
            NullLogger<TspClientHelper>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task UpdateGenerationAsync_SetsAzureSdkNpmRegistry()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "tsp-location.yaml"), "directory: specification/test");
        NpmOptions? capturedOptions = null;
        _npmHelper
            .Setup(x => x.Run(It.IsAny<NpmOptions>(), It.IsAny<CancellationToken>()))
            .Callback<NpmOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var result = await _helper.UpdateGenerationAsync(_tempDirectory);

        Assert.That(result.IsSuccessful, Is.True);
        AssertRegistry(capturedOptions);
    }

    [Test]
    public async Task InitializeGenerationAsync_SetsAzureSdkNpmRegistry()
    {
        NpmOptions? capturedOptions = null;
        _npmHelper
            .Setup(x => x.Run(It.IsAny<NpmOptions>(), It.IsAny<CancellationToken>()))
            .Callback<NpmOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var result = await _helper.InitializeGenerationAsync(_tempDirectory, "tspconfig.yaml");

        Assert.That(result.IsSuccessful, Is.True);
        AssertRegistry(capturedOptions);
    }

    private static void AssertRegistry(NpmOptions? options)
    {
        Assert.That(options, Is.Not.Null);
        Assert.That(options!.EnvironmentVariables, Is.Not.Null);
        Assert.That(options.EnvironmentVariables!["npm_config_registry"], Is.EqualTo(AzureSdkNpmRegistry));
    }
}
