using System.Reflection;
using System.Runtime.Loader;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tests.ProgramTests;

[NonParallelizable]
internal class ProgramTests
{
    private const string CodeownersAssemblyName = "Azure.Sdk.Tools.CodeownersUtils";

    [Test]
    public async Task CliStartup_LoadsCodeownersOnlyWhenUsed()
    {
        var context = new CliLoadContext();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        try
        {
            var program = context.LoadFromAssemblyPath(typeof(Program).Assembly.Location)
                .GetType(typeof(Program).FullName!)!;
            var main = program.GetMethod(nameof(Program.Main))!;

            async Task<int> Run(string[] args)
            {
                try
                {
                    return await (Task<int>)main.Invoke(null, [args])!;
                }
                finally
                {
                    if (program.GetProperty(nameof(Program.ServerApp))!.GetValue(null) is IAsyncDisposable app)
                    {
                        await app.DisposeAsync();
                    }
                }
            }

            var result = await Run(["--help"]);
            Assert.That(result, Is.Zero);
            Assert.That(context.Assemblies.Select(a => a.GetName().Name), Does.Not.Contain(CodeownersAssemblyName));

            using var tempDir = TempDirectory.Create("codeowners-lazy-loading");
            var source = Path.Combine(tempDir.DirectoryPath, "CODEOWNERS");
            var output = Path.Combine(tempDir.DirectoryPath, "exported");
            await File.WriteAllTextAsync(source, "####################\n# Client Libraries\n####################\n/sdk/test/ @owner\n");

            result = await Run([
                "config", "codeowners", "export-section",
                "--codeowners-path", source, "--section", "Client Libraries", "--output-file", output
            ]);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Zero);
                Assert.That(context.Assemblies.Select(a => a.GetName().Name), Does.Contain(CodeownersAssemblyName));
                Assert.That(File.ReadAllText(output), Does.Contain("/sdk/test/ @owner"));
            });
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            context.Unload();
        }
    }

    [Test]
    public async Task McpToolDiscovery_DoesNotLoadCodeowners()
    {
        var context = new CliLoadContext();
        try
        {
            var cli = context.LoadFromAssemblyPath(typeof(Program).Assembly.Location);
            var createBuilder = cli.GetType(typeof(Program).FullName!)!
                .GetMethod(nameof(Program.CreateAppBuilder))!;
            var logLevel = Enum.ToObject(createBuilder.GetParameters()[2].ParameterType, (int)LogLevel.None);
            var builder = createBuilder.Invoke(null, [new[] { "start" }, "hidden", logLevel, false])!;
            var app = builder.GetType().GetMethod("Build")!.Invoke(builder, null)!;
            await using var disposableApp = (IAsyncDisposable)app;
            var services = (IServiceProvider)app.GetType().GetProperty("Services")!.GetValue(app)!;
            var tools = services.GetServices<McpServerTool>().ToList();
            Assert.That(tools.Any(t => t.ProtocolTool.Name == "azsdk_engsys_codeowner_view"), Is.True);
            Assert.That(context.Assemblies.Select(a => a.GetName().Name), Does.Not.Contain(CodeownersAssemblyName));

            foreach (var helper in new[]
            {
                "ICodeownersGenerateHelper", "ICodeownersManagementHelper",
                "ICheckPackageHelper", "ICodeownersAuditHelper"
            })
            {
                var helperType = cli.GetType($"Azure.Sdk.Tools.Cli.Helpers.Codeowners.{helper}")!;
                var lazyType = typeof(Lazy<>).MakeGenericType(helperType);
                var lazy = services.GetService(lazyType)!;
                Assert.That(lazyType.GetProperty("IsValueCreated")!.GetValue(lazy), Is.False);
                var value = lazyType.GetProperty("Value")!.GetValue(lazy);
                Assert.That(value, Is.Not.Null);
                Assert.That(value, Is.SameAs(services.GetService(helperType)));
            }
            Assert.That(context.Assemblies.Select(a => a.GetName().Name), Does.Contain(CodeownersAssemblyName));
        }
        finally
        {
            context.Unload();
        }
    }

    private sealed class CliLoadContext() : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(typeof(Program).Assembly.Location);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name != typeof(Program).Assembly.GetName().Name &&
                assemblyName.Name != CodeownersAssemblyName)
            {
                return null;
            }
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path == null ? null : LoadFromAssemblyPath(path);
        }
    }

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
