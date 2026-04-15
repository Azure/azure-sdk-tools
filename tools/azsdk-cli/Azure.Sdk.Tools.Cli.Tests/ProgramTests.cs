using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tests.ProgramTests;

internal class ProgramTests
{
    [Test]
    public async Task TestMain()
    {
        // Force app builder to build and verify things like DI lifetimes
        Assert.That(() => Program.CreateAppBuilder(["--help"], "hidden", LogLevel.None).Build(), Throws.Nothing);

        // Suppress console output from the end-to-end CLI invocation
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        try
        {
            // Run full builder setup and CLI parsing end to end
            var result = await Program.Run(["-o", "hidden", "example", "hello-world", "test"], LogLevel.None);
            Assert.That(result, Is.EqualTo(0));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Test]
    public void TestIsCommandLine()
    {
        Assert.That(Program.IsCommandLine(["--help"]), Is.True);
        Assert.That(Program.IsCommandLine(["example", "hello-world", "test"]), Is.True);
        Assert.That(Program.IsCommandLine(["start"]), Is.False);
        Assert.That(Program.IsCommandLine(["mcp"]), Is.False);
        Assert.That(Program.IsCommandLine(["start", "--help"]), Is.False);
        Assert.That(Program.IsCommandLine(["mcp", "--help"]), Is.False);
    }
}
