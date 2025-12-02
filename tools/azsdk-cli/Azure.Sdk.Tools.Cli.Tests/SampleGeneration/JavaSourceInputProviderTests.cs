// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Services.Languages.Samples;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Samples;

public class JavaSourceInputProviderTests
{
    private static TempDirectory CreateTempPackage()
    {
        return TempDirectory.Create("azsdk-java-test");
    }

    private static void WriteFile(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Test]
    public void Includes_Src_Directory_When_Present()
    {
        var provider = new JavaSourceInputProvider();
        using var root = CreateTempPackage();
        var srcDir = Path.Combine(root.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);
        WriteFile(srcDir, "Client.java", "class Client {}");

        var inputs = provider.Create(root.DirectoryPath);

        Assert.Multiple(() =>
        {
            Assert.That(inputs.Count, Is.EqualTo(1), "Should have exactly one input when only src directory exists");
            Assert.That(inputs[0].Path, Is.EqualTo(srcDir), "Should include src directory");
            Assert.That(inputs[0].IncludeExtensions, Is.Not.Null, "Should have include extensions specified");
            Assert.That(inputs[0].IncludeExtensions, Does.Contain(".java"), "Should include .java files");
        });
    }

    [Test]
    public void Throws_When_Src_Directory_Missing()
    {
        var provider = new JavaSourceInputProvider();
        using var root = CreateTempPackage();
        var ex = Assert.Throws<ArgumentException>(() => provider.Create(root.DirectoryPath));
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("src"), "Error message should mention src directory");
            Assert.That(ex.Message, Does.Contain(root.DirectoryPath), "Error message should include the provided path");
            Assert.That(ex.ParamName, Is.EqualTo("packagePath"), "Should specify packagePath as the problem parameter");
        });
    }

    [Test]
    public void Includes_Samples_Directory_When_Present()
    {
        var provider = new JavaSourceInputProvider();
        using var root = CreateTempPackage();
        var srcDir = Path.Combine(root.DirectoryPath, "src");
        var samplesDir = Path.Combine(root.DirectoryPath, "samples");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(samplesDir);
        WriteFile(srcDir, "Client.java", "class Client {}");
        WriteFile(samplesDir, "Sample1.java", "class Sample1 {}");

        var inputs = provider.Create(root.DirectoryPath);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(2), "Should include both src and samples directories");
            Assert.That(inputs.Any(i => i.Path == srcDir), "Should include src directory");
            Assert.That(inputs.Any(i => i.Path == samplesDir), "Should include samples directory");
            var samplesInput = inputs.First(i => i.Path == samplesDir);
            Assert.That(samplesInput.IncludeExtensions, Does.Contain(".java"), "Samples directory should include .java files");
        });
    }

    [Test]
    public void Works_Without_Samples_Directory()
    {
        var provider = new JavaSourceInputProvider();
        using var root = CreateTempPackage();
        var srcDir = Path.Combine(root.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);
        WriteFile(srcDir, "Client.java", "class Client {}");

        var inputs = provider.Create(root.DirectoryPath);

        Assert.Multiple(() =>
        {
            Assert.That(inputs.Count, Is.EqualTo(1), "Should work without samples directory");
            Assert.That(inputs[0].Path, Is.EqualTo(srcDir), "Should still include src directory");
        });
    }

    [Test]
    public void Includes_Test_Resources_Files_From_Parent_Directory()
    {
        var provider = new JavaSourceInputProvider();
        using var parentDirTemp = TempDirectory.Create("azsdk-java-parent");
        var parentDir = parentDirTemp.DirectoryPath;
        var packageDir = Path.Combine(parentDir, "package");
        Directory.CreateDirectory(packageDir);

        var srcDir = Path.Combine(packageDir, "src");
        Directory.CreateDirectory(srcDir);
        WriteFile(srcDir, "Client.java", "class Client {}");

        var testResourcesFile1 = Path.Combine(parentDir, "test-resources.json");
        var testResourcesFile2 = Path.Combine(parentDir, "test-resources-post.ps1");
        File.WriteAllText(testResourcesFile1, "{}");
        File.WriteAllText(testResourcesFile2, "{}");

        var inputs = provider.Create(packageDir);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(3), "Should include src dir + 2 test-resources files");
            Assert.That(inputs.Any(i => i.Path == srcDir), "Should include src directory");
            Assert.That(inputs.Any(i => i.Path == testResourcesFile1), "Should include test-resources.json");
            Assert.That(inputs.Any(i => i.Path == testResourcesFile2), "Should include test-resources-post.ps1");
        });
    }

    [Test]
    public void Works_Without_Test_Resources_Files()
    {
        var provider = new JavaSourceInputProvider();
        using var root = CreateTempPackage();
        var srcDir = Path.Combine(root.DirectoryPath, "src");
        Directory.CreateDirectory(srcDir);
        WriteFile(srcDir, "Client.java", "class Client {}");

        var inputs = provider.Create(root.DirectoryPath);

        Assert.Multiple(() =>
        {
            Assert.That(inputs.Count, Is.EqualTo(1), "Should work without test-resources files");
            Assert.That(inputs[0].Path, Is.EqualTo(srcDir), "Should still include src directory");
        });
    }

    [Test]
    public void Includes_All_Components_When_Everything_Present()
    {
        var provider = new JavaSourceInputProvider();
        using var parentDirTemp = TempDirectory.Create("azsdk-java-full");
        var parentDir = parentDirTemp.DirectoryPath;
        var packageDir = Path.Combine(parentDir, "package");
        Directory.CreateDirectory(packageDir);

        var srcDir = Path.Combine(packageDir, "src");
        Directory.CreateDirectory(srcDir);
        WriteFile(srcDir, "Client.java", "class Client {}");
        WriteFile(srcDir, "Model.java", "class Model {}");

        var samplesDir = Path.Combine(packageDir, "samples");
        Directory.CreateDirectory(samplesDir);
        WriteFile(samplesDir, "Sample1.java", "class Sample1 {}");
        WriteFile(samplesDir, "Sample2.java", "class Sample2 {}");

        var testResourcesFile = Path.Combine(parentDir, "test-resources.json");
        File.WriteAllText(testResourcesFile, "{}");

        var inputs = provider.Create(packageDir);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(3), "Should include src dir + samples dir + test-resources file");
            Assert.That(inputs.Any(i => i.Path == srcDir), "Should include src directory");
            Assert.That(inputs.Any(i => i.Path == samplesDir), "Should include samples directory");
            Assert.That(inputs.Any(i => i.Path == testResourcesFile), "Should include test-resources file");

            var directoryInputs = inputs.Where(i => Directory.Exists(i.Path));
            Assert.That(directoryInputs.All(i => i.IncludeExtensions != null && i.IncludeExtensions.Contains(".java")),
                "All directory inputs should include .java files");
        });
    }
}
