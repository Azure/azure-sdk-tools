// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Services.Languages.Samples;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Samples;

public class GoSourceInputProviderTests
{
    [Test]
    public void Create_Returns_Single_Input_With_Go_Extension()
    {
        var provider = new GoSourceInputProvider();
        using var temp = TempDirectory.Create("azsdk-go-test");
        var packagePath = temp.DirectoryPath;

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
        using var temp1 = TempDirectory.Create("azsdk-go-test1");
        using var temp2 = TempDirectory.Create("azsdk-go-test2");
        var path1 = temp1.DirectoryPath;
        var path2 = temp2.DirectoryPath;

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
