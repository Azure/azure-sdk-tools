// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Repair;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Repair;

[TestFixture]
public class CustomizationFilePolicyTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.GetFullPath($"repair-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [TestCase(SdkLanguage.DotNet, "src/Client.cs", true, false)]
    [TestCase(SdkLanguage.DotNet, "src/Customized/Client.cs", true, false)]
    [TestCase(SdkLanguage.DotNet, "src/Generated/Client.cs", false, true)]
    [TestCase(SdkLanguage.DotNet, "src/gEnErAtEd/Client.CS", false, true)]
    [TestCase(SdkLanguage.DotNet, "src/Customized/Generated/Client.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/Client.csproj", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/AssemblyInfo.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/Properties/AssemblyInfo.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/Properties/ASSEMBLYINFO.CS", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/Shared/SharedAssemblyInfo.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/Properties/AssemblyVersion.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/Properties/Version.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/Version.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/Models/Version.cs", true, false)]
    [TestCase(SdkLanguage.DotNet, "src/Models/ApiVersion.cs", true, false)]
    [TestCase(SdkLanguage.DotNet, "src/.git/Client.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "tests/Client.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/eng/Client.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/obj/Client.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/node_modules/Client.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "tsp-location.yaml", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/.github/Client.cs", false, false)]
    [TestCase(SdkLanguage.DotNet, "src/Generated/project.json", false, false)]
    [TestCase(SdkLanguage.Java, "customization/src/main/java/com/Client.java", true, false)]
    [TestCase(SdkLanguage.Java, "src/main/java/com/Client.java", false, true)]
    [TestCase(SdkLanguage.Java, "customization/src/main/java/pom.xml", false, false)]
    [TestCase(SdkLanguage.Java, "customization/src/main/java/com/package-info.java", false, false)]
    [TestCase(SdkLanguage.Java, "customization/src/main/java/module-info.java", false, false)]
    [TestCase(SdkLanguage.Java, "src/test/java/Client.java", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/client.ts", true, false)]
    [TestCase(SdkLanguage.JavaScript, "src/client.tsx", true, false)]
    [TestCase(SdkLanguage.JavaScript, "src/client.mts", true, false)]
    [TestCase(SdkLanguage.JavaScript, "src/client.cts", true, false)]
    [TestCase(SdkLanguage.JavaScript, "src/client.js", true, false)]
    [TestCase(SdkLanguage.JavaScript, "src/client.jsx", true, false)]
    [TestCase(SdkLanguage.JavaScript, "src/client.mjs", true, false)]
    [TestCase(SdkLanguage.JavaScript, "src/client.cjs", true, false)]
    [TestCase(SdkLanguage.JavaScript, "src/Generated/client.ts", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/NODE_MODULES/client.ts", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/package.json", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/client.ts.map", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/main.tsp", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/version.ts", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/_version.mjs", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/utils/version.ts", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/sdk-version.ts", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/packageMetadata.js", false, false)]
    [TestCase(SdkLanguage.JavaScript, "src/models/version.ts", true, false)]
    [TestCase(SdkLanguage.Python, "azure/service/_patch.py", true, false)]
    [TestCase(SdkLanguage.Python, "azure/service/aio/_patch.py", true, false)]
    [TestCase(SdkLanguage.Python, "azure/service/_client.py", false, false)]
    [TestCase(SdkLanguage.Python, "azure/service/_version.py", false, false)]
    [TestCase(SdkLanguage.Python, "azure/service/_generated/_patch.py", false, false)]
    [TestCase(SdkLanguage.Python, "tests/_patch.py", false, false)]
    [TestCase(SdkLanguage.Go, "src/client.go", false, false)]
    public void ClassifiesOnlyLanguageSource(SdkLanguage language, string relativePath, bool custom, bool generated)
    {
        var policy = new CustomizationFilePolicy(language, _root);
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.Multiple(() =>
        {
            Assert.That(policy.IsCustomFile(path), Is.EqualTo(custom));
            Assert.That(policy.IsGeneratedFile(path), Is.EqualTo(generated));
        });
    }

    [TestCase("../outside/src/Client.cs")]
    [TestCase("../repair-policy-sibling/src/Client.cs")]
    [TestCase("src/Client.cs:stream")]
    [TestCase("src/*.cs")]
    [TestCase("src/Client?.cs")]
    [TestCase("src/Client.cs ")]
    [TestCase("src/Client.cs.")]
    [TestCase("src/Client\0.cs")]
    public void RejectsEscapesAndMalformedPaths(string path)
    {
        var policy = new CustomizationFilePolicy(SdkLanguage.DotNet, _root);
        Assert.That(policy.IsCustomFile(Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar))), Is.False);
    }

    [Test]
    public void RejectsRelativeAndPackageRootPaths()
    {
        var policy = new CustomizationFilePolicy(SdkLanguage.DotNet, _root);
        Assert.Multiple(() =>
        {
            Assert.That(policy.IsCustomFile("src/Client.cs"), Is.False);
            Assert.That(policy.IsCustomFile(_root), Is.False);
            Assert.That(policy.IsCustomFile(null!), Is.False);
            Assert.That(policy.IsGeneratedFile(_root), Is.False);
        });
    }

    [TestCase("__all__ = []", false)]
    [TestCase("__all__: List[str] = []", false)]
    [TestCase("# template without export", false)]
    [TestCase("__all__ = ['Client']", true)]
    [TestCase("__all__ = [\n    'Client',\n]", true)]
    public void PythonDiscoveryPreservesActualCustomizationExportCheck(string contents, bool expected)
    {
        var path = WriteFile("azure/service/_patch.py", contents);
        var policy = new CustomizationFilePolicy(SdkLanguage.Python, _root);
        Assert.That(policy.GetCustomizationFiles(), expected ? Is.EqualTo(new[] { path }) : Is.Empty);
    }

    [Test]
    public void DiscoveryOmitsGeneratedInfrastructureAndMetadata()
    {
        var expected = WriteFile("src/Customized/Client.cs", "custom");
        WriteFile("src/Generated/Client.cs", "generated");
        WriteFile("src/obj/Client.cs", "infrastructure");
        WriteFile("src/Client.csproj", "metadata");
        WriteFile("src/Properties/AssemblyInfo.cs", "[assembly: System.Reflection.AssemblyVersion(\"1.0.0\")]");
        WriteFile("src/Constants.cs", "internal const string PackageVersion = \"1.0.0\";");
        var policy = new CustomizationFilePolicy(SdkLanguage.DotNet, _root);
        Assert.That(policy.GetCustomizationFiles(), Is.EqualTo(new[] { expected }));
    }

    [TestCase(SdkLanguage.DotNet, "src/Client.cs", "[assembly: System.Reflection.AssemblyVersion(\"1.0.0\")]")]
    [TestCase(SdkLanguage.DotNet, "src/Client.cs", "[ assembly : System.Runtime.CompilerServices.InternalsVisibleTo(\"Other\")]")]
    [TestCase(SdkLanguage.DotNet, "src/Client.cs", "internal const string PackageVersion = \"1.0.0\";")]
    [TestCase(SdkLanguage.JavaScript, "src/constants.ts", "export const SDK_VERSION = \"1.0.0\";")]
    [TestCase(SdkLanguage.JavaScript, "src/constants.ts", "export const SDK_VERSION: string = \"1.0.0\";")]
    [TestCase(SdkLanguage.JavaScript, "src/utils/constants.js", "export const packageVersion = \"1.0.0\";")]
    [TestCase(SdkLanguage.Python, "azure/service/_patch.py", "__all__ = ['Client']\n__version__ = '1.0.0'")]
    public void ExistingMetadataDeclarationsAreReadOnlyRegardlessOfFilename(SdkLanguage language, string relativePath, string content)
    {
        var path = WriteFile(relativePath, content);
        var policy = new CustomizationFilePolicy(language, _root);
        Assert.That(policy.IsCustomFile(path), Is.False);
        Assert.That(policy.GetCustomizationFiles(), Is.Empty);
        Assert.That(File.ReadAllText(path), Is.EqualTo(content));
    }

    [Test]
    public void OrdinaryServiceApiVersionConstantIsNotPackageVersionMetadata()
    {
        var path = WriteFile("src/constants.ts", "export const API_VERSION = '2026-01-01';");
        var policy = new CustomizationFilePolicy(SdkLanguage.JavaScript, _root);
        Assert.That(policy.IsCustomFile(path), Is.True);
        Assert.That(policy.GetCustomizationFiles(), Is.EqualTo(new[] { path }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RejectsLinkedFilesAndDirectoriesEvenWithinPackage(bool directoryLink)
    {
        var target = WriteFile("src/real/Client.cs", "custom");
        var link = Path.Combine(_root, "src", directoryLink ? "linked" : "Linked.cs");
        try
        {
            if (directoryLink)
            {
                Directory.CreateSymbolicLink(link, Path.GetDirectoryName(target)!);
            }
            else
            {
                File.CreateSymbolicLink(link, target);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Ignore($"This host cannot create symbolic links: {ex.Message}");
        }

        var candidate = directoryLink ? Path.Combine(link, "Client.cs") : link;
        var policy = new CustomizationFilePolicy(SdkLanguage.DotNet, _root);
        Assert.That(policy.IsCustomFile(candidate), Is.False);
        Assert.That(policy.GetCustomizationFiles(), Is.EqualTo(new[] { target }));
    }

    private string WriteFile(string relativePath, string contents)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    [Test]
    public void RejectsDirectoryLinksEscapingPackageAndNonexistentRenameDestinationsThroughLinks()
    {
        var package = Path.Combine(_root, "package");
        Directory.CreateDirectory(Path.Combine(package, "src"));
        var target = WriteFile("outside/Client.cs", "custom");
        var link = Path.Combine(package, "src", "linked");
        try
        {
            Directory.CreateSymbolicLink(link, Path.GetDirectoryName(target)!);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Ignore($"This host cannot create symbolic links: {ex.Message}");
        }
        var policy = new CustomizationFilePolicy(SdkLanguage.DotNet, package);
        Assert.Multiple(() =>
        {
            Assert.That(policy.IsCustomFile(Path.Combine(link, "Client.cs")), Is.False);
            Assert.That(policy.IsCustomFile(Path.Combine(link, "New.cs")), Is.False);
            Assert.That(policy.GetCustomizationFiles(), Is.Empty);
        });
    }
}
