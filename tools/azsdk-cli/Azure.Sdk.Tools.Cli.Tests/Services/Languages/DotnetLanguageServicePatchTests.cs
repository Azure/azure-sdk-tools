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
    public async Task ApplyPatchesAsync_CancellationRequested_ReturnsEmptyList()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);
        await File.WriteAllTextAsync(
            Path.Combine(customizationRoot, "Test.cs"),
            "public partial class Test { }");

        _copilotAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _service.ApplyPatchesAsync(
            customizationRoot,
            _tempDir.DirectoryPath,
            "error CS0117: some error",
            CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    // ── RenameFilesToMatchClassNames tests ──

    [Test]
    public void RenameFilesToMatchClassNames_RenamesFileWhenClassNameDiffers()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);

        var oldFile = Path.Combine(customizationRoot, "OldClient.cs");
        File.WriteAllText(oldFile, "public partial class NewClient { }");

        var patches = new List<Azure.Sdk.Tools.Cli.Models.Responses.Package.AppliedPatch>
        {
            new("OldClient.cs", "Renamed class", 1)
        };

        _service.RenameFilesToMatchClassNames(customizationRoot, Path.DirectorySeparatorChar + "Generated" + Path.DirectorySeparatorChar, patches);

        Assert.That(File.Exists(Path.Combine(customizationRoot, "NewClient.cs")), Is.True);
        Assert.That(File.Exists(oldFile), Is.False);
        Assert.That(patches.Any(p => p.FilePath == "NewClient.cs"), Is.True);
    }

    [Test]
    public void RenameFilesToMatchClassNames_DoesNotRenameWhenNameAlreadyMatches()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);

        var file = Path.Combine(customizationRoot, "MyClient.cs");
        File.WriteAllText(file, "public partial class MyClient { }");

        var patches = new List<Azure.Sdk.Tools.Cli.Models.Responses.Package.AppliedPatch>
        {
            new("MyClient.cs", "Some patch", 1)
        };

        _service.RenameFilesToMatchClassNames(customizationRoot, Path.DirectorySeparatorChar + "Generated" + Path.DirectorySeparatorChar, patches);

        Assert.That(File.Exists(file), Is.True);
        Assert.That(patches.Count, Is.EqualTo(1)); // No rename patch added
    }

    [Test]
    public void RenameFilesToMatchClassNames_SkipsUnpatchedFiles()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);

        // This file was NOT patched by the agent
        var file = Path.Combine(customizationRoot, "OldClient.cs");
        File.WriteAllText(file, "public partial class NewClient { }");

        var patches = new List<Azure.Sdk.Tools.Cli.Models.Responses.Package.AppliedPatch>();

        _service.RenameFilesToMatchClassNames(customizationRoot, Path.DirectorySeparatorChar + "Generated" + Path.DirectorySeparatorChar, patches);

        // File should NOT be renamed because it wasn't in the patch log
        Assert.That(File.Exists(file), Is.True);
        Assert.That(File.Exists(Path.Combine(customizationRoot, "NewClient.cs")), Is.False);
    }

    [Test]
    public void RenameFilesToMatchClassNames_RemovesDuplicateAndRenames()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        Directory.CreateDirectory(customizationRoot);

        // Old file was patched (has real customization content)
        var oldFile = Path.Combine(customizationRoot, "OldClient.cs");
        File.WriteAllText(oldFile, "public partial class NewClient\n{\n    public void CustomMethod() { }\n}");

        // Duplicate file already exists at the target name
        var dupFile = Path.Combine(customizationRoot, "NewClient.cs");
        File.WriteAllText(dupFile, "public partial class NewClient { }");

        var patches = new List<Azure.Sdk.Tools.Cli.Models.Responses.Package.AppliedPatch>
        {
            new("OldClient.cs", "Renamed class", 1)
        };

        _service.RenameFilesToMatchClassNames(customizationRoot, Path.DirectorySeparatorChar + "Generated" + Path.DirectorySeparatorChar, patches);

        Assert.That(File.Exists(Path.Combine(customizationRoot, "NewClient.cs")), Is.True);
        Assert.That(File.Exists(oldFile), Is.False);
        // The moved file should have the real content
        var content = File.ReadAllText(Path.Combine(customizationRoot, "NewClient.cs"));
        Assert.That(content, Does.Contain("CustomMethod"));
    }

    [Test]
    public void RenameFilesToMatchClassNames_SkipsGeneratedFiles()
    {
        var customizationRoot = Path.Combine(_tempDir.DirectoryPath, "src");
        var generatedDir = Path.Combine(customizationRoot, "Generated");
        Directory.CreateDirectory(generatedDir);

        var genFile = Path.Combine(generatedDir, "OldClient.cs");
        File.WriteAllText(genFile, "public partial class NewClient { }");

        var patches = new List<Azure.Sdk.Tools.Cli.Models.Responses.Package.AppliedPatch>
        {
            new(Path.Combine("Generated", "OldClient.cs"), "Renamed class", 1)
        };

        _service.RenameFilesToMatchClassNames(customizationRoot, Path.DirectorySeparatorChar + "Generated" + Path.DirectorySeparatorChar, patches);

        // Generated files should be left alone
        Assert.That(File.Exists(genFile), Is.True);
    }
}
