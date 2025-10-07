// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Sdk.Tools.Cli.SampleGeneration.Languages;

namespace Azure.Sdk.Tools.Cli.Tests.SampleGeneration;

public class DotNetSourceInputProviderTests
{
    private static string CreateTempPackage(out string srcDir, out string testsDir)
    {
        string root = Path.Combine(Path.GetTempPath(), "azsdk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        srcDir = Path.Combine(root, "src");
        testsDir = Path.Combine(root, "tests");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(testsDir);
        return root;
    }

    private static string WriteFile(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    [Test]
    public void Includes_Infrastructure_Base_Class_No_Test_Methods()
    {
        var provider = new DotNetSourceInputProvider();
        var root = CreateTempPackage(out var src, out var tests);

        WriteFile(src, "Client.cs", "public class Client { }");
        WriteFile(tests, "KeysTestBase.cs", @"using NUnit.Framework; public abstract class KeysTestBase { [SetUp] public void Setup(){} }");

        var inputs = provider.Create(root);

        Assert.Multiple(() =>
        {
            // Expect: src folder and KeysTestBase.cs represented (as individual file input), no test methods so it's infra.
            Assert.That(inputs.Any(i => i.Path == src), "src directory should be included");
            Assert.That(inputs.Any(i => i.Path.EndsWith("KeysTestBase.cs")), "KeysTestBase.cs should be included as infra");
        });

    }

    [Test]
    public void Excludes_File_With_Test_Method()
    {
        var provider = new DotNetSourceInputProvider();
        var root = CreateTempPackage(out var src, out var tests);

        WriteFile(src, "Client.cs", "public class Client { }");
        WriteFile(tests, "KeyClientTests.cs", @"using NUnit.Framework; public class KeyClientTests { [Test] public void TestA(){} }");

        var inputs = provider.Create(root);

        Assert.That(!inputs.Any(i => i.Path.EndsWith("KeyClientTests.cs")), "Test file with [Test] should be excluded");
    }

    [Test]
    public void Includes_File_With_Lifecycle_Only()
    {
        var provider = new DotNetSourceInputProvider();
        var root = CreateTempPackage(out var src, out var tests);

        WriteFile(src, "Client.cs", "public class Client { }");
        WriteFile(tests, "Fixture.cs", @"using NUnit.Framework; public class Fixture { [OneTimeSetUp] public void Init(){} }");

        var inputs = provider.Create(root);

        Assert.That(inputs.Any(i => i.Path.EndsWith("Fixture.cs")), "File with only lifecycle attributes should be treated as infra");
    }

    [Test]
    public void Excludes_File_With_TestCase_Method()
    {
        var provider = new DotNetSourceInputProvider();
        var root = CreateTempPackage(out var src, out var tests);

        WriteFile(src, "Client.cs", "public class Client { }");
        WriteFile(tests, "SomethingTests.cs", @"using NUnit.Framework; public class SomethingTests { [TestCase(1)] public void TestA(int x){} }");

        var inputs = provider.Create(root);
        Assert.That(!inputs.Any(i => i.Path.EndsWith("SomethingTests.cs")), "File containing [TestCase] should be excluded");
    }

    [Test]
    public void Includes_Abstract_Base_With_Inheritance_Of_RecordedTestBase()
    {
        var provider = new DotNetSourceInputProvider();
        var root = CreateTempPackage(out var src, out var tests);

        WriteFile(src, "Client.cs", "public class Client { }");
        WriteFile(tests, "CustomTestBase.cs", @"public abstract class CustomTestBase : RecordedTestBase<object> { protected void Helper(){} }");

        var inputs = provider.Create(root);
        Assert.That(inputs.Any(i => i.Path.EndsWith("CustomTestBase.cs")), "Abstract base inheriting RecordedTestBase should be included");
    }
}
