# perf-automation

## Overview
`perf-automation` is a tool to run multiple perf tests in any languages and collect the results in a single JSON file.

## Walkthrough
1. [Install .NET](https://dotnet.microsoft.com/download)

2. `git clone https://github.com/Azure/azure-sdk-tools`

3. `cd azure-sdk-tools/tools/perf-automation/Azure.Sdk.Tools.PerfAutomation`

4. Copy `config.sample.yml` to `config.yml` and update paths to language repos.  Unused languages can be deleted.

5. Run `dotnet run -- --help` to view available command-line arguments

6. Example: `dotnet run -- run --languages net java --services sample --dry-run`

7. View results in file `results/results.json`
