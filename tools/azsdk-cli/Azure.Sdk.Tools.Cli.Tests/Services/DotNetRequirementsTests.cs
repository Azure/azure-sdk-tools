// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.SetupRequirements;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class DotNetRequirementsTests
{
    [Test]
    public void SdkRequirement_UsesDotNetDisplayNameAndUnchangedCommand()
    {
        var requirement = DotNetRequirements.All.Single();

        Assert.That(requirement, Is.TypeOf<DotNetRequirements.DotNetSdkRequirement>());
        Assert.That(requirement.Name, Is.EqualTo(".NET SDK"));
        Assert.That(requirement.CheckCommand, Is.EqualTo(new[] { "dotnet", "--version" }));
        Assert.That(requirement.IsAutoInstallable, Is.False);
        Assert.That(requirement.NotAutoInstallableReason, Is.EqualTo(NotInstallableReasons.LanguageRuntime));
    }

    [TestCase(SdkLanguage.DotNet, true)]
    [TestCase(SdkLanguage.Java, false)]
    [TestCase(SdkLanguage.Python, false)]
    [TestCase(SdkLanguage.Unknown, false)]
    public void SdkRequirement_OnlyChecksDotNetContexts(SdkLanguage language, bool expected)
    {
        var context = new RequirementContext
        {
            RepoRoot = "repo",
            RepoName = "azure-sdk-for-net",
            Languages = new HashSet<SdkLanguage> { language },
        };

        Assert.That(DotNetRequirements.All.Single().ShouldCheck(context), Is.EqualTo(expected));
    }
}
