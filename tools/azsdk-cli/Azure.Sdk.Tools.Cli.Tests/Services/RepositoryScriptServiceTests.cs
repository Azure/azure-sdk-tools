using System.Collections.Specialized;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
internal class RepositoryScriptServiceTests
{
    private const string DefaultCommandName = "RepositoryCommand";
    private const string DefaultScriptRelativePath = "eng/scripts/RepositoryCommand.ps1";

    private TempDirectory? _tempRepo;
    private string _repoRoot = null!;
    private RepositoryScriptService _service = null!;
    private Mock<IGitHelper> _gitHelperMock = null!;
    private Mock<IPowershellHelper> _powershellHelperMock = null!;
    private TestLogger<RepositoryScriptService> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRepo = TempDirectory.Create("azsdk-repository-script");
        _repoRoot = _tempRepo.DirectoryPath;

        _logger = new TestLogger<RepositoryScriptService>();
        _gitHelperMock = new Mock<IGitHelper>();
        _powershellHelperMock = new Mock<IPowershellHelper>();

        _service = new RepositoryScriptService(
            _logger,
            _gitHelperMock.Object,
            _powershellHelperMock.Object
        );
    }

    [TearDown]
    public void TearDown()
    {
        _tempRepo?.Dispose();
        _tempRepo = null;
    }

    [Test]
    public async Task GetCommand_ReturnsNull_WhenRepoHasNoOverrides()
    {
        string? command = await _service.GetCommand(DefaultCommandName, _repoRoot, CancellationToken.None);

        Assert.That(command, Is.Null);
    }

    [Test]
    public async Task GetCommand_ReturnsConfiguredScript_ForAnyMatchingTag()
    {
        WriteOverrideConfig(["RepositoryCommand", "azsdk_repository_command"], DefaultScriptRelativePath);
        CreateScript(DefaultScriptRelativePath);

        string? primary = await _service.GetCommand(DefaultCommandName, _repoRoot, CancellationToken.None);
        string? alias = await _service.GetCommand("azsdk_repository_command", _repoRoot, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(primary, Is.EqualTo(DefaultScriptRelativePath));
            Assert.That(alias, Is.EqualTo(DefaultScriptRelativePath));
        });
    }

    [Test]
    public void GetCommand_Throws_WhenScriptDoesNotExist()
    {
        WriteOverrideConfig(["RepositoryCommand"], DefaultScriptRelativePath);

        Assert.That(
            async () => await _service.GetCommand(DefaultCommandName, _repoRoot, CancellationToken.None),
            Throws.Exception.With.Message.Contain("does not exist"));
    }

    [Test]
    public async Task HasImplementation_ReturnsTrue_ForAllConfiguredTags()
    {
        WriteOverrideConfig(["RepositoryCommand", "azsdk_repository_command"], DefaultScriptRelativePath);
        CreateScript(DefaultScriptRelativePath);

        bool primary = await _service.HasImplementation(DefaultCommandName, _repoRoot, CancellationToken.None);
        bool alias = await _service.HasImplementation("azsdk_repository_command", _repoRoot, CancellationToken.None);
        bool missing = await _service.HasImplementation("NotConfigured", _repoRoot, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(primary, Is.True);
            Assert.That(alias, Is.True);
            Assert.That(missing, Is.False);
        });
    }

    [Test]
    public async Task TryInvoke_ReturnsFalse_WhenOverrideMissing()
    {
        var packagePath = Path.Combine(_repoRoot, "sdk", "service", "package");
        _gitHelperMock.Setup(x => x.DiscoverRepoRoot(packagePath)).Returns(_repoRoot);

        var args = new OrderedDictionary
        {
            ["ServiceDirectory"] = "sdk/service"
        };

        var (invoked, result) = await _service.TryInvoke("UnknownCommand", packagePath, args, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(invoked, Is.False);
            Assert.That(result, Is.Not.Null);
            // We should get a default empty result back to be ignored
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Output, Is.Empty);
        });

        _powershellHelperMock.Verify(x => x.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task TryInvoke_CallsIntoPowershell_WithSerializedParameters()
    {
        WriteOverrideConfig(["RepositoryCommand"], DefaultScriptRelativePath);
        var scriptFullPath = CreateScript(DefaultScriptRelativePath);

        var packagePath = Path.Combine(_repoRoot, "sdk", "service", "package");
        _gitHelperMock.Setup(x => x.DiscoverRepoRoot(packagePath)).Returns(_repoRoot);

        var processResult = new ProcessResult { ExitCode = 5 };
        processResult.AppendStdout("script ran");

        PowershellOptions? capturedOptions = null;
        _powershellHelperMock
            .Setup(x => x.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .Callback<PowershellOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        var args = new OrderedDictionary
        {
            ["ServiceDirectory"] = "sdk/service",
            ["TestParam"] = true
        };

        var (invoked, result) = await _service.TryInvoke(DefaultCommandName, packagePath, args, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(invoked, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(processResult.ExitCode));
            Assert.That(capturedOptions, Is.Not.Null);
        });

        Assert.That(capturedOptions!.Command, Is.EqualTo("pwsh"));
        Assert.That(capturedOptions.WorkingDirectory, Is.EqualTo(_repoRoot));
        Assert.That(capturedOptions.Args, Has.Count.EqualTo(2));

        var commandArgument = capturedOptions.Args[1];
        Assert.Multiple(() =>
        {
            Assert.That(commandArgument, Does.Contain("\"ServiceDirectory\":\"sdk/service\""));
            Assert.That(commandArgument, Does.Contain("\"TestParam\":true"));
            Assert.That(commandArgument, Does.Contain(scriptFullPath));
            Assert.That(commandArgument, Does.Contain("ConvertFrom-Json -AsHashtable"));
            Assert.That(commandArgument.TrimEnd(), Does.EndWith("@params"));
        });

        _gitHelperMock.Verify(x => x.DiscoverRepoRoot(packagePath), Times.Once);
        _powershellHelperMock.Verify(x => x.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task TryInvoke_EscapesSingleQuotesAndSupportsHyphenatedKeys()
    {
        WriteOverrideConfig(["RepositoryCommand"], DefaultScriptRelativePath);
        CreateScript(DefaultScriptRelativePath);

        var packagePath = Path.Combine(_repoRoot, "sdk", "service", "package");
        _gitHelperMock.Setup(x => x.DiscoverRepoRoot(packagePath)).Returns(_repoRoot);

        ProcessResult processResult = new();
        PowershellOptions? capturedOptions = null;
        _powershellHelperMock
            .Setup(x => x.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .Callback<PowershellOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        var args = new OrderedDictionary
        {
            ["repo-path"] = "sdk/service",
            ["displayName"] = "O'Neil"
        };

        var (invoked, result) = await _service.TryInvoke(DefaultCommandName, packagePath, args, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(invoked, Is.True);
            Assert.That(result, Is.SameAs(processResult));
            Assert.That(capturedOptions, Is.Not.Null);
        });

        var commandArgument = capturedOptions!.Args[1];
        Assert.Multiple(() =>
        {
            Assert.That(commandArgument, Does.Contain("$params = @'"));
            Assert.That(commandArgument, Does.Contain("'@ | ConvertFrom-Json -AsHashtable;"));
            Assert.That(commandArgument, Does.Contain("\"repo-path\":\"sdk/service\""));
        });

        var jsonStartToken = "@'\n";
        var jsonEndToken = "\n'@";
        var jsonStartIndex = commandArgument.IndexOf(jsonStartToken, StringComparison.Ordinal) + jsonStartToken.Length;
        var jsonEndIndex = commandArgument.IndexOf(jsonEndToken, StringComparison.Ordinal);
        var jsonPayload = commandArgument.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex);

        using var document = JsonDocument.Parse(jsonPayload);
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("repo-path").GetString(), Is.EqualTo("sdk/service"));
            Assert.That(document.RootElement.GetProperty("displayName").GetString(), Is.EqualTo("O'Neil"));
        });
    }

    [Test]
    public async Task TryInvoke_DoesNotChangeWorkingDirectory_WhenNotInvokedFromRepoRoot()
    {
        WriteOverrideConfig(["RepositoryCommand"], DefaultScriptRelativePath);
        CreateScript(DefaultScriptRelativePath);

        var packagePath = Path.Combine(_repoRoot, "sdk", "service", "package");
        _gitHelperMock.Setup(x => x.DiscoverRepoRoot(packagePath)).Returns(_repoRoot);

        ProcessResult processResult = new();
        PowershellOptions? capturedOptions = null;
        _powershellHelperMock
            .Setup(x => x.Run(It.IsAny<PowershellOptions>(), It.IsAny<CancellationToken>()))
            .Callback<PowershellOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(processResult);

        var args = new OrderedDictionary();

        var currentDirectory = Environment.CurrentDirectory;

        var (invoked, result) = await _service.TryInvoke(
            DefaultCommandName,
            packagePath,
            args,
            invokeFromRepoRoot: false,
            ct: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(invoked, Is.True);
            Assert.That(result, Is.SameAs(processResult));
            Assert.That(capturedOptions, Is.Not.Null);
        });

        Assert.Multiple(() =>
        {
            Assert.That(capturedOptions?.WorkingDirectory, Is.EqualTo(currentDirectory));
            Assert.That(capturedOptions?.Args[1], Does.Contain(DefaultScriptRelativePath));
            Assert.That(capturedOptions?.Args[1], Does.Not.Contain(_repoRoot));
        });
    }

    private void WriteOverrideConfig(IEnumerable<string> tags, string command)
    {
        var configPath = Path.Combine(_repoRoot, _service.ScriptConfig);
        var configDirectory = Path.GetDirectoryName(configPath)!;
        Directory.CreateDirectory(configDirectory);

        var contract = new[]
        {
            new
            {
                tags,
                command
            }
        };

        var json = JsonSerializer.Serialize(contract, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
    }

    private string CreateScript(string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var scriptPath = Path.Combine(_repoRoot, normalizedRelativePath);
        var directory = Path.GetDirectoryName(scriptPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(scriptPath, "# mock script");

        return scriptPath;
    }
}
