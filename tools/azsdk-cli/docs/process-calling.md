# Overview

This document contains details for calling external processes.

* [Running External Processes](#running-external-processes)
  * [What to inject](#what-to-inject)
  * [Invoking processes](#invoking-processes)
  * [Options and Custom Helpers](#options-and-custom-helpers)
  * [Result type: ProcessResult](#result-type-processresult)
  * [PowershellHelper: scripts and one-liners](#powershellhelper-scripts-and-one-liners)
  * [Best practices for external processes](#best-practices-for-external-processes)

## Running External Processes

Many tools need to call out to existing CLIs or scripts. Use the provided process helpers instead of spawning processes directly.

- Dependency-injected classes with logging and output streaming
- Cross-platform command wiring (Unix vs. Windows)
- Timeouts and cancellation
- Structured results with exit code and separated stdout/stderr

References:
- See `ExampleTool.DemonstrateProcessExecution` and `ExampleTool.DemonstratePowershellExecution` in [ExampleTool](../Azure.Sdk.Tools.Cli/Tools/ExampleTool.cs)
- Helpers live under [Azure.Sdk.Tools.Cli/Helpers/Process/*](../Azure.Sdk.Tools.Cli/Helpers/Process). See [CommandHelpers](../Azure.Sdk.Tools.Cli/Helpers/Process/CommandHelpers.cs)
for a list of available process helpers (simple, powershell, npx, etc.).

### What to inject

Add the helpers you need to your tool constructor:

```csharp
public class YourTool(
    IProcessHelper processHelper,
    IPowershellHelper powershellHelper,
    INpxHelper npxHelper
) { }
```

### Invoking processes

To run a simple process:

```
var options = new ProcessOptions("sleep", ["1"]);
var result = await processHelper.Run(options, cancellationToken);
SetFailure(result.ExitCode);
```

To run a process where the commands may differ on unix vs. windows:

```
var result = await processHelper.Run(new(
                "goimports", ["-w", "."],
                "gofmt.exe", ["-w", "."],
                workingDirectory: packagePath
             ), cancellationToken);
```

### Options and Custom Helpers

The process helper can be sub-classed along with a custom options class.
This class determines which parameters are required and handles re-writing commands under the hood if needed.

For example, when using the [NpxOptions](../Azure.Sdk.Tools.Cli/Helpers/Process/Options/NpxOptions.cs) class
the first constructor parameter is `package` which will end up running:

```
npx --package @azure-tools/typespec-client-generator-cli -- tsp-client convert --swagger-readme <path> --output-dir <output>
```

```
var npxOptions = new NpxOptions(
    "@azure-tools/typespec-client-generator-cli",
    ["tsp-client", "convert", "--swagger-readme", pathToSwaggerReadme, "--output-dir", outputDirectory]);
await npxHelper.Run(npxOptions, ct);
```

### Result type: ProcessResult

All helpers return `ProcessResult` with:

- `ExitCode` (int): 0 is success. Non-zero should usually call `SetFailure(exitCode)`.
- `Output` (string): stdout and stderr lines combined into a single string.
- `OutputDetails` (List<(StdioLevel, string)>): per-line entries with stream info (StandardOutput | StandardError).

Use `OutputDetails` when stream separation matters; otherwise `Output` is sufficient.

### PowershellHelper: scripts and one-liners

PowerShell can be run as a one-liner (`-Command`) or a script file (`-File`). Example creating a temp script and cleaning it up:

```csharp
// Run script
var scriptOptions = new PowershellOptions(scriptPath, ["-Foo", "foo", "-Bar", "bar"]);
var result = await powershellHelper.Run(scriptOptions, cancellationToken);

// Run command
var cmdOptions = new PowershellOptions(["Write-Host", $"Hello World"]);
var result = await powershellHelper.Run(cmdOptions, cancellationToken);
```

### Best practices for external processes

- Always provide a `CancellationToken` and a sensible `Timeout` value on options.
- Use the default `logOutputStream: true` when calling `processHelper.Run()` for long operations to show progress in CLI mode. Keep it `false` for short/noisy commands.
- Check `ExitCode` and call `SetFailure(exitCode)` when non-zero. Include `result.Output` in error responses for diagnostics (or parse it if it's a large string for MCP mode).
- Consider parsing `OutputDetails` to handle stderr warnings vs. stdout results.
- Never log secrets. Prefer env vars or files for passing sensitive data; avoid putting them in `Args`.
- Keep commands cross-platform when feasible (e.g. if some commands have a `.exe` suffix for windows).
