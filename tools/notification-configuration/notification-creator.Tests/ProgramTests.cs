using System;
using NUnit.Framework;

namespace Azure.Sdk.Tools.NotificationConfiguration.Tests;

[TestFixture]
public class ProgramTests
{
    [Test]
    public void ThrowsVssUnauthorizedException()
    {
        Assert.ThrowsAsync<Microsoft.VisualStudio.Services.Common.VssUnauthorizedException>(
            async () =>
                // Act
                await Program.Main(
                    organization: "fooOrg",
                    project: "barProj",
                    pathPrefix: "qux",
                    selectionStrategy: PipelineSelectionStrategy.Scheduled,
                    dryRun: true)
                );
    }
}
