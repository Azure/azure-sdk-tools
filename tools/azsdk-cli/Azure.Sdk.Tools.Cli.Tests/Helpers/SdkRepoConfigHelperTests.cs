// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class SdkRepoConfigHelperTests
{
    #region Test Constants

    private const string TestRepoName = "azure-sdk-for-net";
    private const string TestConfigPath = "eng/swagger_to_sdk_config.json";
    private const string BuildCommandJsonPath = "packageOptions/buildScript/command";
    private const string BuildScriptPathJsonPath = "packageOptions/buildScript/path";

    #endregion

    private SdkRepoConfigHelper _helper;
    private TestLogger<SdkRepoConfigHelper> _logger;
    private string _tempDirectory;
    private string _configFilePath;
    private string _repoConfigFilePath;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<SdkRepoConfigHelper>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SdkRepoConfigHelperTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        // Create a test SdkRepoConfig.json file in the expected location relative to temp directory
        var configDir = Path.Combine(_tempDirectory, "Configuration");
        Directory.CreateDirectory(configDir);
        _configFilePath = Path.Combine(configDir, "SdkRepoConfig.json");

        var sdkRepoConfig = new Dictionary<string, SdkRepoConfiguration>
        {
            { TestRepoName, new SdkRepoConfiguration { ConfigFilePath = TestConfigPath } }
        };
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(sdkRepoConfig, new JsonSerializerOptions { WriteIndented = true }));

        // Create the helper instance using the test config
        _helper = new TestSdkRepoConfigHelper(_logger, _configFilePath);

        // Create test repository config file
        var repoConfigDir = Path.Combine(_tempDirectory, "eng");
        Directory.CreateDirectory(repoConfigDir);
        _repoConfigFilePath = Path.Combine(_tempDirectory, TestConfigPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region Configuration Loading Tests

    [Test]
    public async Task GetConfigFilePathForRepoAsync_ValidRepo_ReturnsCorrectPath()
    {
        // Act
        var result = await _helper.GetConfigFilePathForRepoAsync(TestRepoName);

        // Assert
        Assert.That(result, Is.EqualTo(TestConfigPath));
    }

    [Test]
    public void GetConfigFilePathForRepoAsync_InvalidRepo_ThrowsException()
    {
        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => _helper.GetConfigFilePathForRepoAsync("nonexistent-repo"));
        Assert.That(ex.Message, Does.Contain("No configuration found for repository"));
    }

    [Test]
    public void GetRepoConfigurationAsync_EmptyRepoName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _helper.GetRepoConfigurationAsync(""));
        Assert.ThrowsAsync<ArgumentException>(() => _helper.GetRepoConfigurationAsync(string.Empty));
    }

    #endregion

    #region Build Configuration Tests

    [Test]
    public async Task GetBuildConfigurationAsync_CommandExists_ReturnsCommand()
    {
        // Arrange
        var configContent = new
        {
            packageOptions = new
            {
                buildScript = new
                {
                    command = "dotnet build {packagePath}"
                }
            }
        };
        File.WriteAllText(_repoConfigFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetBuildConfigurationAsync(_tempDirectory, TestRepoName);

        // Assert
        Assert.That(result.type, Is.EqualTo(BuildConfigType.Command));
        Assert.That(result.value, Is.EqualTo("dotnet build {packagePath}"));
    }

    [Test]
    public async Task GetBuildConfigurationAsync_OnlyPathExists_ReturnsScriptPath()
    {
        // Arrange
        var configContent = new
        {
            packageOptions = new
            {
                buildScript = new
                {
                    path = "eng/scripts/build.sh"
                }
            }
        };
        File.WriteAllText(_repoConfigFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetBuildConfigurationAsync(_tempDirectory, TestRepoName);

        // Assert
        Assert.That(result.type, Is.EqualTo(BuildConfigType.ScriptPath));
        Assert.That(result.value, Is.EqualTo("eng/scripts/build.sh"));
    }

    [Test]
    public async Task GetBuildConfigurationAsync_BothExist_PrefersCommand()
    {
        // Arrange
        var configContent = new
        {
            packageOptions = new
            {
                buildScript = new
                {
                    command = "dotnet build {packagePath}",
                    path = "eng/scripts/build.sh"
                }
            }
        };
        File.WriteAllText(_repoConfigFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetBuildConfigurationAsync(_tempDirectory, TestRepoName);

        // Assert
        Assert.That(result.type, Is.EqualTo(BuildConfigType.Command));
        Assert.That(result.value, Is.EqualTo("dotnet build {packagePath}"));
    }

    [Test]
    public void GetBuildConfigurationAsync_NeitherExists_ThrowsException()
    {
        // Arrange
        var configContent = new
        {
            packageOptions = new
            {
                other = "value"
            }
        };
        File.WriteAllText(_repoConfigFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => _helper.GetBuildConfigurationAsync(_tempDirectory, TestRepoName));
        Assert.That(ex.Message, Does.Contain("Neither 'packageOptions/buildScript/command' nor 'packageOptions/buildScript/path' found"));
    }

    [Test]
    public void GetBuildConfigurationAsync_ConfigFileNotFound_ThrowsException()
    {
        // Act & Assert
        var ex = Assert.ThrowsAsync<FileNotFoundException>(() => _helper.GetBuildConfigurationAsync(_tempDirectory, TestRepoName));
        Assert.That(ex.Message, Does.Contain("Configuration file not found"));
    }

    #endregion

    #region Generic Config Value Tests

    [Test]
    public async Task GetConfigValueFromRepoAsync_ValidPath_ReturnsValue()
    {
        // Arrange
        var configContent = new
        {
            packageOptions = new
            {
                buildScript = new
                {
                    command = "dotnet build {packagePath}"
                }
            }
        };
        File.WriteAllText(_repoConfigFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetConfigValueFromRepoAsync<string>(_tempDirectory, TestRepoName, BuildCommandJsonPath);

        // Assert
        Assert.That(result, Is.EqualTo("dotnet build {packagePath}"));
    }

    [Test]
    public async Task GetConfigValueFromRepoAsync_ComplexObject_ReturnsObject()
    {
        // Arrange
        var buildOptions = new { configuration = "Release", verbosity = "minimal" };
        var configContent = new
        {
            packageOptions = new
            {
                buildOptions = buildOptions
            }
        };
        File.WriteAllText(_repoConfigFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetConfigValueFromRepoAsync<Dictionary<string, string>>(_tempDirectory, TestRepoName, "packageOptions/buildOptions");

        // Assert
        Assert.That(result["configuration"], Is.EqualTo("Release"));
        Assert.That(result["verbosity"], Is.EqualTo("minimal"));
    }

    [Test]
    public void GetConfigValueFromRepoAsync_InvalidPath_ThrowsException()
    {
        // Arrange
        var configContent = new { other = "value" };
        File.WriteAllText(_repoConfigFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => _helper.GetConfigValueFromRepoAsync<string>(_tempDirectory, TestRepoName, "nonexistent/path"));
        Assert.That(ex.Message, Does.Contain("Property not found at JSON path"));
    }

    #endregion

    #region Variable Substitution Tests

    [Test]
    public void SubstituteCommandVariables_SingleVariable_SubstitutesCorrectly()
    {
        // Arrange
        var command = "dotnet build {packagePath}";
        var variables = new Dictionary<string, string>
        {
            { "packagePath", "/path/to/package" }
        };

        // Act
        var result = _helper.SubstituteCommandVariables(command, variables);

        // Assert
        Assert.That(result, Is.EqualTo("dotnet build /path/to/package"));
    }

    [Test]
    public void SubstituteCommandVariables_MultipleVariables_SubstitutesAll()
    {
        // Arrange
        var command = "dotnet build {projectPath} --configuration {config} --output {outputDir}";
        var variables = new Dictionary<string, string>
        {
            { "projectPath", "/path/to/project" },
            { "config", "Release" },
            { "outputDir", "/path/to/output" }
        };

        // Act
        var result = _helper.SubstituteCommandVariables(command, variables);

        // Assert
        Assert.That(result, Is.EqualTo("dotnet build /path/to/project --configuration Release --output /path/to/output"));
    }

    [Test]
    public void SubstituteCommandVariables_CaseInsensitive_SubstitutesCorrectly()
    {
        // Arrange
        var command = "dotnet build {PackagePath}";
        var variables = new Dictionary<string, string>
        {
            { "packagepath", "/path/to/package" }
        };

        // Act
        var result = _helper.SubstituteCommandVariables(command, variables);

        // Assert
        Assert.That(result, Is.EqualTo("dotnet build /path/to/package"));
    }

    [Test]
    public void SubstituteCommandVariables_NoVariables_ReturnsOriginal()
    {
        // Arrange
        var command = "dotnet build";
        var variables = new Dictionary<string, string>();

        // Act
        var result = _helper.SubstituteCommandVariables(command, variables);

        // Assert
        Assert.That(result, Is.EqualTo("dotnet build"));
    }

    [Test]
    public void SubstituteCommandVariables_EmptyCommand_ReturnsEmpty()
    {
        // Arrange
        var variables = new Dictionary<string, string> { { "test", "value" } };

        // Act
        var result = _helper.SubstituteCommandVariables("", variables);

        // Assert
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void SubstituteCommandVariables_NullVariables_ReturnsOriginal()
    {
        // Arrange
        var command = "dotnet build {packagePath}";

        // Act
        var result = _helper.SubstituteCommandVariables(command, new Dictionary<string, string>());

        // Assert
        Assert.That(result, Is.EqualTo(command));
    }

    #endregion

    #region Command Parsing Tests

    [Test]
    public void ParseCommand_SimpleCommand_ParsesCorrectly()
    {
        // Arrange
        var command = "dotnet build --configuration Release";

        // Act
        var result = _helper.ParseCommand(command);

        // Assert
        Assert.That(result, Is.EqualTo(new[] { "dotnet", "build", "--configuration", "Release" }));
    }

    [Test]
    public void ParseCommand_QuotedArguments_PreservesSpaces()
    {
        // Arrange
        var command = "dotnet build \"C:\\Path With Spaces\\Project.csproj\" --output \"C:\\Output Dir\"";

        // Act
        var result = _helper.ParseCommand(command);

        // Assert
        Assert.That(result, Is.EqualTo(new[] { "dotnet", "build", "C:\\Path With Spaces\\Project.csproj", "--output", "C:\\Output Dir" }));
    }

    [Test]
    public void ParseCommand_MultipleSpaces_IgnoresExtraSpaces()
    {
        // Arrange
        var command = "dotnet    build    --configuration    Release";

        // Act
        var result = _helper.ParseCommand(command);

        // Assert
        Assert.That(result, Is.EqualTo(new[] { "dotnet", "build", "--configuration", "Release" }));
    }

    [Test]
    public void ParseCommand_EmptyCommand_ReturnsEmpty()
    {
        // Act
        var result = _helper.ParseCommand("");

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ParseCommand_WhitespaceOnly_ReturnsEmpty()
    {
        // Act
        var result = _helper.ParseCommand("   ");

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ParseCommand_SingleWord_ReturnsSingleElement()
    {
        // Act
        var result = _helper.ParseCommand("dotnet");

        // Assert
        Assert.That(result, Is.EqualTo(new[] { "dotnet" }));
    }

    #endregion
}

/// <summary>
/// Test version of SdkRepoConfigHelper that allows injecting a custom config file path
/// </summary>
public class TestSdkRepoConfigHelper : SdkRepoConfigHelper
{
    private readonly string _configFilePath;

    public TestSdkRepoConfigHelper(ILogger<SdkRepoConfigHelper> logger, string configFilePath) : base(logger)
    {
        _configFilePath = configFilePath;
        
        // Use reflection to set the private configFilePath field
        var field = typeof(SdkRepoConfigHelper).GetField("configFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(this, _configFilePath);
    }
}
