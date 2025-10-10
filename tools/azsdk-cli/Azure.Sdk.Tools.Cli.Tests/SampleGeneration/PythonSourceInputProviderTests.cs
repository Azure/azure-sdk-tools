// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.SampleGeneration.Languages;

namespace Azure.Sdk.Tools.Cli.Tests.SampleGeneration;

public class PythonSourceInputProviderTests
{
    private static string CreateTempPackage()
    {
        string root = Path.Combine(Path.GetTempPath(), "azsdk-python-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFile(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Test]
    public void Includes_Azure_Directory_When_Present()
    {
        var provider = new PythonSourceInputProvider();
        var root = CreateTempPackage();
        var azureDir = Path.Combine(root, "azure");
        Directory.CreateDirectory(azureDir);
        WriteFile(azureDir, "client.py", "class Client: pass");

        var inputs = provider.Create(root);

        Assert.Multiple(() =>
        {
            Assert.That(inputs.Count, Is.EqualTo(1), "Should have exactly one input when only azure directory exists");
            Assert.That(inputs[0].Path, Is.EqualTo(azureDir), "Should include azure directory");
            Assert.That(inputs[0].IncludeExtensions, Is.Not.Null, "Should have include extensions specified");
            Assert.That(inputs[0].IncludeExtensions, Does.Contain(".py"), "Should include .py files");
        });
    }

    [Test]
    public void Throws_When_Azure_Directory_Missing()
    {
        var provider = new PythonSourceInputProvider();
        var root = CreateTempPackage();

        var ex = Assert.Throws<ArgumentException>(() => provider.Create(root));
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("azure"), "Error message should mention azure directory");
            Assert.That(ex.Message, Does.Contain(root), "Error message should include the provided path");
            Assert.That(ex.ParamName, Is.EqualTo("packagePath"), "Should specify packagePath as the problem parameter");
        });
    }

    [Test]
    public void Includes_Samples_Directory_When_Present()
    {
        var provider = new PythonSourceInputProvider();
        var root = CreateTempPackage();
        var azureDir = Path.Combine(root, "azure");
        var samplesDir = Path.Combine(root, "samples");
        Directory.CreateDirectory(azureDir);
        Directory.CreateDirectory(samplesDir);
        WriteFile(azureDir, "client.py", "class Client: pass");
        WriteFile(samplesDir, "sample1.py", "# Sample code");

        var inputs = provider.Create(root);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(2), "Should include both azure and samples directories");
            Assert.That(inputs.Any(i => i.Path == azureDir), "Should include azure directory");
            Assert.That(inputs.Any(i => i.Path == samplesDir), "Should include samples directory");
            
            var samplesInput = inputs.First(i => i.Path == samplesDir);
            Assert.That(samplesInput.IncludeExtensions, Does.Contain(".py"), "Samples directory should include .py files");
        });
    }

    [Test]
    public void Works_Without_Samples_Directory()
    {
        var provider = new PythonSourceInputProvider();
        var root = CreateTempPackage();
        var azureDir = Path.Combine(root, "azure");
        Directory.CreateDirectory(azureDir);
        WriteFile(azureDir, "client.py", "class Client: pass");

        var inputs = provider.Create(root);

        Assert.Multiple(() =>
        {
            Assert.That(inputs.Count, Is.EqualTo(1), "Should work without samples directory");
            Assert.That(inputs[0].Path, Is.EqualTo(azureDir), "Should still include azure directory");
        });
    }

    [Test]
    public void Includes_Test_Resources_Files_From_Parent_Directory()
    {
        var provider = new PythonSourceInputProvider();
        var tempRoot = Path.GetTempPath();
        var parentDir = Path.Combine(tempRoot, "azsdk-python-parent-" + Guid.NewGuid().ToString("N"));
        var packageDir = Path.Combine(parentDir, "package");
        Directory.CreateDirectory(packageDir);
        
        var azureDir = Path.Combine(packageDir, "azure");
        Directory.CreateDirectory(azureDir);
        WriteFile(azureDir, "client.py", "class Client: pass");
        
        var testResourcesFile1 = Path.Combine(parentDir, "test-resources.json");
        var testResourcesFile2 = Path.Combine(parentDir, "test-resources-post.ps1");
        File.WriteAllText(testResourcesFile1, "{}");
        File.WriteAllText(testResourcesFile2, "{}");

        var inputs = provider.Create(packageDir);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(3), "Should include azure dir + 2 test-resources files");
            Assert.That(inputs.Any(i => i.Path == azureDir), "Should include azure directory");
            Assert.That(inputs.Any(i => i.Path == testResourcesFile1), "Should include test-resources.json");
            Assert.That(inputs.Any(i => i.Path == testResourcesFile2), "Should include test-resources-dev.json");
        });
    }

    [Test]
    public void Works_Without_Test_Resources_Files()
    {
        var provider = new PythonSourceInputProvider();
        var root = CreateTempPackage();
        var azureDir = Path.Combine(root, "azure");
        Directory.CreateDirectory(azureDir);
        WriteFile(azureDir, "client.py", "class Client: pass");

        var inputs = provider.Create(root);

        Assert.Multiple(() =>
        {
            Assert.That(inputs.Count, Is.EqualTo(1), "Should work without test-resources files");
            Assert.That(inputs[0].Path, Is.EqualTo(azureDir), "Should still include azure directory");
        });
    }

    [Test]
    public void Works_When_Package_Has_No_Parent_Directory()
    {
        var provider = new PythonSourceInputProvider();
        var root = CreateTempPackage();
        var azureDir = Path.Combine(root, "azure");
        Directory.CreateDirectory(azureDir);
        WriteFile(azureDir, "client.py", "class Client: pass");

        var inputs = provider.Create(root);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(1), "Should work when package has no parent directory or no test-resources files");
            Assert.That(inputs[0].Path, Is.EqualTo(azureDir), "Should still include azure directory");
        });
    }

    [Test]
    public void Includes_All_Components_When_Everything_Present()
    {
        var provider = new PythonSourceInputProvider();
        var tempRoot = Path.GetTempPath();
        var parentDir = Path.Combine(tempRoot, "azsdk-python-full-" + Guid.NewGuid().ToString("N"));
        var packageDir = Path.Combine(parentDir, "package");
        Directory.CreateDirectory(packageDir);
        
        var azureDir = Path.Combine(packageDir, "azure");
        Directory.CreateDirectory(azureDir);
        WriteFile(azureDir, "client.py", "class Client: pass");
        WriteFile(azureDir, "models.py", "class Model: pass");
        
        var samplesDir = Path.Combine(packageDir, "samples");
        Directory.CreateDirectory(samplesDir);
        WriteFile(samplesDir, "sample1.py", "# Sample 1");
        WriteFile(samplesDir, "sample2.py", "# Sample 2");
        
        var testResourcesFile = Path.Combine(parentDir, "test-resources.json");
        File.WriteAllText(testResourcesFile, "{}");

        var inputs = provider.Create(packageDir);

        Assert.Multiple(() =>
        {
            Assert.That(inputs, Has.Count.EqualTo(3), "Should include azure dir + samples dir + test-resources file");
            Assert.That(inputs.Any(i => i.Path == azureDir), "Should include azure directory");
            Assert.That(inputs.Any(i => i.Path == samplesDir), "Should include samples directory");
            Assert.That(inputs.Any(i => i.Path == testResourcesFile), "Should include test-resources file");
            
            var directoryInputs = inputs.Where(i => Directory.Exists(i.Path));
            Assert.That(directoryInputs.All(i => i.IncludeExtensions != null && i.IncludeExtensions.Contains(".py")), 
                "All directory inputs should include .py files");
        });
    }
  }