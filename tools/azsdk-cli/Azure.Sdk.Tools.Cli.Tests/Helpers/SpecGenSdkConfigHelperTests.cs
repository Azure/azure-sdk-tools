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
public class SpecGenSdkConfigHelperTests
{
    #region Test Constants

    private const string TestRepoName = "azure-sdk-for-net";
    private const string TestConfigPath = "eng/swagger_to_sdk_config.json";
    private const string BuildCommandJsonPath = "packageOptions/buildScript/command";
    private const string BuildScriptPathJsonPath = "packageOptions/buildScript/path";

    #endregion

    private SpecGenSdkConfigHelper _helper;
    private TestLogger<SpecGenSdkConfigHelper> _logger;
    private TempDirectory _tempDirectory;
    private string _configFilePath;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<SpecGenSdkConfigHelper>();
        _tempDirectory = TempDirectory.Create("SpecGenSdkConfigHelperTests");

        // Create the swagger_to_sdk_config.json file at the expected location
        var engDir = Path.Combine(_tempDirectory.DirectoryPath, "eng");
        Directory.CreateDirectory(engDir);
        _configFilePath = Path.Combine(engDir, "swagger_to_sdk_config.json");
        var mockProcessHelper = new Mock<IProcessHelper>();
        _helper = new SpecGenSdkConfigHelper(_logger, mockProcessHelper.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _tempDirectory.Dispose();
    }

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
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetConfigurationAsync(_tempDirectory.DirectoryPath, SpecGenSdkConfigType.Build);

        // Assert
        Assert.That(result.type, Is.EqualTo(SpecGenSdkConfigContentType.Command));
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
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetConfigurationAsync(_tempDirectory.DirectoryPath, SpecGenSdkConfigType.Build);

        // Assert
        Assert.That(result.type, Is.EqualTo(SpecGenSdkConfigContentType.ScriptPath));
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
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetConfigurationAsync(_tempDirectory.DirectoryPath, SpecGenSdkConfigType.Build);

        // Assert
        Assert.That(result.type, Is.EqualTo(SpecGenSdkConfigContentType.Command));
        Assert.That(result.value, Is.EqualTo("dotnet build {packagePath}"));
    }

    [Test]
    public async Task GetBuildConfigurationAsync_NeitherExists_ReturnsUnknown()
    {
        // Arrange
        var configContent = new
        {
            packageOptions = new
            {
                other = "value"
            }
        };
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetConfigurationAsync(_tempDirectory.DirectoryPath, SpecGenSdkConfigType.Build);

        // Assert
        Assert.That(result.type, Is.EqualTo(SpecGenSdkConfigContentType.Unknown));
        Assert.That(result.value, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetBuildConfigurationAsync_ConfigFileNotFound_ThrowsException()
    {
        // Act & Assert
        var ex = Assert.ThrowsAsync<FileNotFoundException>(() => _helper.GetConfigurationAsync(_tempDirectory.DirectoryPath, SpecGenSdkConfigType.Build));
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
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetConfigValueFromRepoAsync<string>(_tempDirectory.DirectoryPath, BuildCommandJsonPath);

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
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act
        var result = await _helper.GetConfigValueFromRepoAsync<Dictionary<string, string>>(_tempDirectory.DirectoryPath, "packageOptions/buildOptions");

        // Assert
        Assert.That(result["configuration"], Is.EqualTo("Release"));
        Assert.That(result["verbosity"], Is.EqualTo("minimal"));
    }

    [Test]
    public void GetConfigValueFromRepoAsync_InvalidPath_ThrowsException()
    {
        // Arrange
        var configContent = new { other = "value" };
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(configContent, new JsonSerializerOptions { WriteIndented = true }));

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => _helper.GetConfigValueFromRepoAsync<string>(_tempDirectory.DirectoryPath, "nonexistent/path"));
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
