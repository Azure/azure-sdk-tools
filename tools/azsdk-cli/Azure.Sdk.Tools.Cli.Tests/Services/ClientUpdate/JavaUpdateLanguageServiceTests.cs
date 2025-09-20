// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Services; // for ILanguageSpecificCheckResolver & ILanguageSpecificChecks
using Azure.Sdk.Tools.Cli.Services.ClientUpdate; // for JavaUpdateLanguageService & JavaApiViewMethodIndexLoader
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Services.ClientUpdate;

[TestFixture]
public class JavaUpdateLanguageServiceTests
{
    private class DummyResolver : ILanguageSpecificCheckResolver
    {
        public Task<ILanguageSpecificChecks?> GetLanguageCheckAsync(string packagePath) => Task.FromResult<ILanguageSpecificChecks?>(null);
    }

    private class StubJavaUpdateLanguageService : JavaUpdateLanguageService
    {
        public StubJavaUpdateLanguageService() : base(new DummyResolver(), NullLogger<JavaUpdateLanguageService>.Instance) { }
    }

    [Test]
    public async Task ComputeApiChanges_ReturnsEmptyWhenNoSources()
    {
        var svc = new StubJavaUpdateLanguageService();
        var result = await svc.DiffAsync(oldGenerationPath: "old", newGenerationPath: "new");
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ComputeApiChanges_ThrowsIfPathsMissing()
    {
        var svc = new StubJavaUpdateLanguageService();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.DiffAsync(string.Empty, "new"));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.DiffAsync("old", string.Empty));
    }

    [Test]
    public async Task MethodIndexDiff_AddRemoveRename()
    {
        var asmDir = Path.GetDirectoryName(typeof(JavaUpdateLanguageServiceTests).Assembly.Location)!;
        string? sourceFixtureDir = null;
        var probe = new DirectoryInfo(asmDir);
        for (int i = 0; i < 10 && probe != null; i++)
        {
            var candidate = Path.Combine(probe.FullName, "tools", "azsdk-cli", "Azure.Sdk.Tools.Cli.Tests", "TestAssets", "JavaMethodIndexDiff"); // moved from TestData to TestAssets
            if (Directory.Exists(candidate))
            {
                sourceFixtureDir = candidate;
                break;
            }
            probe = probe.Parent;
        }
        Assert.That(sourceFixtureDir, Is.Not.Null, "Could not locate JavaMethodIndexDiff fixture directory in source tree.");

        var oldDir = Path.Combine(sourceFixtureDir!, "old");
        var newDir = Path.Combine(sourceFixtureDir!, "new");
        Assert.That(Directory.Exists(oldDir), Is.True, "Old fixtures missing");
        Assert.That(Directory.Exists(newDir), Is.True, "New fixtures missing");

        var oldIndex = JavaApiViewMethodIndexLoader.LoadMerged(oldDir);
        var newIndex = JavaApiViewMethodIndexLoader.LoadMerged(newDir);
        var changes = JavaApiViewMethodIndexLoader.ComputeChanges(oldIndex, newIndex);

        Assert.Multiple(() =>
        {
            Assert.That(changes.Any(c => c.Kind == "MethodAdded" && c.Symbol == "com.example.Foo#methodC()"));
            Assert.That(changes.Any(c => c.Kind == "MethodRemoved" && c.Symbol == "com.example.Foo#methodB()"));
            Assert.That(changes.Any(c => c.Kind == "MethodParameterNameChanged" && c.Symbol == "com.example.Foo#methodA(int)" && c.Metadata["oldName"] == "x" && c.Metadata["newName"] == "y"));
        });
    }
}
