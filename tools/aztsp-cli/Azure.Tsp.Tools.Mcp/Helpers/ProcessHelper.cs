using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Azure.Tsp.Tools.Mcp.Helpers;

public class ProcessHelper
{
    public static (string Output, int ExitCode) RunProcess(string command, string[] args, string workingDirectory)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        var output = new StringBuilder();
        int exitCode = -1;

        using (var process = new Process())
        {
            process.StartInfo = processStartInfo;
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };

            Console.Error.WriteLine($"Running command: {command} {string.Join(" ", args)} in {workingDirectory}");

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit(100_000);

            exitCode = process.ExitCode;
            if (process.ExitCode != 0)
            {
                output.Append($"{Environment.NewLine}Process failed.");
            }

        }

        return (output.ToString(), exitCode);
    }

    public static (string Output, int ExitCode) RunNpx(List<string> args, string workingDirectory)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isWindows)
        {
            return RunProcess(
              "cmd.exe",
              ["/C", "npx", .. args],
              workingDirectory
            );
        }
        else
        {
            return RunProcess(
              "npx",
              [.. args],
              workingDirectory
            );
        }
    }
}
