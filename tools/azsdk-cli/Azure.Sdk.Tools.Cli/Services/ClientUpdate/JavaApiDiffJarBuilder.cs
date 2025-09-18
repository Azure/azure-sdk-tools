// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Helper responsible for building the internal Java API diff tool jar.
/// </summary>
internal static class JavaApiDiffJarBuilder
{
    /// <summary>
    /// Builds the APIDiffTool Maven project and copy the resulting shaded jar to the conventional location.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the copied conventional jar, or null on failure.</returns>
    internal static async Task<string?> BuildDiffJarAsync(ILogger logger, CancellationToken ct)
    {
        try
        {
            var repoRoot = Helpers.FileHelper.AscendToGitRoot(AppContext.BaseDirectory);
            if (repoRoot == null)
            {
                logger.LogDebug("APIDiff build: repo root not found");
                return null;
            }

            // Primary expected location (repo root / Tools / APIDiffTool)
            string? pom = Path.Combine(repoRoot, "Tools", "APIDiffTool", "pom.xml");
            if (!File.Exists(pom))
            {
                // Fallback: APIDiffTool nested under the CLI project
                var cliNested = Path.Combine(repoRoot, "tools", "azsdk-cli", "Azure.Sdk.Tools.Cli", "Tools", "APIDiffTool", "pom.xml");
                if (File.Exists(cliNested))
                {
                    pom = cliNested;
                }
                else
                {
                    logger.LogDebug("APIDiff build: pom not found at {Primary} or {Nested}", pom, cliNested);
                    return null;
                }
            }

            // Build the jar at found pom location
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

            logger.LogDebug("APIDiff build: running '{FileName} {Arguments}' in {Cwd}", psi.FileName, psi.Arguments, psi.WorkingDirectory);

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                logger.LogDebug("APIDiff build: failed to start mvn process");
                return null;
            }

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                logger.LogDebug("APIDiff build failed (exit {Code}): {Err}", proc.ExitCode, Truncate(stderr, 400));
                return null;
            }

            var targetDir = Path.Combine(Path.GetDirectoryName(pom)!, "target");
            var shaded = Directory.Exists(targetDir) ? Directory.GetFiles(targetDir, "*-jar-with-dependencies.jar").FirstOrDefault() : null;
            if (shaded == null)
            {
                logger.LogDebug("APIDiff build: shaded jar not found under {TargetDir}", targetDir);
                return null;
            }

            var conventional = Path.Combine(AppContext.BaseDirectory, "tools", "java", "apidiff", "apidiff.jar");
            Directory.CreateDirectory(Path.GetDirectoryName(conventional)!);
            File.Copy(shaded, conventional, overwrite: true);
            logger.LogDebug("APIDiff build: copied {Shaded} -> {Conventional}\nMaven stdout (truncated): {Stdout}", shaded, conventional, Truncate(stdout, 400));
            return File.Exists(conventional) ? conventional : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "APIDiff build threw an exception");
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
