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
/// All tests work from a sparse clone of azure-sdk-for-python at a pinned SHA so behaviour
/// is deterministic regardless of local clone state.  Tests are skipped automatically when
/// there is no network access or when GitHub Copilot CLI is not available/authenticated.
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

    /// <summary>Recursively copies a directory tree.</summary>
    internal static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    // ── Pinned-SHA clone setup ────────────────────────────────────────────────
    //
    // Sparse-clones azure-sdk-for-python at a pinned SHA once for the whole fixture.
    // At SHA 79728834 (2026-03-12), models/_patch.py uses NumberField throughout:
    //   - from ._models import (..., NumberField, ...)
    //   - __all__ = [..., "NumberField", ...]
    //   - if TYPE_CHECKING: class NumberField(NumberField): ...
    //   - _add_value_property_to_field(NumberField, "value_number", Optional[float])

    private const string PinnedPythonRepoSha = "79728834e7f38018d372860cf9117bf51d9ed417";
    private const string PinnedPythonRepoUrl = "https://github.com/Azure/azure-sdk-for-python";
    private const string PinnedRelativePackagePath = "sdk/contentunderstanding/azure-ai-contentunderstanding";

    private TempDirectory? _cloneDir;
    private string? _clonedPackagePath;

    /// <summary>
    /// Attempts a sparse clone at the pinned SHA. If the clone fails (e.g. no network),
    /// sets <see cref="_clonedPackagePath"/> to null so individual tests can skip cleanly
    /// without marking the whole fixture as ignored.
    /// </summary>
    [OneTimeSetUp]
    public async Task SetUpPinnedClone()
    {
        _cloneDir = TempDirectory.Create("py-lang-svc-pinned-clone");

        var cloneExit = await RunCommand("git",
        [
            "clone",
            "--filter=blob:none",
            "--no-checkout",
            "--sparse",
            PinnedPythonRepoUrl,
            _cloneDir.DirectoryPath
        ]);

        if (cloneExit != 0)
        {
            _cloneDir.Dispose();
            _cloneDir = null;
            return; // _clonedPackagePath stays null; pinned tests will skip individually
        }

        // Include the SDK package plus all paths that azpysdk resolves from the repo root
        // at runtime: eng/scripts (get_package_properties.py), eng/tools/azure-sdk-tools
        // (installed into isolated venvs by azpysdk --isolate),
        // scripts/devops_tasks (common_tasks.py), sdk/core/azure-core
        // (relative path dep in dev_requirements.txt), and pylintrc (repo-root config).
        await RunCommand("git", ["-C", _cloneDir.DirectoryPath, "sparse-checkout", "set",
            PinnedRelativePackagePath,
            "sdk/core/azure-core",
            "eng/scripts",
            "eng/tools/azure-sdk-tools",
            "scripts/devops_tasks",
            "pylintrc"]);
        await RunCommand("git", ["-C", _cloneDir.DirectoryPath, "checkout", PinnedPythonRepoSha]);

        var candidate = Path.Combine(_cloneDir.DirectoryPath,
            PinnedRelativePackagePath.Replace('/', Path.DirectorySeparatorChar));
        _clonedPackagePath = Directory.Exists(candidate) ? candidate : null;
    }

    [OneTimeTearDown]
    public void TearDownPinnedClone() => _cloneDir?.Dispose();

    private static async Task<int> RunCommand(string fileName, string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        var proc = System.Diagnostics.Process.Start(psi)!;
        await proc.WaitForExitAsync();
        return proc.ExitCode;
    }

    // ── Test ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Three-step live roundtrip using a pinned SHA clone:
    ///   1. Verify the clean package builds without errors.
    ///   2. Rename NumberField → NumberFieldOld in models/_patch.py and verify the build fails.
    ///   3. Feed the real build errors to ApplyPatchesAsync with a live Copilot runner,
    ///      then verify the build passes again.
    ///
    /// Requires GitHub Copilot CLI installed and authenticated.
    /// </summary>
    [Test]
    public async Task Live_PinnedClone_CleanBuildThenFixNumberFieldRename()
    {
        if (_clonedPackagePath is null)
            Assert.Ignore("Pinned clone not available (no network?) — skipping.");
        if (!await CopilotTestHelper.IsCopilotAvailableAsync())
            Assert.Ignore("GitHub Copilot CLI not available or not authenticated.");

        using var pinnedTemp = TempDirectory.Create("py-lang-svc-pinned");

        // Mirror the sparse-checked-out directories into a temp working tree so that:
        //   - discover_repo_root() finds repoRoot as git root
        //   - relative path deps in dev_requirements.txt resolve correctly
        //     (e.g. ../../core/azure-core from sdk/contentunderstanding/<pkg>)
        var repoRoot = pinnedTemp.DirectoryPath;
        var cloneRoot = _cloneDir!.DirectoryPath;
        CopyDirectory(Path.Combine(cloneRoot, "sdk", "contentunderstanding", "azure-ai-contentunderstanding"),
            Path.Combine(repoRoot, "sdk", "contentunderstanding", "azure-ai-contentunderstanding"));
        CopyDirectory(Path.Combine(cloneRoot, "sdk", "core", "azure-core"),
            Path.Combine(repoRoot, "sdk", "core", "azure-core"));
        CopyDirectory(Path.Combine(cloneRoot, "eng", "scripts"),
            Path.Combine(repoRoot, "eng", "scripts"));
        CopyDirectory(Path.Combine(cloneRoot, "eng", "tools", "azure-sdk-tools"),
            Path.Combine(repoRoot, "eng", "tools", "azure-sdk-tools"));
        CopyDirectory(Path.Combine(cloneRoot, "scripts", "devops_tasks"),
            Path.Combine(repoRoot, "scripts", "devops_tasks"));
        File.Copy(Path.Combine(cloneRoot, "pylintrc"), Path.Combine(repoRoot, "pylintrc"));
        await RunGitInit(repoRoot);

        var packagePath = Path.Combine(repoRoot, "sdk", "contentunderstanding", "azure-ai-contentunderstanding");

        var modelsPatchFile = Path.Combine(
            packagePath, "azure", "ai", "contentunderstanding", "models", "_patch.py");
        Assert.That(File.Exists(modelsPatchFile), Is.True, "models/_patch.py not found in pinned clone");

        // ── Step 1: clean build should pass ──────────────────────────────────
        var service = CreateLanguageService(Mock.Of<ICopilotAgentRunner>());
        var (cleanSuccess, cleanError, _) = await service.BuildAsync(
            packagePath, timeoutMinutes: 10, ct: CancellationToken.None);

        TestContext.WriteLine($"Step 1 — clean build: success={cleanSuccess}, error={cleanError}");
        Assert.That(cleanSuccess, Is.True, $"Expected clean pinned build to pass. Error: {cleanError}");

        // ── Step 2: introduce stale rename and verify build fails ─────────────
        var original = await File.ReadAllTextAsync(modelsPatchFile);
        const string StaleName = "NumberFieldOld";
        await File.WriteAllTextAsync(modelsPatchFile, original.Replace("NumberField", StaleName));

        var (brokenSuccess, brokenError, _) = await service.BuildAsync(
            packagePath, timeoutMinutes: 10, ct: CancellationToken.None);

        TestContext.WriteLine($"Step 2 — broken build: success={brokenSuccess}");
        TestContext.WriteLine($"Build error output:\n{brokenError}");
        Assert.That(brokenSuccess, Is.False, "Build should fail with stale NumberFieldOld reference");
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

        var resultContent = await File.ReadAllTextAsync(modelsPatchFile);
        Assert.That(resultContent, Does.Not.Contain(StaleName),
            "Copilot should have replaced all stale symbol occurrences");

        var (finalSuccess, finalError, _) = await liveService.BuildAsync(
            packagePath, timeoutMinutes: 10, ct: CancellationToken.None);

        TestContext.WriteLine($"Step 3 — final build: success={finalSuccess}, error={finalError}");
        Assert.That(finalSuccess, Is.True, $"Build should pass after Copilot patch. Error: {finalError}");
    }
}
