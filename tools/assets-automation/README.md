# Assets automation tooling

This directory contains tooling pertaining to the _support_ of externalized assets created by the [azure-sdk test-proxy](../test-proxy/Azure.Sdk.Tools.TestProxy/README.md).

| Directory | Description |
|---|---|
| [assets-maintenance-tool](./assets-maintenance-tool/README.md) | CLI tool used to scan, backup, and clean azure-sdk assets across all repositories. |
| [assets-reporting](./assets-reporting/README.md) | CLI tool used to audit current repositories and find status of test-proxy adoption on a per-package basis. Used to generate weekly reporting. |
| [asset-sync](./assets-sync/README.md) | Deprecated initial version of `asset-sync` implementation. The current implementation ended up integrated directly into the `test-proxy` codebase, rather than existing as an external powershell script. |
