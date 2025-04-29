using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AzureSDKDSpecTools.Helpers
{
    public interface ICommonHelper
    {        public string GetLoggedInUserAlias();
    }
    public class CommonHelper: ICommonHelper
    {
        public string GetLoggedInUserAlias()
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string command = isWindows ? "cmd.exe" : "az";
            string args = isWindows? "/C az ": "";
            args += "ad signed-in-user show --query userPrincipalName -o tsv";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to get GitHub auth token. Error: {process.StandardError.ReadToEnd()}");
                }
                return output.Trim();
            }
        }
    }
}
