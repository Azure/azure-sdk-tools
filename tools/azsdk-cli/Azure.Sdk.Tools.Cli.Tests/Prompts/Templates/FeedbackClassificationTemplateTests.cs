// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Tests.Prompts.Templates;

[TestFixture]
public class FeedbackClassificationTemplateTests
{
    private static List<FeedbackItem> SampleItems() =>
    [
        new FeedbackItem { Text = "Rename FooClient to BarClient" }
    ];

    private static FeedbackClassificationTemplate CreateTemplate(EditScope editScope, bool specInspectionAvailable = true) =>
        new(
            serviceName: "Widget",
            language: "csharp",
            referenceDocContent: "reference",
            items: SampleItems(),
            globalContext: "ctx",
            editScope: editScope,
            specInspectionAvailable: specInspectionAvailable);

    [Test]
    public void AllScope_EmitsNoEditScopeBias()
    {
        var prompt = CreateTemplate(EditScope.All).BuildPrompt();

        Assert.That(prompt, Does.Not.Contain("EDIT SCOPE — CUSTOM CODE ONLY"));
        Assert.That(prompt, Does.Not.Contain("EDIT SCOPE — SPEC INPUTS ONLY"));
    }

    [Test]
    public void CustomCodeScope_BiasesTowardCodeCustomization()
    {
        var prompt = CreateTemplate(EditScope.CustomCode).BuildPrompt();

        // Spec inputs out of scope: classifier is told to prefer CODE_CUSTOMIZATION for ambiguous items.
        Assert.That(prompt, Does.Contain("EDIT SCOPE — CUSTOM CODE ONLY"));
        Assert.That(prompt, Does.Contain("classify it as **CODE_CUSTOMIZATION**"));
        Assert.That(prompt, Does.Not.Contain("EDIT SCOPE — SPEC INPUTS ONLY"));
    }

    [Test]
    public void SpecInputsScope_BiasesTowardTspApplicable()
    {
        var prompt = CreateTemplate(EditScope.SpecInputs).BuildPrompt();

        // Custom code out of scope: classifier is told to prefer TSP_APPLICABLE for ambiguous items.
        Assert.That(prompt, Does.Contain("EDIT SCOPE — SPEC INPUTS ONLY"));
        Assert.That(prompt, Does.Contain("classify it as **TSP_APPLICABLE**"));
        Assert.That(prompt, Does.Not.Contain("EDIT SCOPE — CUSTOM CODE ONLY"));
    }

    [Test]
    public void CustomCodeScopeWithoutSpecInspection_DoesNotAdvertiseSpecTools()
    {
        var prompt = CreateTemplate(EditScope.CustomCode, specInspectionAvailable: false).BuildPrompt();

        Assert.That(prompt, Does.Contain("TypeSpec inspection unavailable"));
        Assert.That(prompt, Does.Not.Contain("**Available Tools:**"));
        Assert.That(prompt, Does.Not.Contain("Always search the TypeSpec files first"));
    }
}
