// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class DotnetBreakingChangeClassificationTests
{
    private Mock<ICopilotAgentRunner> _agentRunner = null!;
    private SdkBreakingChangeClassificationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _agentRunner = new Mock<ICopilotAgentRunner>();
        _service = new SdkBreakingChangeClassificationService(
            _agentRunner.Object, new TestLogger<SdkBreakingChangeClassificationService>());
    }

    [TestCase("generator", SdkBreakingChangeMitigationStrategy.Generator)]
    [TestCase("client customization", SdkBreakingChangeMitigationStrategy.ClientCustomization)]
    [TestCase("manual", SdkBreakingChangeMitigationStrategy.Manual)]
    public async Task Classify_PreservesSupportedMitigationRoute(string route, SdkBreakingChangeMitigationStrategy expected)
    {
        ConfigureResponse($$"""
            {
                "hasBreakingChange": true,
                "breakingChanges": [{
                    "breakingChange": "Widget.Name changed",
                    "category": "emitter change",
                    "resolution": "Follow the verified pattern and request user selection.",
                    "mitigationStrategy": "{{route}}",
                    "originBreaks": ["CP0002: Widget.Name was removed"]
                }]
            }
            """);

        var result = await _service.ClassifySdkBreakingChangesAsync(
            "### Breaking Changes\n- CP0002: Widget.Name was removed", "Verified patterns", "DotNet", null, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BreakingChanges.Single().MitigationStrategy, Is.EqualTo(expected));
        Assert.That(result.BreakingChanges.Single().OriginBreaks, Is.EqualTo(new[] { "CP0002: Widget.Name was removed" }));
        Assert.That(JsonSerializer.Serialize(result), Does.Contain($"\"mitigationStrategy\":\"{route}\""));
    }

    [TestCase(", \"mitigationStrategy\": \"suppress\"")]
    [TestCase(", \"mitigationStrategy\": 999")]
    public async Task Classify_RejectsUnparseableRoute(string mitigationProperty)
    {
        ConfigureResponse($$"""
            {
                "hasBreakingChange": true,
                "breakingChanges": [{
                    "breakingChange": "Widget removed",
                    "category": "unknown"
                    {{mitigationProperty}}
                }]
            }
            """);

        var result = await _service.ClassifySdkBreakingChangesAsync(
            "### Breaking Changes\n- Widget removed", "Patterns", "DotNet", null, CancellationToken.None);

        Assert.That(result, Is.Null, "Invalid routing must not authorize an automatic mitigation.");
    }

    [TestCase("")]
    [TestCase(", \"mitigationStrategy\": null")]
    [TestCase(", \"mitigationStrategy\": \"999\"")]
    public async Task Classify_LeavesLanguageSpecificValidationToLanguageService(string mitigationProperty)
    {
        ConfigureResponse($$"""
            {"hasBreakingChange":true,"breakingChanges":[
                {"breakingChange":"Widget removed","category":"unknown"{{mitigationProperty}}}
            ]}
            """);

        var result = await _service.ClassifySdkBreakingChangesAsync("Changes", "Patterns", "DotNet", null, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        var language = Languages.DotnetLanguageServiceBreakingChangeTests.CreateService(
            Mock.Of<Azure.Sdk.Tools.Cli.Helpers.ISpecGenSdkConfigHelper>());
        Assert.That(language.ValidateBreakingChangeClassification(result!), Is.Not.Null);
    }

    [TestCase("Go")]
    [TestCase("Java")]
    [TestCase("Python")]
    [TestCase("JavaScript")]
    public async Task Classify_OtherLanguagesRetainExistingContract(string language)
    {
        ConfigureResponse("""
            {
                "hasBreakingChange": true,
                "breakingChanges": [{
                    "breakingChange": "Widget removed",
                    "category": "unknown",
                    "originBreaks": ["Widget removed"]
                }]
            }
            """);

        var result = await _service.ClassifySdkBreakingChangesAsync(
            "### Breaking Changes\n- Widget removed", "Patterns", language, null, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BreakingChanges.Single().MitigationStrategy, Is.Null);
        Assert.That(JsonSerializer.Serialize(result), Does.Not.Contain("\"mitigationStrategy\""));
    }

    [TestCase(".NET")]
    [TestCase("DotNet")]
    [TestCase("csharp")]
    [TestCase("c#")]
    public void DotnetPrompt_RequiresConservativeEvidenceBasedRouting(string language)
    {
        var prompt = new SdkBreakingChangeClassificationTemplate(
            "Pattern catalog", "Original ApiCompat diagnostics", language, null).BuildPrompt();

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("ApiCompat's forward comparison"));
            Assert.That(prompt, Does.Contain("not proof of a rename"));
            Assert.That(prompt, Does.Not.Contain("treat the combined evidence as a likely model rename"));
            Assert.That(prompt, Does.Contain("reverse-comparison diagnostics are supplementary evidence"));
            Assert.That(prompt, Does.Not.Contain("mitigate-breaking-changes skill"));
            Assert.That(prompt, Does.Not.Contain("azsdk_customized_code_update"));
            Assert.That(prompt, Does.Contain("Select the mitigation strategy and resolution from the matched SDK pattern catalog"));
            Assert.That(prompt, Does.Contain("A rename can be classified when"));
            Assert.That(prompt, Does.Contain("Category describes the root cause, independently of rename confidence"));
            Assert.That(prompt, Does.Not.Contain("otherwise preserve the original violations with category unknown"));
            Assert.That(prompt, Does.Contain("Use \"manual\" for ambiguous mappings"));
            Assert.That(prompt, Does.Contain("Never apply fixes, edit generated code, add suppressions"));
            Assert.That(prompt, Does.Contain("Original ApiCompat diagnostics"));
            Assert.That(prompt, Does.Contain("Pattern catalog"));
        });
    }

    [Test]
    public void OtherLanguagePrompt_DoesNotRequireDotnetMitigation()
    {
        var prompt = new SdkBreakingChangeClassificationTemplate("Patterns", "Changes", "Go", null).BuildPrompt();

        Assert.That(prompt, Does.Not.Contain(".NET compatibility and mitigation"));
        Assert.That(prompt, Does.Not.Contain("mitigate-breaking-changes skill"));
        Assert.That(prompt, Does.Contain("treat the combined evidence as a likely model rename"));
    }

    [TestCase("Go")]
    [TestCase("DotNet")]
    public void Prompt_ExplainsValidFieldValuesOutsideTheJsonExample(string language)
    {
        var prompt = new SdkBreakingChangeClassificationTemplate("Patterns", "Changes", language, null).BuildPrompt();

        Assert.That(prompt, Does.Contain("hasBreakingChange is a Boolean: true when breaking changes exist, otherwise false"));
        Assert.That(prompt, Does.Contain("breakingChanges is an array"));
        Assert.That(prompt, Does.Contain("category explains why the change occurred"));
        Assert.That(prompt, Does.Contain("\"emitter change\", \"conversion-by design\", \"conversion-need resolve\", \"spec change\", or \"unknown\""));
        Assert.That(prompt, Does.Contain("mitigationStrategy is separate from category"));
    }

    [TestCase("emitter change", "manual", SdkBreakingChangeCategory.EmitterChange)]
    [TestCase("spec change", "client customization", SdkBreakingChangeCategory.SpecChange)]
    public async Task Classify_PreservesRootCauseIndependentlyOfStrategy(string category, string strategy, SdkBreakingChangeCategory expected)
    {
        ConfigureResponse($$"""
            {"hasBreakingChange":true,"breakingChanges":[{
                "breakingChange":"Widget removed; mapping is not verified",
                "category":"{{category}}","mitigationStrategy":"{{strategy}}",
                "resolution":"Catalog guidance","originBreaks":["Widget removed"]
            }]}
            """);

        var result = await _service.ClassifySdkBreakingChangesAsync("Changes", "Patterns", "DotNet", null, CancellationToken.None);

        Assert.That(result!.BreakingChanges.Single().Category, Is.EqualTo(expected));
        Assert.That(result.BreakingChanges.Single().Resolution, Is.EqualTo("Catalog guidance"));
        var json = JsonSerializer.SerializeToElement(result.BreakingChanges.Single());
        Assert.That(json.GetProperty("mitigationStrategy").GetString(), Is.EqualTo(strategy));
        Assert.That(json.TryGetProperty("mitigation", out _), Is.False);
    }

    [TestCase(".NET", true)]
    [TestCase("DotNet", true)]
    [TestCase("csharp", true)]
    [TestCase("c#", true)]
    [TestCase("Go", false)]
    [TestCase("Java", false)]
    [TestCase("Python", false)]
    [TestCase("JavaScript", false)]
    public void Prompt_ExactOutputExampleIsValidJsonAndMatchesLanguageValidation(string language, bool requiresRoute)
    {
        var prompt = new SdkBreakingChangeClassificationTemplate("Patterns", "Changes", language, null).BuildPrompt();
        var start = prompt.IndexOf('{', prompt.IndexOf("following this exact format:", StringComparison.Ordinal));
        var end = prompt.IndexOf("Output must be raw JSON only.", start, StringComparison.Ordinal);
        var example = prompt[start..end].Trim();

        using var document = JsonDocument.Parse(example);
        Assert.That(document.RootElement.GetProperty("breakingChanges")[0].TryGetProperty("mitigationStrategy", out _),
            Is.EqualTo(requiresRoute));
        var classification = JsonSerializer.Deserialize<SdkBreakingChangeDetectionResult>(example)!;
        var validator = requiresRoute
            ? Languages.DotnetLanguageServiceBreakingChangeTests.CreateService(Mock.Of<Azure.Sdk.Tools.Cli.Helpers.ISpecGenSdkConfigHelper>())
            : (Azure.Sdk.Tools.Cli.Services.Languages.LanguageService)new Mock<Azure.Sdk.Tools.Cli.Services.Languages.LanguageService> { CallBase = true }.Object;
        Assert.That(validator.ValidateBreakingChangeClassification(classification), Is.Null);
    }

    [Test]
    public async Task Classify_UsesReadOnlyAgentAndPassesOriginalEvidence()
    {
        ConfigureResponse("{\"hasBreakingChange\":false,\"breakingChanges\":[]}");

        await _service.ClassifySdkBreakingChangesAsync(
            "Original compatibility output", "Original pattern catalog", "DotNet", null, CancellationToken.None);

        _agentRunner.Verify(r => r.RunAsync(It.Is<CopilotAgent<string>>(agent =>
            agent.Instructions.Contains("Original compatibility output") &&
            agent.Instructions.Contains("Original pattern catalog") &&
            !agent.Tools.Any()), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Classify_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _agentRunner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Assert.ThrowsAsync<OperationCanceledException>(() => _service.ClassifySdkBreakingChangesAsync(
            "Changes", "Patterns", "DotNet", null, cts.Token));
    }

    private void ConfigureResponse(string response)
    {
        _agentRunner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }
}
