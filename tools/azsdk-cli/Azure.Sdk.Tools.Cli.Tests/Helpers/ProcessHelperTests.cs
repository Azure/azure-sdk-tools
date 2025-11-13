// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

internal class ProcessHelperTests
{
    private ProcessHelper processHelper = null!;
    private Mock<ILogger<ProcessHelper>> mockLogger = null!;
    private Mock<IRawOutputHelper> mockOutputHelper = null!;

    [SetUp]
    public void Setup()
    {
        mockLogger = new Mock<ILogger<ProcessHelper>>();
        mockOutputHelper = new Mock<IRawOutputHelper>();
        processHelper = new ProcessHelper(mockLogger.Object, mockOutputHelper.Object);
    }

    [Test]
    public void RunProcessAsync_TimesOut()
    {
        var options = new ProcessOptions(
            "sleep", ["1"],
            "timeout", ["/t", "1", "/NOBREAK"],
            timeout: TimeSpan.FromMilliseconds(1)
        );
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await processHelper.Run(options, CancellationToken.None);
        });
    }
}
