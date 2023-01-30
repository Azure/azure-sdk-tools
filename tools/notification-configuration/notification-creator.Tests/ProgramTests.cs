using System;
using NUnit.Framework;

namespace Azure.Sdk.Tools.NotificationConfiguration.Tests;

[TestFixture]
public class ProgramTests
{
    [Test]
    public void ThrowsVssUnauthorizedException()
    {
        Environment.SetEnvironmentVariable("aadAppIdVar", "aadAppIdVarValue");
        Environment.SetEnvironmentVariable("aadAppSecretVar", "aadAppSecretVarValue");
        Environment.SetEnvironmentVariable("aadTenantVar", "aadTenantVarValue");
        Assert.ThrowsAsync<Microsoft.VisualStudio.Services.Common.VssUnauthorizedException>(
            async () =>
                // Act
                await Program.Main(
                    organization: "fooOrg",
                    project: "barProj",
                    pathPrefix: "qux",
                    tokenVariableName: "token",
                    aadAppIdVar: "aadAppIdVar",
                    aadAppSecretVar: "aadAppSecretVar",
                    aadTenantVar: "aadTenantVar",
                    selectionStrategy: PipelineSelectionStrategy.Scheduled,
                    dryRun: true)
                );
    }
}
