// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface INpxHelper
    {
        public Task<ProcessResult> RunNpxAsync(List<string> args, string workingDirectory, CancellationToken ct);
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
        Task<ProcessResult> RunAsync(CancellationToken ct);
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

        public async Task<ProcessResult> RunAsync(CancellationToken ct)
        {
            var finalArgs = new List<string>();

            if (!string.IsNullOrEmpty(Package))
            {
                finalArgs.Add($"--package={Package}");
                finalArgs.Add("--");
            }

            finalArgs.AddRange(args);

            return await npxHelper.RunNpxAsync(finalArgs, Cwd, ct);
        }
    }

    public class NpxHelper(IProcessHelper processHelper) : INpxHelper
    {
        public async Task<ProcessResult> RunNpxAsync(List<string> args, string workingDirectory, CancellationToken ct)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                return await processHelper.RunProcessAsync(
                    "cmd.exe",
                    ["/C", "npx", .. args],
                    workingDirectory,
                    ct
                );
            }
            else
            {
                return await processHelper.RunProcessAsync(
                    "npx",
                    [.. args],
                    workingDirectory,
                    ct
                );
            }
        }

        public INpxCommand CreateCommand()
        {
            return new NpxCommand(this);
        }
    }
}
