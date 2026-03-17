// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages;

/// <summary>
/// Integration tests for PythonLanguageService.BuildAsync and ApplyPatchesAsync.
///
/// Tests run against a minimal local Python package created in a temp directory — no network
/// access required. Tests are skipped automatically when GitHub Copilot CLI is not
/// available/authenticated.
///
/// Running live tests requires:
///   - GitHub Copilot CLI installed and authenticated
/// </summary>
[TestFixture]
[Category("Integration")]
public class PythonLanguageServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a PythonLanguageService wired with real helpers.
    /// Pass a real <see cref="ICopilotAgentRunner"/> for tests that need live Copilot,
    /// or use <c>Mock.Of&lt;ICopilotAgentRunner&gt;()</c> for tests that don't invoke it.
    /// </summary>
    internal static PythonLanguageService CreateLanguageService(ICopilotAgentRunner copilotAgentRunner)
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var rawOutput = Mock.Of<IRawOutputHelper>();

        var processHelper = new ProcessHelper(loggerFactory.CreateLogger<ProcessHelper>(), rawOutput);
        var pythonHelper = new PythonHelper(loggerFactory.CreateLogger<PythonHelper>(), rawOutput);
        var npxHelper = new NpxHelper(loggerFactory.CreateLogger<NpxHelper>(), rawOutput);
        var gitCommandHelper = new GitCommandHelper(loggerFactory.CreateLogger<GitCommandHelper>(), rawOutput);

        var gitHelper = new GitHelper(
            Mock.Of<IGitHubService>(),
            gitCommandHelper,
            loggerFactory.CreateLogger<GitHelper>());

        var packageInfoHelper = new PackageInfoHelper(
            loggerFactory.CreateLogger<PackageInfoHelper>(),
            gitHelper);

        return new PythonLanguageService(
            processHelper,
            pythonHelper,
            npxHelper,
            copilotAgentRunner,
            gitHelper,
            loggerFactory.CreateLogger<PythonLanguageService>(),
            Mock.Of<ICommonValidationHelpers>(),
            packageInfoHelper,
            Mock.Of<IFileHelper>(),
            Mock.Of<ISpecGenSdkConfigHelper>(),
            Mock.Of<IChangelogHelper>());
    }

    internal static ICopilotAgentRunner CreateRealCopilotRunner()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var rawOutput = Mock.Of<IRawOutputHelper>();

        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            UseStdio = true,
            AutoStart = true
        });
        var wrapper = new CopilotClientWrapper(copilotClient);
        var tokenUsage = new TokenUsageHelper(rawOutput);
        return new CopilotAgentRunner(wrapper, tokenUsage, loggerFactory.CreateLogger<CopilotAgentRunner>());
    }

    private static async Task<bool> IsAzpysdkAvailableAsync()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "azpysdk",
                ArgumentList = { "--help" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Runs <c>git init</c> in the given directory so it is treated as a git repo.</summary>
    internal static async Task RunGitInit(string directory)
    {
        var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            ArgumentList = { "init", directory },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
        await proc.WaitForExitAsync();
    }

    // ── Test ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Three-step live roundtrip using a minimal local Python package:
    ///   1. Verify the clean package lints without errors.
    ///   2. Rename Widget → OldWidget in the class definition only and verify lint fails.
    ///   3. Feed the build errors to ApplyPatchesAsync with a live Copilot runner,
    ///      then verify lint passes again.
    ///
    /// Requires GitHub Copilot CLI installed and authenticated.
    /// </summary>
    [Test]
    public async Task Live_SimplePyFile_LintErrorFixedByCopilot()
    {
        if (!await IsAzpysdkAvailableAsync())
            Assert.Ignore("azpysdk not available or not properly configured — skipping.");
        if (!await CopilotTestHelper.IsCopilotAvailableAsync())
            Assert.Ignore("GitHub Copilot CLI not available or not authenticated.");

        using var tempPkg = TempDirectory.Create("py-lang-svc-lint-test");
        var packagePath = tempPkg.DirectoryPath;

        // Minimal package structure azpysdk can lint without --isolate:
        //   - pyproject.toml: required by LintCode upfront check; `dependencies` and
        //     `requires-python` must both be present to avoid parse errors in ci_tools
        //   - pylintrc at the git root: azpysdk resolves REPO_ROOT via discover_repo_root()
        //     (walks up from cwd to find .git) and then looks for REPO_ROOT/pylintrc;
        //     disable style-only checks so only real errors (e.g. E0602 undefined-variable)
        //     cause a failure
        //   - widget/__init__.py: so discover_namespace() returns "widget" and both
        //     pylint and mypy target the widget/ subdirectory rather than the package root
        //   - widget/_patch.py: the file we mutate in step 2
        await File.WriteAllTextAsync(Path.Combine(packagePath, "pyproject.toml"),
            "[project]\nname = \"widget\"\nversion = \"0.0.1\"\ndependencies = []\nrequires-python = \">=3.8\"\n");
        File.WriteAllText(Path.Combine(packagePath, "pylintrc"),
            "[MESSAGES CONTROL]\ndisable=C0114,C0115,C0116,R0903,C0103\n");

        var widgetDir = Path.Combine(packagePath, "widget");
        Directory.CreateDirectory(widgetDir);
        await File.WriteAllTextAsync(Path.Combine(widgetDir, "__init__.py"), "__version__ = \"0.0.1\"\n");

        var patchFile = Path.Combine(widgetDir, "_patch.py");
        await File.WriteAllTextAsync(patchFile,
            // __all__ must be non-empty so ApplyPatchesAsync's HasNonEmptyAllExport check
            // includes this file; Widget in __all__ also means the undefined-variable error
            // after renaming the class is unambiguous.
            "__all__ = [\"Widget\"]\n\nclass Widget:\n    def value(self) -> int:\n        return 1\n\nresult = Widget().value()\n");

        await RunGitInit(packagePath);

        var service = CreateLanguageService(Mock.Of<ICopilotAgentRunner>());

        // ── Step 1: clean build should pass ──────────────────────────────────
        var (cleanSuccess, cleanError, _) = await service.BuildAsync(packagePath, ct: CancellationToken.None);
        TestContext.WriteLine($"Step 1 — clean build: success={cleanSuccess}, error={cleanError}");
        Assert.That(cleanSuccess, Is.True, $"Expected clean build to pass. Error: {cleanError}");

        // ── Step 2: rename class in definition only → undefined-variable error ──
        var original = await File.ReadAllTextAsync(patchFile);
        await File.WriteAllTextAsync(patchFile, original.Replace("class Widget", "class OldWidget"));

        var (brokenSuccess, brokenError, _) = await service.BuildAsync(packagePath, ct: CancellationToken.None);
        TestContext.WriteLine($"Step 2 — broken build: success={brokenSuccess}");
        TestContext.WriteLine($"Build error output:\n{brokenError}");
        Assert.That(brokenSuccess, Is.False, "Build should fail with stale Widget reference");
        Assert.That(brokenError, Is.Not.Null.And.Not.Empty);

        // ── Step 3: Copilot fixes it and build passes ─────────────────────────
        var runner = CreateRealCopilotRunner();
        var liveService = CreateLanguageService(runner);

        var patches = await liveService.ApplyPatchesAsync(
            customizationRoot: packagePath,
            packagePath: packagePath,
            buildContext: brokenError!,
            ct: CancellationToken.None);

        TestContext.WriteLine($"Patches applied: {patches.Count}");
        foreach (var p in patches)
            TestContext.WriteLine($"  {p.Description}");

        Assert.That(patches, Is.Not.Empty, "Copilot should have applied at least one patch");

        var (finalSuccess, finalError, _) = await liveService.BuildAsync(packagePath, ct: CancellationToken.None);
        TestContext.WriteLine($"Step 3 — final build: success={finalSuccess}, error={finalError}");
        Assert.That(finalSuccess, Is.True, $"Build should pass after Copilot patch. Error: {finalError}");
    }
}
