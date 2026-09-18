// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Repair;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents.Tools;

[TestFixture]
public class RepairFileToolsTests
{
    private string _root = null!;
    private CustomizationFilePolicy _policy = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.GetFullPath($"repair-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _policy = new CustomizationFilePolicy(SdkLanguage.DotNet, _root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [TestCase("src/Generated/Client.cs")]
    [TestCase("src/Client.csproj")]
    [TestCase("src/tsp-location.yaml")]
    [TestCase("src/Properties/AssemblyInfo.cs")]
    [TestCase("src/Properties/AssemblyVersion.cs")]
    [TestCase("src/Version.cs")]
    [TestCase("src/.git/Client.cs")]
    public async Task PatchGuardRejectsGeneratedAndMetadataBeforeWrite(string relativePath)
    {
        var path = WriteFile(relativePath);
        var result = await CodePatchTools.ApplyPatchAsync(_root, relativePath, 1, 1, "before", "after",
            CancellationToken.None, _policy.IsCustomFile);
        Assert.That(result.Success, Is.False);
        Assert.That(File.ReadAllText(path), Is.EqualTo("before"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task PatchGuardEvaluatesFinalFallbackCandidate(bool allowed)
    {
        var path = WriteFile(allowed ? "src/custom/Client.cs" : "src/Generated/Client.cs");
        string? checkedPath = null;
        var result = await CodePatchTools.ApplyPatchAsync(_root, "Client.cs", 1, 1, "before", "after",
            CancellationToken.None, candidate =>
            {
                checkedPath = candidate;
                return _policy.IsCustomFile(candidate);
            });
        Assert.Multiple(() =>
        {
            Assert.That(checkedPath, Is.EqualTo(path));
            Assert.That(result.Success, Is.EqualTo(allowed));
            Assert.That(File.ReadAllText(path).Trim(), Is.EqualTo(allowed ? "after" : "before"));
        });
    }

    [TestCase("src/*.cs")]
    [TestCase("../Client.cs")]
    [TestCase("src/Client.cs:stream")]
    [TestCase("src/Client.cs.")]
    [TestCase("src/Client.cs ")]
    public async Task GuardedPatchRejectsMalformedPathsWithoutFilenameFallback(string path)
    {
        var original = WriteFile("src/Client.cs");
        var result = await CodePatchTools.ApplyPatchAsync(_root, path, 1, 1, "before", "after",
            CancellationToken.None, _policy.IsCustomFile);
        Assert.That(result.Success, Is.False);
        Assert.That(File.ReadAllText(original), Is.EqualTo("before"));
    }

    [TestCase("src/Generated/New.cs")]
    [TestCase("src/New.csproj")]
    [TestCase("src/eng/New.cs")]
    [TestCase("src/Properties/AssemblyInfo.cs")]
    [TestCase("src/Version.cs")]
    [TestCase("src/.git/New.cs")]
    [TestCase("../New.cs")]
    [TestCase("src/New.cs.")]
    [TestCase("src/New.cs ")]
    public void RenameGuardRejectsDestinationBeforeDirectoryCreation(string destination)
    {
        var original = WriteFile("src/Client.cs");
        var renamed = false;
        var tool = FileTools.CreateRenameFileTool(_root, onFileRenamed: (_, _) => renamed = true,
            isSourceAllowed: _policy.IsCustomFile, isDestinationAllowed: _policy.IsCustomFile);
        Assert.ThrowsAsync<ArgumentException>(async () => await tool.InvokeAsync(new AIFunctionArguments
        {
            ["oldFilePath"] = "src/Client.cs",
            ["newFilePath"] = destination
        }));
        Assert.That(File.Exists(original), Is.True);
        Assert.That(renamed, Is.False);
        Assert.That(Directory.Exists(Path.Combine(_root, "src", "Generated")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_root, "src", "Properties")), Is.False);
    }

    [Test]
    public void RenameGuardRejectsGeneratedSource()
    {
        var path = WriteFile("src/Generated/Client.cs");
        var tool = FileTools.CreateRenameFileTool(_root,
            isSourceAllowed: _policy.IsCustomFile, isDestinationAllowed: _policy.IsCustomFile);
        Assert.ThrowsAsync<ArgumentException>(async () => await tool.InvokeAsync(new AIFunctionArguments
        {
            ["oldFilePath"] = "src/Generated/Client.cs",
            ["newFilePath"] = "src/Client.cs"
        }));
        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public async Task MetadataFilenameFallbackIsRejectedBeforeMutation()
    {
        var path = WriteFile("src/Properties/AssemblyInfo.cs");
        var result = await CodePatchTools.ApplyPatchAsync(_root, "AssemblyInfo.cs", 1, 1, "before", "after",
            CancellationToken.None, _policy.IsCustomFile);
        Assert.That(result.Success, Is.False);
        Assert.That(File.ReadAllText(path), Is.EqualTo("before"));
    }

    [Test]
    public void RenameGuardRejectsMetadataHiddenInAnOrdinarySourceFilename()
    {
        var path = WriteFile("src/Client.cs");
        File.WriteAllText(path, "[assembly: System.Reflection.AssemblyVersion(\"1.0.0\")]");
        var tool = FileTools.CreateRenameFileTool(_root,
            isSourceAllowed: _policy.IsCustomFile, isDestinationAllowed: _policy.IsCustomFile);
        Assert.ThrowsAsync<ArgumentException>(async () => await tool.InvokeAsync(new AIFunctionArguments
        {
            ["oldFilePath"] = "src/Client.cs", ["newFilePath"] = "src/NormalClient.cs"
        }));
        Assert.That(File.Exists(path), Is.True);
        Assert.That(File.Exists(Path.Combine(_root, "src", "NormalClient.cs")), Is.False);
    }

    [Test]
    public void PatchCancellationIsNotConvertedToToolFailure()
    {
        WriteFile("src/Client.cs");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => CodePatchTools.ApplyPatchAsync(_root, "src/Client.cs",
            1, 1, "before", "after", cts.Token, _policy.IsCustomFile));
    }

    [Test]
    public async Task RepairReadAndGrepRejectLinksAndDoNotTraverseLinkedDirectories()
    {
        var target = WriteFile("src/real/Client.cs");
        var link = Path.Combine(_root, "src", "linked");
        try
        {
            Directory.CreateSymbolicLink(link, Path.GetDirectoryName(target)!);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Ignore($"This host cannot create symbolic links: {ex.Message}");
        }
        bool IsAllowed(string path) => ToolHelpers.IsPathWithinDirectoryWithoutLinks(_root, path);
        var read = FileTools.CreateReadFileTool(_root, isPathAllowed: IsAllowed);
        Assert.ThrowsAsync<ArgumentException>(async () => await read.InvokeAsync(new AIFunctionArguments
        {
            ["filePath"] = "src/linked/Client.cs"
        }));
        var grep = FileTools.CreateGrepSearchTool(_root, isPathAllowed: IsAllowed);
        Assert.ThrowsAsync<ArgumentException>(async () => await grep.InvokeAsync(new AIFunctionArguments
        {
            ["pattern"] = "before", ["path"] = "src/linked"
        }));
        var result = await grep.InvokeAsync(new AIFunctionArguments { ["pattern"] = "before", ["path"] = "." });
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.That(json, Does.Contain("real").And.Not.Contain("linked"));
        var patch = await CodePatchTools.ApplyPatchAsync(_root, "src/linked/Client.cs", 1, 1, "before", "after",
            CancellationToken.None, _policy.IsCustomFile);
        Assert.That(patch.Success, Is.False);
        Assert.That(File.ReadAllText(target), Is.EqualTo("before"));
        var rename = FileTools.CreateRenameFileTool(_root,
            isSourceAllowed: _policy.IsCustomFile, isDestinationAllowed: _policy.IsCustomFile);
        Assert.ThrowsAsync<ArgumentException>(async () => await rename.InvokeAsync(new AIFunctionArguments
        {
            ["oldFilePath"] = "src/real/Client.cs", ["newFilePath"] = "src/linked/New.cs"
        }));
        Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(target)!, "New.cs")), Is.False);
    }

    private string WriteFile(string relativePath)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "before");
        return path;
    }
}
