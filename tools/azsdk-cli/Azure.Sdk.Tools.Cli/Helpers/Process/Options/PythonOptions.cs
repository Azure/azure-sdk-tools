// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Helpers;

public class PythonOptions : IProcessOptions
{
    private const int DEFAULT_TIMEOUT_SECONDS = 300;

    public string Command { get; private set; } = "";
    public List<string> Args { get; private set; } = [];
    public string WorkingDirectory { get; set; } = "";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS);
    public bool LogOutputStream { get; set; } = true;
    public string ShortName { get; private set; } = "python";
    public Dictionary<string, string> EnvironmentVariables { get; private set; } = new();

    public static PythonOptions Pip(string[] args, string workingDirectory = "", string? virtualEnvPath = null) =>
        CreateOptions("pip", args, workingDirectory, virtualEnvPath, "pip");

    public static PythonOptions Python(string[] args, string workingDirectory = "", string? virtualEnvPath = null) =>
        CreateOptions("python", args, workingDirectory, virtualEnvPath, "python");

    public static PythonOptions Pytest(string[] args, string workingDirectory = "", string? virtualEnvPath = null) =>
        CreateOptions("pytest", args, workingDirectory, virtualEnvPath, "pytest");

    private static PythonOptions CreateOptions(string tool, string[] args, string workingDirectory, string? virtualEnvPath, string shortName)
    {
        var options = new PythonOptions
        {
            Command = GetCommand(tool, virtualEnvPath),
            Args = [..args],
            WorkingDirectory = workingDirectory,
            ShortName = shortName
        };

        // Set up virtual environment variables if using a venv
        if (!string.IsNullOrEmpty(virtualEnvPath))
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var scriptsDir = Path.Combine(virtualEnvPath, isWindows ? "Scripts" : "bin");
            
            options.EnvironmentVariables["VIRTUAL_ENV"] = virtualEnvPath;
            options.EnvironmentVariables["PATH"] = $"{scriptsDir}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
            
            // Set PYTHONPATH to include the site-packages directory
            if (isWindows)
            {
                var sitePackages = Path.Combine(virtualEnvPath, "Lib", "site-packages");
                options.EnvironmentVariables["PYTHONPATH"] = sitePackages;
            }
            else
            {
                // On Unix, we need to find the python version directory
                var libDir = Path.Combine(virtualEnvPath, "lib");
                if (Directory.Exists(libDir))
                {
                    var pythonDirs = Directory.GetDirectories(libDir, "python*");
                    if (pythonDirs.Length > 0)
                    {
                        var sitePackages = Path.Combine(pythonDirs[0], "site-packages");
                        options.EnvironmentVariables["PYTHONPATH"] = sitePackages;
                    }
                }
            }
        }

        return options;
    }

    private static string GetCommand(string tool, string? virtualEnvPath)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        
        if (!string.IsNullOrEmpty(virtualEnvPath))
        {
            var subDir = isWindows ? "Scripts" : "bin";
            var executable = tool switch
            {
                "python" => isWindows ? "python.exe" : "python",
                "pip" => isWindows ? "pip.exe" : "pip",
                "pytest" => isWindows ? "pytest.exe" : "pytest",
                _ => tool
            };
            return Path.Combine(virtualEnvPath, subDir, executable);
        }

        return tool switch
        {
            "python" => isWindows ? "python.exe" : "python",
            "pip" => isWindows ? "pip.exe" : "pip",
            "pytest" => isWindows ? "pytest.exe" : "pytest",
            _ => tool
        };
    }
}