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

    public static PythonOptions Pip(string[] args, string workingDirectory = "", string? virtualEnvPath = null) =>
        new() { Command = GetCommand("pip", virtualEnvPath), Args = [..args], WorkingDirectory = workingDirectory, ShortName = "pip" };

    public static PythonOptions Python(string[] args, string workingDirectory = "", string? virtualEnvPath = null) =>
        new() { Command = GetCommand("python", virtualEnvPath), Args = [..args], WorkingDirectory = workingDirectory, ShortName = "python" };

    public static PythonOptions Pytest(string[] args, string workingDirectory = "", string? virtualEnvPath = null) =>
        new() { Command = GetCommand("pytest", virtualEnvPath), Args = [..args], WorkingDirectory = workingDirectory, ShortName = "pytest" };

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