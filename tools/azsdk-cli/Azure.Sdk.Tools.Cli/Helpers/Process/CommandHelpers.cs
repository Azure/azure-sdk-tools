// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;

/*
 * This file contains types for running sub-processes that require custom options classes
 * used to modify the command line before being run.
 * This is all just boilerplate so we can get named logs via ILogger<T> and add
 * more clarity to the caller about what type of command is being run based on the helper type.
*/

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface IProcessHelper
{
    public Task<ProcessResult> Run(ProcessOptions options, CancellationToken ct);
}

public interface IPowershellHelper
{
    public Task<ProcessResult> Run(PowershellOptions options, CancellationToken ct);
}

public interface INpxHelper
{
    public Task<ProcessResult> Run(NpxOptions options, CancellationToken ct);
}

public interface IPythonHelper
{
    Task<ProcessResult> Run(PythonOptions options, CancellationToken ct);
    Task<ProcessResult> RunPip(string[] args, string workingDirectory, string? virtualEnvPath = null, CancellationToken ct = default);
    Task<ProcessResult> RunPython(string[] args, string workingDirectory, string? virtualEnvPath = null, CancellationToken ct = default);
    Task<ProcessResult> RunPytest(string[] args, string workingDirectory, string? virtualEnvPath = null, CancellationToken ct = default);
    Task<string?> CreateVirtualEnvironment(string packagePath, CancellationToken ct = default);
    Task EnsurePythonEnvironment(string packagePath, string? virtualEnvPath = null, CancellationToken ct = default);
}

public sealed class ProcessHelper(ILogger<ProcessHelper> logger, IRawOutputHelper outputHelper)
    : ProcessHelperBase<ProcessHelper>(logger, outputHelper), IProcessHelper
{
    public async Task<ProcessResult> Run(ProcessOptions options, CancellationToken ct) => await base.Run(options, ct);
}

public sealed class PowershellHelper(ILogger<PowershellHelper> logger, IRawOutputHelper outputHelper)
    : ProcessHelperBase<PowershellHelper>(logger, outputHelper), IPowershellHelper
{
    public async Task<ProcessResult> Run(PowershellOptions options, CancellationToken ct) => await base.Run(options, ct);
}

public sealed class NpxHelper(ILogger<NpxHelper> logger, IRawOutputHelper outputHelper)
    : ProcessHelperBase<NpxHelper>(logger, outputHelper), INpxHelper
{
    public async Task<ProcessResult> Run(NpxOptions options, CancellationToken ct) => await base.Run(options, ct);
}

public sealed class PythonHelper(ILogger<PythonHelper> logger, IRawOutputHelper outputHelper)
    : ProcessHelperBase<PythonHelper>(logger, outputHelper), IPythonHelper
{
    public async Task<ProcessResult> Run(PythonOptions options, CancellationToken ct) => await base.Run(options, ct);

    public async Task<ProcessResult> RunPip(string[] args, string workingDirectory, string? virtualEnvPath = null, CancellationToken ct = default) =>
        await Run(PythonOptions.Pip(args, workingDirectory, virtualEnvPath), ct);

    public async Task<ProcessResult> RunPython(string[] args, string workingDirectory, string? virtualEnvPath = null, CancellationToken ct = default) =>
        await Run(PythonOptions.Python(args, workingDirectory, virtualEnvPath), ct);

    public async Task<ProcessResult> RunPytest(string[] args, string workingDirectory, string? virtualEnvPath = null, CancellationToken ct = default) =>
        await Run(PythonOptions.Pytest(args, workingDirectory, virtualEnvPath), ct);

    public async Task<string?> CreateVirtualEnvironment(string packagePath, CancellationToken ct = default)
    {
        var venvPath = Path.Combine(packagePath, ".venv");
        if (Directory.Exists(venvPath))
        {
            return venvPath;
        }

        var result = await RunPython(["-m", "venv", ".venv"], packagePath, null, ct);
        return result.ExitCode == 0 ? venvPath : null;
    }

    public async Task EnsurePythonEnvironment(string packagePath, string? virtualEnvPath = null, CancellationToken ct = default)
    {
        virtualEnvPath ??= await CreateVirtualEnvironment(packagePath, ct);

        await RunPip(["install", "--upgrade", "pip"], packagePath, virtualEnvPath, ct);
        await RunPip(["install", "pytest", "pytest-cov"], packagePath, virtualEnvPath, ct);
        
        if (File.Exists(Path.Combine(packagePath, "dev_requirements.txt")))
        {
            await RunPip(["install", "-r", "dev_requirements.txt"], packagePath, virtualEnvPath, ct);
        }
            
        await RunPip(["install", "-e", "."], packagePath, virtualEnvPath, ct);
    }
}
