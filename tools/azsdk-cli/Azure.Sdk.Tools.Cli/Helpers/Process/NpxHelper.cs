// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface INpxHelper
    {
        public Task<ProcessResult> RunNpx(List<string> args, string workingDirectory, CancellationToken ct);
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
        Task<ProcessResult> Run(CancellationToken ct);
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

        public async Task<ProcessResult> Run(CancellationToken ct)
        {
            var finalArgs = new List<string>();

            if (!string.IsNullOrEmpty(Package))
            {
                finalArgs.Add($"--package={Package}");
                finalArgs.Add("--");
            }

            finalArgs.AddRange(args);

            return await npxHelper.RunNpx(finalArgs, Cwd, ct);
        }
    }

    public class NpxHelper(IProcessHelper processHelper) : INpxHelper
    {
        public async Task<ProcessResult> RunNpx(List<string> args, string workingDirectory, CancellationToken ct)
        {
            return await processHelper.RunProcess("npx", [.. args], workingDirectory, ct);
        }

        public INpxCommand CreateCommand()
        {
            return new NpxCommand(this);
        }
    }
}
