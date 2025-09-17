// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Helper responsible for (optionally) auto-building the internal Java API diff tool jar.
/// </summary>
internal static class JavaApiDiffJarBuilder
{
    internal static bool IsAutoBuildEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("APIDIFF_AUTOBUILD"), "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("APIDIFF_AUTOBUILD"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to build the APIDiffTool Maven project and copy the resulting shaded jar to the conventional location.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the copied conventional jar, or null on failure.</returns>
    internal static async Task<string?> TryAutoBuildAsync(ILogger logger, CancellationToken ct)
    {
        try
        {
            // Locate repo root by searching upward for Azure.Sdk.Tools.Cli.csproj (limit depth to avoid runaway)
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            DirectoryInfo? root = null;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Azure.Sdk.Tools.Cli.csproj"))) { root = dir; break; }
                dir = dir.Parent;
            }
            if (root == null)
            {
                logger.LogDebug("APIDiff auto-build: repo root not found");
                return null;
            }

            var pom = Path.Combine(root.FullName, "Tools", "APIDiffTool", "pom.xml");
            if (!File.Exists(pom))
            {
                logger.LogDebug("APIDiff auto-build: pom not found at {Pom}", pom);
                return null;
            }

            var mvnExe = "mvn" + (OperatingSystem.IsWindows() ? ".cmd" : "");
            var psi = new ProcessStartInfo
            {
                FileName = mvnExe,
                Arguments = "-q -DskipTests package",
                WorkingDirectory = Path.GetDirectoryName(pom)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            logger.LogDebug("APIDiff auto-build: running '{FileName} {Arguments}' in {Cwd}", psi.FileName, psi.Arguments, psi.WorkingDirectory);

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                logger.LogDebug("APIDiff auto-build: failed to start mvn process");
                return null;
            }

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                logger.LogDebug("APIDiff auto-build failed (exit {Code}): {Err}", proc.ExitCode, Truncate(stderr, 400));
                return null;
            }

            var targetDir = Path.Combine(Path.GetDirectoryName(pom)!, "target");
            var shaded = Directory.Exists(targetDir) ? Directory.GetFiles(targetDir, "*-jar-with-dependencies.jar").FirstOrDefault() : null;
            if (shaded == null)
            {
                logger.LogDebug("APIDiff auto-build: shaded jar not found under {TargetDir}", targetDir);
                return null;
            }

            var conventional = Path.Combine(AppContext.BaseDirectory, "tools", "java", "apidiff", "apidiff.jar");
            Directory.CreateDirectory(Path.GetDirectoryName(conventional)!);
            File.Copy(shaded, conventional, overwrite: true);
            logger.LogDebug("APIDiff auto-build: copied {Shaded} -> {Conventional}\nMaven stdout (truncated): {Stdout}", shaded, conventional, Truncate(stdout, 400));
            return File.Exists(conventional) ? conventional : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "APIDiff auto-build threw an exception");
            return null;
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
        {
            return s;
        }
        return s[..max] + "...";
    }
}
