// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Process options for running Python executables with automatic virtual environment resolution.
/// Resolves Python executables from AZSDKTOOLS_PYTHON_VENV_PATH environment variable.
/// </summary>
public class PythonProcessOptions : ProcessOptions
{
    // Environment variable user can set in their system environment variables for specifying Python venv path
    private const string VenvEnvironmentVariable = "AZSDKTOOLS_PYTHON_VENV_PATH";

    /// <summary>
    /// Creates process options for a Python executable with automatic venv resolution.
    /// </summary>
    /// <param name="executableName">Name of the Python executable (e.g., "python", "pytest", "azpysdk")</param>
    /// <param name="args">Command line arguments</param>
    /// <param name="logger">Optional logger for resolution diagnostics</param>
    /// <param name="workingDirectory">Working directory for the process</param>
    /// <param name="timeout">Execution timeout</param>
    /// <param name="logOutputStream">Whether to log stdout/stderr</param>
    public PythonProcessOptions(
        string executableName,
        string[] args,
        ILogger? logger = null,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        bool logOutputStream = true
    ) : base(
        ResolvePythonExecutable(executableName, logger),
        args,
        logOutputStream,
        workingDirectory,
        timeout
    )
    {
    }

    /// <summary>
    /// Resolves a Python executable path from venv.
    /// Checks in order: AZSDKTOOLS_PYTHON_VENV_PATH env var.
    /// </summary>
    /// <param name="executableName">Name of the Python executable</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>Resolved executable path</returns>
    public static string ResolvePythonExecutable(string executableName, ILogger? logger = null)
    {
        string? resolvedFrom = null;

        // Check environment variable
        var venvPath = Environment.GetEnvironmentVariable(VenvEnvironmentVariable);

        // Try to resolve from venv if path is provided
        if (!string.IsNullOrWhiteSpace(venvPath))
        {
            if (!Directory.Exists(venvPath))
            {
                throw new DirectoryNotFoundException(
                    $"Python venv path specified in {VenvEnvironmentVariable} does not exist: {venvPath}");
            }

            var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
            var venvExecutablePath = Path.Combine(venvPath, binDir, executableName);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var exePath = venvExecutablePath + ".exe";
                    logger?.LogInformation("Resolved Python executable '{ExecutableName}' from {Source}: {ResolvedPath}", 
                        executableName, resolvedFrom, exePath);
                    return exePath;
                }

                logger?.LogInformation("Resolved Python executable '{ExecutableName}' from {Source}: {ResolvedPath}", 
                    executableName, resolvedFrom, venvExecutablePath);
                return venvExecutablePath;
            }
            else
            {
                logger?.LogInformation("Resolved Python executable '{ExecutableName}' from {Source}: {ResolvedPath}", 
                    executableName, resolvedFrom, venvExecutablePath);
                return venvExecutablePath;
            }
        }
        return executableName;
    }
}
