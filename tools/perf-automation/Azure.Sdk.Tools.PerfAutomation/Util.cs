using Microsoft.Crank.Agent;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    static class Util
    {
        public static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static string GetUniquePath(string path)
        {
            var directoryName = Path.GetDirectoryName(path);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            var uniquePath = Path.Join(directoryName, $"{fileNameWithoutExtension}{extension}");

            int index = 0;
            while (File.Exists(uniquePath))
            {
                index++;
                uniquePath = Path.Join(directoryName, $"{fileNameWithoutExtension}.{index}{extension}");
            }

            return uniquePath;
        }

        // These commands must be run using "cmd /c" on Windows, since they are shell scripts rather than executables.
        private static readonly string[] _requiresShellOnWindows = new string[]
        {
            "mvn",
            "npm",
        };

        // TODO: We should usually not throw on error, since it prevents extracting StandardOutput and StandardError
        public static async Task<ProcessResult> RunAsync(string filename, string arguments, string workingDirectory,
            StringBuilder outputBuilder = null, StringBuilder errorBuilder = null, bool throwOnError = true)
        {
            if (IsWindows && _requiresShellOnWindows.Contains(filename))
            {
                arguments = $"/c {filename} {arguments}";
                filename = "cmd";
            }

            var result = await ProcessUtil.RunAsync(
                filename,
                arguments,
                workingDirectory: workingDirectory,
                throwOnError: false,
                log: true,
                captureOutput: true,
                captureError: true);

            outputBuilder?.Append(result.StandardOutput);
            errorBuilder?.Append(result.StandardError);

            if (throwOnError && result.ExitCode != 0)
            {
                throw new ProcessResultException(command: $"{filename} {arguments}", result: result);
            }
            else
            {
                return result;
            }
        }

        public static void DeleteIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                }
                catch (UnauthorizedAccessException)
                {
                    // Allow deleting read-only files
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(path, recursive: true);
                }
            }
        }
    }
}
