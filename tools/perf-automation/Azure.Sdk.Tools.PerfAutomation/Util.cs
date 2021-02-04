using Microsoft.Crank.Agent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    static class Util
    {
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

        public static async Task<ProcessResult> RunAsync(string filename, string arguments, string workingDirectory,
            StringBuilder outputBuilder = null, StringBuilder errorBuilder = null, bool throwOnError = true)
        {
            var result = await ProcessUtil.RunAsync(
                filename,
                arguments,
                workingDirectory: workingDirectory,
                throwOnError: throwOnError,
                log: true,
                captureOutput: true,
                captureError: true);

            outputBuilder?.Append(result.StandardOutput);
            errorBuilder?.Append(result.StandardError);

            return result;
        }
    }
}
