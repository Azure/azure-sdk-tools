# perf-automation

## Overview
`perf-automation` is a tool to run multiple perf tests in any languages and collect the results in a single JSON file.

## Walkthrough
1. [Install .NET](https://dotnet.microsoft.com/download)

2. `git clone https://github.com/Azure/azure-sdk-tools`

3. `cd azure-sdk-tools/tools/perf-automation/Azure.Sdk.Tools.PerfAutomation`

4. Copy `config.sample.yml` to `config.yml` and update paths to language repos.  Unused languages can be deleted.

5. Run `dotnet run -- --help` to view available command-line arguments

6. Example (.NET): `dotnet run -- run --language Net --language-version 8 --repo-root <path-to-azure-sdk-for-net> --tests-file <path-to-azure-sdk-for-net>/sdk/storage/Azure.Storage.Blobs/perf-tests.yml --tests download --arguments 10240 --dry-run`

7. Example (Go): `dotnet run -- run --language Go --language-version 1.25 --repo-root <path-to-azure-sdk-for-go> --tests-file <path-to-azure-sdk-for-go>/sdk/storage/azblob/perf-tests.yml --tests download --arguments 10240 --dry-run`

8. View results in file `results/results.json`

## Supported Languages

| Language | `--language` value | Example `--language-version` |
| -------- | ------------------ | ---------------------------- |
| .NET     | `Net`              | `8`                          |
| Java     | `Java`             | `17`                         |
| JS       | `JS`               | `18`                         |
| Python   | `Python`           | `3.11`                       |
| Cpp      | `Cpp`              | `N/A`                        |
| Rust     | `Rust`             | `N/A`                        |
| Go       | `Go`               | `1.25`                       |

### Go-specific notes

- Go perf runs are sync-only; the `--no-async` flag is applied automatically.
- The `--repo-root` must point to a local clone of [`Azure/azure-sdk-for-go`](https://github.com/Azure/azure-sdk-for-go).
- The `--tests-file` should point at a service's `perf-tests.yml` inside the Go SDK repo (for example `sdk/storage/azblob/perf-tests.yml`).
- When `PackageVersions` is set to `source`, the runner adds a `go mod edit -replace=<pkg>=<local-path>` against the module under `--repo-root`, runs `go mod tidy`, and restores the original `go.mod` / `go.sum` on cleanup.
- Throughput is parsed from lines matching the standard perf format, e.g. `Completed 721 operations in a weighted-average of 30.00s (24.033 ops/s, 0.042 s/op)`.
