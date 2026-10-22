---
applyTo: "**/*.yml,**/*.yaml,tools/**,eng/**"
excludeAgent: "cloud-agent"
---

# Code review: no hardcoded Azure/external resources or magic constants

Applies to pipeline definitions (`*.yml`, `*.yaml`, templates under `eng/` and `tools/**`)
and to tool source code under `tools/**` (C#, TypeScript/JavaScript, Python, Go, PowerShell).
Ignore test fixtures, mocks, and documentation unless they are used at runtime.

## Severity: HIGH — hardcoded Azure or external resources

Apply this resource restriction only to non-public, deployment-specific resource names,
identifiers, or URLs in pipelines and code. Do not flag publicly known URLs or invariant
service/protocol endpoints, such as `https://github.com`, `https://api.github.com`,
public GitHub repository URLs, `https://aka.ms/...` links, or Microsoft identity service
endpoints such as `https://login.microsoftonline.com`.
A public service hostname does not exempt a URL that embeds a non-public resource name,
tenant/subscription ID, private repository path, or credential. Credentials must always
be flagged, even in otherwise public URLs.

Flag in-scope instances below as **HIGH** severity issues. Quote only non-secret values;
redact secrets, tokens, keys, and credentials embedded in connection strings or URLs.
State which parameter, variable, or configuration setting should replace the value.
For exposed credentials, also recommend revocation or rotation.

### Pipelines (YAML, templates, scripts invoked by pipelines)

- Hardcoded Key Vault names or URLs (`*.vault.azure.net`).
- Hardcoded Storage account names, blob/queue/table/file endpoints (`*.blob.core.windows.net`, etc.) or container names.
- Hardcoded subscription IDs, tenant IDs, resource group names, service connection names, ACR/registry names, App Service/Function/Container App names, Cosmos DB/SQL/Service Bus/Event Hub/App Configuration names or endpoints, Log Analytics workspaces, and managed identity client/object IDs.
- Hardcoded non-public, deployment-specific URLs, hostnames, or organization/project/repo names of external services (Azure DevOps, GitHub, feeds, APIs).
- Resource identifiers embedded in script arguments or inline scripts.

**Required fix:** declare a pipeline `parameters:` entry (preferred for per-run or per-environment values) or a pipeline/variable-group `variables:` entry, and reference it via `${{ parameters.X }}` or `$(X)`. Defaults on parameters are acceptable only when the value is not environment-specific or secret-bearing; callers must be able to override them.
Require parameterization only where the pipeline schema supports the chosen substitution.
Exempt schema-required literals from hardcoding findings, such as
`resources.repositories[*].name`, which accepts neither variables nor template expressions.
This exemption does not apply to exposed credentials.

### Tools (application code)

- Non-public, deployment-specific Azure resource names, endpoints, URLs, IDs, or external service resource addresses written as literals in code.
- Secrets, connection strings, keys, or tokens (also a security issue; flag as HIGH).

**Required fix:** read from environment variables or the tool's configuration (for example `IConfiguration`/options pattern in .NET, `process.env` in Node, `os.environ` in Python, `os.Getenv` in Go, `$env:` or script parameters in PowerShell). Fail fast with a clear error when a required value is missing rather than silently falling back to a hardcoded resource.

## Severity: MEDIUM — hardcoded constants in tools

Tools must not scatter magic literals (strings, numbers, paths, timeouts, retry counts, header/field names, file names, API versions) through the code.

- Flag a literal that is used more than once, or whose meaning is not obvious, and ask for a named constant (`const`/`static readonly`/`UPPER_SNAKE_CASE`/module-level constant) defined in one place, e.g. a dedicated constants class or file.
- Non-public, deployment-specific resource values covered by the HIGH rule above must come from environment/pipeline variables or parameters, not merely a named constant. Public service/protocol endpoints are not subject to that resource restriction.
- Do not flag obvious literals such as `0`, `1`, `-1`, empty strings, or well-known format strings used once.

## Review output guidance

- Label findings with their severity (`HIGH` / `MEDIUM`).
- Give a concrete suggested change, e.g. replace `keyVaultName: 'my-kv'` with a `keyVaultName` parameter and `${{ parameters.keyVaultName }}`.
- Do not flag values that are already parameters, variables, environment lookups, or configuration reads.
