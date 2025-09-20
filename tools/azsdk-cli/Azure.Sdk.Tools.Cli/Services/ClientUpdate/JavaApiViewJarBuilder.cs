// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Helper responsible for building the apiview-java-processor Maven project jar that emits APIView JSON
/// including the methodIndex used for fast diffs.
/// Collaborate with Ray to see if this can be already built and stored at a location.
/// </summary>
internal static class JavaApiViewJarBuilder
{
    /// <summary>
    /// Builds the apiview-java-processor Maven project and copies the resulting jar to a conventional location.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the copied conventional jar, or null on failure.</returns>
    internal static async Task<string?> BuildProcessorJarAsync(ILogger logger, CancellationToken ct)
    {
        try
        {
            var repoRoot = Helpers.FileHelper.AscendToGitRoot(AppContext.BaseDirectory);
            if (repoRoot == null)
            {
                logger.LogDebug("APIView processor build: repo root not found");
                return null;
            }

            // Primary expected pom location
            string pom = Path.Combine(repoRoot, "src", "java", "apiview-java-processor", "pom.xml");
            if (!File.Exists(pom))
            {
                logger.LogDebug("APIView processor build: pom not found at {Pom}", pom);
                return null;
            }

            var mvnExe = "mvn" + (OperatingSystem.IsWindows() ? ".cmd" : "");
            var workingDir = Path.GetDirectoryName(pom)!;
            var psi = new ProcessStartInfo
            {
                FileName = mvnExe,
                Arguments = "-q -DskipTests package",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            logger.LogDebug("APIView processor build: running '{FileName} {Arguments}' in {Cwd}", psi.FileName, psi.Arguments, psi.WorkingDirectory);

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                logger.LogDebug("APIView processor build: failed to start mvn process");
                return null;
            }

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                logger.LogDebug("APIView processor build failed (exit {Code}): {Err}", proc.ExitCode, Truncate(stderr, 400));
                return null;
            }

            var targetDir = Path.Combine(workingDir, "target");
            if (!Directory.Exists(targetDir))
            {
                logger.LogDebug("APIView processor build: target directory missing at {TargetDir}", targetDir);
                return null;
            }

            // Prefer shaded jar if present, else regular artifact jar (excluding *-sources.jar)
            var shaded = Directory.GetFiles(targetDir, "*-jar-with-dependencies.jar").FirstOrDefault();
            string? artifact = shaded;
            if (artifact == null)
            {
                artifact = Directory.GetFiles(targetDir, "apiview-java-processor-*.jar")
                    .Where(f => !f.EndsWith("-sources.jar", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.Length) // arbitrary stable choice if multiple versions
                    .FirstOrDefault();
            }

            if (artifact == null)
            {
                logger.LogDebug("APIView processor build: jar artifact not found under {TargetDir}", targetDir);
                return null;
            }

            var conventional = Path.Combine(AppContext.BaseDirectory, "tools", "java", "apiview", "apiview.jar");
            Directory.CreateDirectory(Path.GetDirectoryName(conventional)!);
            File.Copy(artifact, conventional, overwrite: true);
            logger.LogDebug("APIView processor build: copied {Artifact} -> {Conventional}\nMaven stdout (truncated): {Stdout}", artifact, conventional, Truncate(stdout, 400));
            return File.Exists(conventional) ? conventional : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "APIView processor build threw an exception");
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
