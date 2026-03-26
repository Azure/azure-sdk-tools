// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Tests.Prompts.Templates;

[TestFixture]
public class DotnetErrorDrivenPatchTemplateTests
{
    private const string SampleBuildContext = """
        error CS0117: 'WidgetClient' does not contain a definition for 'GetWidgetAsync'
        error CS0246: The type or namespace name 'WidgetOptions' could not be found
        """;

    private const string SamplePackagePath = "/sdk/widget/Azure.Widget";
    private const string SampleCustomizationRoot = "/sdk/widget/Azure.Widget/src";

    private static readonly List<string> SampleCustomizationFiles =
    [
        "src/WidgetClientExtensions.cs",
        "src/Customized/WidgetOptionsHelper.cs"
    ];

    private static readonly List<string> SamplePatchFilePaths =
    [
        "WidgetClientExtensions.cs",
        "Customized/WidgetOptionsHelper.cs"
    ];

    private DotnetErrorDrivenPatchTemplate CreateTemplate() =>
        new(SampleBuildContext, SamplePackagePath, SampleCustomizationRoot,
            SampleCustomizationFiles, SamplePatchFilePaths);

    [Test]
    public void TemplateId_IsDotnetErrorDrivenPatch()
    {
        var template = CreateTemplate();
        Assert.That(template.TemplateId, Is.EqualTo("dotnet-error-driven-patch"));
    }

    [Test]
    public void Version_Is1_0_0()
    {
        var template = CreateTemplate();
        Assert.That(template.Version, Is.EqualTo("1.0.0"));
    }

    [Test]
    public void Description_IsNotEmpty()
    {
        var template = CreateTemplate();
        Assert.That(template.Description, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void BuildPrompt_ContainsBuildContext()
    {
        var prompt = CreateTemplate().BuildPrompt();
        Assert.That(prompt, Does.Contain("CS0117"));
        Assert.That(prompt, Does.Contain("GetWidgetAsync"));
    }

    [Test]
    public void BuildPrompt_ContainsCustomizationFiles()
    {
        var prompt = CreateTemplate().BuildPrompt();
        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("WidgetClientExtensions.cs"));
            Assert.That(prompt, Does.Contain("WidgetOptionsHelper.cs"));
        });
    }

    [Test]
    public void BuildPrompt_ContainsPackagePath()
    {
        var prompt = CreateTemplate().BuildPrompt();
        Assert.That(prompt, Does.Contain(SamplePackagePath));
    }

    [Test]
    public void BuildPrompt_ContainsCustomizationRoot()
    {
        var prompt = CreateTemplate().BuildPrompt();
        Assert.That(prompt, Does.Contain(SampleCustomizationRoot));
    }

    [Test]
    public void BuildPrompt_ContainsDotnetSpecificGuidance()
    {
        var prompt = CreateTemplate().BuildPrompt();
        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("partial class").IgnoreCase);
            Assert.That(prompt, Does.Contain("Generated/"));
            Assert.That(prompt, Does.Contain(".cs"));
            Assert.That(prompt, Does.Contain(".NET"));
        });
    }

    [Test]
    public void BuildPrompt_ContainsToolDescriptions()
    {
        var prompt = CreateTemplate().BuildPrompt();
        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("GrepSearch"));
            Assert.That(prompt, Does.Contain("ReadFile"));
            Assert.That(prompt, Does.Contain("CodePatchTool"));
        });
    }

    [Test]
    public void BuildPrompt_ContainsSafetyConstraints()
    {
        var prompt = CreateTemplate().BuildPrompt();
        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("CUSTOMIZATION FILES ONLY"));
            Assert.That(prompt, Does.Contain("SAFE PATCHES ONLY"));
            Assert.That(prompt, Does.Contain("SURGICAL PATCHING"));
            Assert.That(prompt, Does.Contain("NO DUPLICATE PATCHES"));
        });
    }

    [Test]
    public void BuildPrompt_ContainsWorkflowSteps()
    {
        var prompt = CreateTemplate().BuildPrompt();
        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Step 1"));
            Assert.That(prompt, Does.Contain("Step 2"));
            Assert.That(prompt, Does.Contain("Step 3"));
            Assert.That(prompt, Does.Contain("Step 4"));
        });
    }

    [Test]
    public void BuildPrompt_ContainsReadFilePaths()
    {
        var prompt = CreateTemplate().BuildPrompt();
        foreach (var file in SampleCustomizationFiles)
        {
            Assert.That(prompt, Does.Contain(file),
                $"Expected prompt to contain ReadFile path: {file}");
        }
    }

    [Test]
    public void BuildPrompt_ContainsPatchFilePaths()
    {
        var prompt = CreateTemplate().BuildPrompt();
        foreach (var file in SamplePatchFilePaths)
        {
            Assert.That(prompt, Does.Contain(file),
                $"Expected prompt to contain CodePatchTool path: {file}");
        }
    }
}
