// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Process options for running Python executables with automatic virtual environment resolution.
/// Resolves Python executables from AZSDKTOOLS_PYTHON_VENV_PATH environment variable.
/// </summary>
public class PythonOptions : ProcessOptions
{
    // Environment variable user can set in their system environment variables for specifying Python venv path
    private static string VenvEnvironmentVariable = "AZSDKTOOLS_PYTHON_VENV_PATH";

    /// <summary>
    /// Creates process options for a Python executable with automatic venv resolution.
    /// </summary>
    /// <param name="executableName">Name of the Python executable (e.g., "python", "pytest", "azpysdk")</param>
    /// <param name="args">Command line arguments</param>
    /// <param name="workingDirectory">Working directory for the process</param>
    /// <param name="timeout">Execution timeout</param>
    /// <param name="logOutputStream">Whether to log stdout/stderr</param>
    public PythonOptions(
        string executableName,
        string[] args,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        bool logOutputStream = true
    ) : base(
        ResolvePythonExecutable(executableName),
        args,
        logOutputStream,
        workingDirectory,
        timeout
    )
    {
    }

    /// <summary>
    /// Resolves an executable path within a given venv directory.
    /// Handles platform-specific bin directory (Scripts vs bin) and .exe suffix.
    /// </summary>
    /// <param name="venvPath">Absolute path to the venv root directory.</param>
    /// <param name="executableName">Name of the executable (e.g., "python", "pytest").</param>
    /// <returns>Full path to the executable within the venv.</returns>
    public static string ResolveFromVenvPath(string venvPath, string executableName)
    {
        var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
        var candidate = Path.Combine(venvPath, binDir, executableName);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            !executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            candidate += ".exe";
        }

        return candidate;
    }

    /// <summary>
    /// Resolves a Python executable path from venv.
    /// Checks in order: AZSDKTOOLS_PYTHON_VENV_PATH env var.
    /// </summary>
    /// <param name="executableName">Name of the Python executable</param>
    /// <returns>Resolved executable path</returns>
    public static string ResolvePythonExecutable(string executableName)
    {
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

            return ResolveFromVenvPath(venvPath, executableName);
        }
        return executableName;
    }
}
