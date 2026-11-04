// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Tests.Prompts.Templates;

[TestFixture]
public class TypeSpecCustomizationTemplateTests
{
    [TestCase("Read the complete existing client.tsp file before making any changes")]
    [TestCase("Apply only the changes required by the customization request")]
    [TestCase("Preserve all other existing customization code and content unchanged")]
    [TestCase("do not truncate client.tsp or omit unrelated content when using WriteFile")]
    public void BuildPrompt_ContainsContentPreservationRequirements(string requirement)
    {
        var template = new TypeSpecCustomizationTemplate(
            "Rename the client", "/specification/widget", "Reference documentation");

        var prompt = template.BuildPrompt();

        Assert.That(prompt, Does.Contain(requirement));
    }
}
