// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

public interface IMavenHelper
{
    public Task<ProcessResult> Run(MavenOptions options, CancellationToken ct);
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

public sealed class MavenHelper(ILogger<MavenHelper> logger, IRawOutputHelper outputHelper)
    : ProcessHelperBase<MavenHelper>(logger, outputHelper), IMavenHelper
{
    public async Task<ProcessResult> Run(MavenOptions options, CancellationToken ct) => await base.Run(options, ct);
}
