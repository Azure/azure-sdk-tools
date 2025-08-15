// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

internal class ProcessHelperTests
{
    private ProcessHelper processHelper = null!;
    private Mock<ILogger<ProcessHelper>> mockLogger = null!;

    [SetUp]
    public void Setup()
    {
        mockLogger = new Mock<ILogger<ProcessHelper>>();
        processHelper = new ProcessHelper(mockLogger.Object);
    }

    [Test]
    public void RunProcessAsync_TimesOut()
    {
        var process = processHelper.CreateForCrossPlatform("sleep", ["1"], "timeout", ["/T", "1", "/NOBREAK"], Environment.CurrentDirectory);
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await process.RunProcess(TimeSpan.FromMilliseconds(1), CancellationToken.None);
        });
    }
}
