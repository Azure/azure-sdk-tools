// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface INpxHelper
    {
        public ProcessResult RunNpx(List<string> args, string workingDirectory);
        public INpxCommand CreateCommand();
    }

    public interface INpxCommand
    {
        /// <summary>
        /// The package to run with npx. If not set, the command will not specify a package and will run the command directly.
        /// If set, it will use the specified package when running the command.
        /// </summary>
        string? Package { get; set; }
        string Cwd { get; set; }
        INpxCommand AddArgs(params string[] args);
        INpxCommand AddArgs(IEnumerable<string> args);
        ProcessResult Run();
    }

    public class NpxCommand : INpxCommand
    {
        private readonly INpxHelper npxHelper;
        private readonly List<string> args = [];

        public string? Package { get; set; }
        public string Cwd { get; set; } = Environment.CurrentDirectory;

        internal NpxCommand(INpxHelper npxHelper)
        {
            this.npxHelper = npxHelper;
        }

        public INpxCommand AddArgs(params string[] args)
        {
            this.args.AddRange(args);
            return this;
        }

        public INpxCommand AddArgs(IEnumerable<string> args)
        {
            this.args.AddRange(args);
            return this;
        }

        public ProcessResult Run()
        {
            var finalArgs = new List<string>();

            if (!string.IsNullOrEmpty(Package))
            {
                finalArgs.Add($"--package={Package}");
                finalArgs.Add("--");
            }

            finalArgs.AddRange(args);

            return npxHelper.RunNpx(finalArgs, Cwd);
        }
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

        public INpxCommand CreateCommand()
        {
            return new NpxCommand(this);
        }
    }
}
