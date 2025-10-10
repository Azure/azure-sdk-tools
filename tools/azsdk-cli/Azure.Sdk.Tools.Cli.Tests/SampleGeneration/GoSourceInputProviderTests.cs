// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.SampleGeneration.Languages;

namespace Azure.Sdk.Tools.Cli.Tests.SampleGeneration;

public class GoSourceInputProviderTests
{
    [Test]
    public void Create_Returns_Single_Input_With_Go_Extension()
    {
        var provider = new GoSourceInputProvider();
        var packagePath = Path.Combine(Path.GetTempPath(), "azsdk-go-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packagePath);

        var inputs = provider.Create(packagePath);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(1), "Should return exactly one source input");
            Assert.That(inputs[0].Path, Is.EqualTo(packagePath), "Path should match the provided package path");
            Assert.That(inputs[0].IncludeExtensions, Is.Not.Null, "IncludeExtensions should be set");
            Assert.That(inputs[0].IncludeExtensions, Does.Contain(".go"), "Should include .go extension filter");
        });
    }

    [Test]
    public void Create_Does_Not_Require_Existing_Path()
    {
        var provider = new GoSourceInputProvider();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "azsdk-go-missing-" + Guid.NewGuid().ToString("N"));
        // Intentionally do NOT create the directory

        var inputs = provider.Create(nonExistentPath);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(1), "Should still return a single input even if directory doesn't exist");
            Assert.That(inputs[0].Path, Is.EqualTo(nonExistentPath), "Path should be the provided (possibly non-existent) path");
            Assert.That(inputs[0].IncludeExtensions, Does.Contain(".go"), "Should include .go extension");
        });
    }

    [Test]
    public void Create_Returns_New_List_Instance_On_Each_Call()
    {
        var provider = new GoSourceInputProvider();
        var path1 = Path.Combine(Path.GetTempPath(), "azsdk-go-test1-" + Guid.NewGuid().ToString("N"));
        var path2 = Path.Combine(Path.GetTempPath(), "azsdk-go-test2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path1);
        Directory.CreateDirectory(path2);

        var first = provider.Create(path1);
        var second = provider.Create(path2);

        Assert.Multiple(() =>
        {
            Assert.That(first[0].Path, Is.EqualTo(path1));
            Assert.That(second[0].Path, Is.EqualTo(path2));
            Assert.That(!ReferenceEquals(first, second), "Each call should return a new list instance");
        });
    }
}
