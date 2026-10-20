---
applyTo: "**/*.yml,**/*.yaml,tools/**,eng/**"
excludeAgent: "coding-agent"
---

# Code review: no hardcoded Azure/external resources or magic constants

Applies to pipeline definitions (`*.yml`, `*.yaml`, templates under `eng/` and `tools/**`)
and to tool source code under `tools/**` (C#, TypeScript/JavaScript, Python, Go, PowerShell).
Ignore test fixtures, mocks, and documentation unless they are used at runtime.

## Severity: HIGH — hardcoded Azure or external resources

Flag every instance below as a **HIGH** severity issue. Quote the offending value and
state which parameter, variable, or configuration setting should replace it.

### Pipelines (YAML, templates, scripts invoked by pipelines)

- Hardcoded Key Vault names or URLs (`*.vault.azure.net`).
- Hardcoded Storage account names, blob/queue/table/file endpoints (`*.blob.core.windows.net`, etc.) or container names.
- Hardcoded subscription IDs, tenant IDs, resource group names, service connection names, ACR/registry names, App Service/Function/Container App names, Cosmos DB/SQL/Service Bus/Event Hub/App Configuration names or endpoints, Log Analytics workspaces, and managed identity client/object IDs.
- Hardcoded URLs, hostnames, or organization/project/repo names of external services (Azure DevOps, GitHub, feeds, APIs) that vary by environment.
- Resource identifiers embedded in script arguments or inline scripts.

**Required fix:** declare a pipeline `parameters:` entry (preferred for per-run or per-environment values) or a pipeline/variable-group `variables:` entry, and reference it via `${{ parameters.X }}` or `$(X)`. Defaults on parameters are acceptable only when the value is not environment-specific or secret-bearing; callers must be able to override them.

### Tools (application code)

- Any Azure resource name, endpoint, URL, ID, region, or external service address written as a literal in code.
- Secrets, connection strings, keys, or tokens (also a security issue; flag as HIGH).

**Required fix:** read from environment variables or the tool's configuration (for example `IConfiguration`/options pattern in .NET, `process.env` in Node, `os.environ` in Python, `os.Getenv` in Go, `$env:` or script parameters in PowerShell). Fail fast with a clear error when a required value is missing rather than silently falling back to a hardcoded resource.

## Severity: MEDIUM — hardcoded constants in tools

Tools must not scatter magic literals (strings, numbers, paths, timeouts, retry counts, header/field names, file names, API versions) through the code.

- Flag a literal that is used more than once, or whose meaning is not obvious, and ask for a named constant (`const`/`static readonly`/`UPPER_SNAKE_CASE`/module-level constant) defined in one place, e.g. a dedicated constants class or file.
- Values that depend on Azure or any external resource are covered by the HIGH rule above and must come from environment/pipeline variables or parameters, not merely a named constant.
- Do not flag obvious literals such as `0`, `1`, `-1`, empty strings, or well-known format strings used once.

## Review output guidance

- Label findings with their severity (`HIGH` / `MEDIUM`).
- Give a concrete suggested change, e.g. replace `keyVaultName: 'my-kv'` with a `keyVaultName` parameter and `${{ parameters.keyVaultName }}`.
- Do not flag values that are already parameters, variables, environment lookups, or configuration reads.
