// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Helpers.Process
{
    public interface INpxHelper
    {
        public ProcessResult RunNpx(List<string> args, string workingDirectory);
    }

    public class NpxHelper(IProcessHelper processHelper) : INpxHelper
    {
        public ProcessResult RunNpx(List<string> args, string workingDirectory)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                return processHelper.RunProcess(
                    "cmd.exe",
                    ["/C", "npx", .. args],
                    workingDirectory
                );
            }
            else
            {
                return processHelper.RunProcess(
                    "npx",
                    [.. args],
                    workingDirectory
                );
            }
        }
    }
}
