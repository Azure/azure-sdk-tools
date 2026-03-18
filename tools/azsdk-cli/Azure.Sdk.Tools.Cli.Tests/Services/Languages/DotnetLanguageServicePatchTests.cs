// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

[TestFixture]
public class DotnetLanguageServicePatchTests
{
    private Mock<IProcessHelper> _processHelper = null!;
    private Mock<IPowershellHelper> _powershellHelper = null!;
    private Mock<ICopilotAgentRunner> _copilotAgentRunner = null!;
    private Mock<IGitHelper> _gitHelper = null!;
    private DotnetLanguageService _service = null!;
    private TempDirectory _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _processHelper = new Mock<IProcessHelper>();
        _powershellHelper = new Mock<IPowershellHelper>();
        _copilotAgentRunner = new Mock<ICopilotAgentRunner>();
        _gitHelper = new Mock<IGitHelper>();
        _tempDir = TempDirectory.Create("dotnet-patch-tests");

        _service = new DotnetLanguageService(
            _processHelper.Object,
            _powershellHelper.Object,
            _copilotAgentRunner.Object,
            _gitHelper.Object,
            new TestLogger<DotnetLanguageService>(),
            Mock.Of<ICommonValidationHelpers>(),
            Mock.Of<IPackageInfoHelper>(),
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    [TearDown]
    public void TearDown() => _tempDir.Dispose();

    [Test]
    public async Task ApplyPatchesAsync_CustomizationRootDoesNotExist_ReturnsEmptyList()
    {
        var nonExistentRoot = Path.Combine(_tempDir.DirectoryPath, "nonexistent");

        var result = await _service.ApplyPatchesAsync(
            nonExistentRoot,
            _tempDir.DirectoryPath,
            "error CS0117: some error",
            CancellationToken.None);

        Assert.That(result, Is.Empty);
        _copilotAgentRunner.Verify(
            r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task ApplyPatchesAsync_NoCsFiles_ReturnsEmptyList()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);

        var result = await _service.ApplyPatchesAsync(
            customizationRoot,
            _tempDir.DirectoryPath,
            "error CS0117: some error",
            CancellationToken.None);

        Assert.That(result, Is.Empty);
        _copilotAgentRunner.Verify(
            r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task ApplyPatchesAsync_WithCsFiles_CallsAgentRunner()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);
        await File.WriteAllTextAsync(
            Path.Combine(customizationRoot, "WidgetClientExtensions.cs"),
            "public partial class WidgetClient { }");

        _copilotAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult("done"));

        var result = await _service.ApplyPatchesAsync(
            customizationRoot,
            _tempDir.DirectoryPath,
            "error CS0117: 'WidgetClient' does not contain a definition for 'GetWidgetAsync'",
            CancellationToken.None);

        _copilotAgentRunner.Verify(
            r => r.RunAsync(
                It.Is<CopilotAgent<string>>(a =>
                    a.MaxIterations == 10 &&
                    a.Instructions.Contains("WidgetClient") &&
                    a.Instructions.Contains("CS0117")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ApplyPatchesAsync_WithCsFiles_IncludesRenameFileTool()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);
        await File.WriteAllTextAsync(
            Path.Combine(customizationRoot, "WidgetClient.cs"),
            "public partial class WidgetClient { }");

        _copilotAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult("done"));

        await _service.ApplyPatchesAsync(
            customizationRoot,
            _tempDir.DirectoryPath,
            "error CS0246: type or namespace name 'WidgetClient' could not be found",
            CancellationToken.None);

        _copilotAgentRunner.Verify(
            r => r.RunAsync(
                It.Is<CopilotAgent<string>>(a =>
                    a.Tools.Any(t => t.Name == "RenameFile")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ApplyPatchesAsync_AgentThrows_ReturnsEmptyList()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);
        await File.WriteAllTextAsync(
            Path.Combine(customizationRoot, "Test.cs"),
            "public partial class Test { }");

        _copilotAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent failed"));

        var result = await _service.ApplyPatchesAsync(
            customizationRoot,
            _tempDir.DirectoryPath,
            "error CS0117: some error",
            CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ApplyPatchesAsync_ExcludesGeneratedFiles()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        var generatedDir = Path.Combine(customizationRoot, "Generated");
        Directory.CreateDirectory(customizationRoot);
        Directory.CreateDirectory(generatedDir);

        // Create a customization file (should be included)
        await File.WriteAllTextAsync(
            Path.Combine(customizationRoot, "WidgetClient.cs"),
            "public partial class WidgetClient { }");

        // Create a generated file (should be excluded)
        await File.WriteAllTextAsync(
            Path.Combine(generatedDir, "WidgetClient.cs"),
            "public partial class WidgetClient { // generated }");

        _copilotAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult("done"));

        await _service.ApplyPatchesAsync(
            customizationRoot,
            _tempDir.DirectoryPath,
            "error CS0117: some error",
            CancellationToken.None);

        // Verify the agent was called with instructions that contain the customization file
        // but not the generated file path in the file lists
        _copilotAgentRunner.Verify(
            r => r.RunAsync(
                It.Is<CopilotAgent<string>>(a =>
                    a.Instructions.Contains("WidgetClient.cs") &&
                    !a.Instructions.Contains("Generated" + Path.DirectorySeparatorChar + "WidgetClient.cs")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public void ApplyPatchesAsync_CancellationRequested_Throws()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);
        File.WriteAllText(
            Path.Combine(customizationRoot, "Test.cs"),
            "public partial class Test { }");

        _copilotAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ApplyPatchesAsync(
                customizationRoot,
                _tempDir.DirectoryPath,
                "error CS0117: some error",
                CancellationToken.None));
    }
}
