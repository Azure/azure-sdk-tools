# Spec: 7 Repository Tooling Contract - (no tool)

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Alternatives Considered](#alternatives-considered) _(optional)_
- [Open Questions](#open-questions)
- [Success Criteria](#success-criteria)
- [Implementation Plan](#implementation-plan)
- [Testing Strategy](#testing-strategy)
- [Documentation Updates](#documentation-updates)

---

## Definitions

_Define any terms that might be ambiguous or interpreted differently across teams. Establish shared understanding before diving into the design._

**Example:**

- **Generate SDK**: In this context, "generate SDK" means...
- **Environment Setup**: Refers to...
- **Tool**: A discrete unit of functionality that...

**Your definitions:**

- **[Splatting]**: [A powershell feature that allows a key/value object to be transformed into script parameters]

---

## Background / Problem Statement

There are no firm guidelines for calling scripts and/or binaries that are specific to a language repository from the `azsdk` tool.
Without a standardized approach, it is difficult to reason about potential breaking changes when `azsdk`
is tightly coupled to the call pattern of scripts in other repositories.

This spec builds upon the ideas outlined in [6-package-updater-spec.md](https://github.com/Azure/azure-sdk-tools/blob/cc5bae3dd44b02c01ebb8d7546fdbeb0838b665d/tools/azsdk-cli/docs/specs/6-package-updater.spec.md#proposed-approaches-for-the-intergration-between-language-specific-logic-and-tools) and the [preceding discussion](https://github.com/Azure/azure-sdk-tools/pull/12563#discussion_r2449474956). It seemed easier to focus that discussion into this separate spec.

### Current State

- Scripts can be referenced directly in `azsdk` by a hardcoded file path in the process helper [example](https://github.com/Azure/azure-sdk-tools/blob/826bd97dc1580e65b82ac7e4c2c4cd35a1ff8c3a/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Services/Languages/DotNetLanguageSpecificChecks.cs#L54)
- There are no CI tests in the target repositories to prevent breaking changes being made to referenced scripts.
- There is no formal versioning/dependency between `azsdk` and the repository state.

### Why This Matters

There is years worth of hardened automation that has been driving the engineering system in the language repositories.
As the `azsdk` tool is being developed it offers opportunities to streamline some of this automation and also make it accessible
to AI agents.
It seems likely that existing repository scripts will always exist in some capacity (see below), so a formalized approach to referencing
these scripts will have the following benefits:

- Improved velocity - tactical script changes can be made with confidence while not requiring an `azsdk` release.
  - As `azsdk` matures and gains usage/impact, the validations/requirements to release a new version will increase.
  - Small, language-specific changes can be made without needing to understand `azsdk` development.
  - Breakglass changes can be made to resolve urgent issues (unblocking releases, security issues, etc.)
- Easier migration of scripts to `azsdk`.
  - It will take time to centralize automation into `azsdk`. There needs to be a bridge between `azsdk` and existing scripts that can support a gradual migration while maintaining reliability.
- New automation experiments can be made in language repositories, even if the desired end state is a generic interface in `azsdk`.
- Language specific edge cases can exist without cluttering the cli/mcp tool list.
- Depending on calling a script (i.e. Export-Apis.ps1) or command line (msbuild /p:ExportApis) that have a set of parameters unique to our repo will be fragile and make versioning `azsdk` with the source changes in the repo very difficult. It will also make this tool very difficult to use in other repo contexts (i.e. the openai or mcp repos).

---

## Goals and Exceptions/Limitations

### Goals

- [ ] Simplify SDK automation
  - Make automation entrypoints easier to understand and invoke
  - Invoke operations in the same way locally and in pipelines. Remove multiple entrypoints for the same logic.
  - Reduce layers of indirection and complexity
  - Improve testability of changes
- [ ] Reduce tight coupling between `azsdk` and language repository automation
  - `azsdk` cli commands and mcp tools should not be bound to a repository structure that isn't designed to version with it
- [ ] Enable development in both areas
  - Keep toil low for small, tactical changes to scripts in a repository
  - Support an efficient on-ramp for script logic to be migrated to `azsdk` without requiring intensive release coordination

### Limitations

Managing parameter mappings can still cause tight coupling or potential breakages.
The design proposal below outlines the use of `[Alias]` for powershell cmdlets to address this.
However, this approach means that direct calls to repository tools (e.g. `msbuild`) should be wrapped in repository scripts.

Adding schemas for parameters could improve reliability but at the cost of requiring changes to `azsdk` and/or scripts to be coordinated.
It would also require additional up front work to capture parameter schemas for existing scripts, along with CI checks to ensure the
scripts implement the schema.

## Design Proposal

The original idea for this approach is copied from the [package updater spec](https://github.com/Azure/azure-sdk-tools/blob/cc5bae3dd44b02c01ebb8d7546fdbeb0838b665d/tools/azsdk-cli/docs/specs/6-package-updater.spec.md#proposed-approaches-for-the-intergration-between-language-specific-logic-and-tools) by [@raych1](http://github.com/raych1):

> Define a small, well-documented contract/API (parameters, expected inputs/outputs, exit codes, JSON schema). Language repo implements the contract (script or program). CLI does normalization, validation, and invokes the repo-side contract. Optionally implement language-specific logic in CLI for cases where shared helpers or BCL types are beneficial; otherwise call the repo script.

> Pros: Best of both worlds: a small, stable contract (parameters, exit codes, JSON output) that CLI depends on while letting repo implement details. Repo can evolve implementation without breaking consumers so long as contract remains stable. CLI can provide shared normalization, validation, telemetry, and orchestration, while repo scripts handle repo-local tasks (file paths, tool variants). Easier to test and mock (CLI tests against a known contract; repo tests ensure script adheres to contract).

> Cons: Requires upfront design of the contract and discipline to keep it stable. Still requires versioning decisions (how to roll out contract changes across repos). Some duplication of wrapper code may persist (but minimized if contract is well-designed).

The design proposal below removes some aspects of the contract initially proposed, only keeping the script path and lookup tags.

### Overview

When a command/tool is invoked in `azsdk` that is known to be repository-specific (i.e. the command requires a package path or a call to get the repository root), it should first check a centralized config in that repository. If the config contains an override entry for the command, call the override specified. If not, fall back to the implementation in the CLI/MCP server (or fail if not implemented).

### Detailed Design

In `eng/azsdk-automation-contract.json` in a language repository, a set of mappings can be defined that
correspond to CLI commands and/or MCP tools.

```
[
  {
    "tags": ["FooBarCheck", "azsdk_foo_bar", "foobar"],
    "command": "eng/scripts/FooBar.ps1"
  }
]
```

Any script parameters will be [splatted](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_splatting) in the script call, and the mapping enabled via the `[Alias]` attribute in the powershell cmdlet. No contract is defined for parameters.

By supporting `[Alias(...)]` script authors can easily coordinate changes to parameter names in `azsdk` without having to
synchronize releases of the binary with script changes in the local repository.
Aliases used by older `azsdk` versions can be kept around for backwards compatibility.

To onboard an existing script to be used by the `azsdk` tool, all that needs to be done is add aliases for parameters.
Alternatively, normalize the keys used by an `azsdk` invocation of the script to match its existing parameter names.

```pwsh
# eng/scripts/FooBar.ps1
param(
  [Alias('fooBarOption', 'foo-bar')]
  [string] $FooBar
)

echo $FooBar
```

On the `azsdk` side, a service will be written to enable the following call patterns:

- Within a helper method: a key/value parameters object can be created manually and passed to `Invoke`
- In CLI mode: a key/value parameters object can be created from arguments and options, based on the parse result from `System.CommandLine`
  - The key for arguments will be the key given in the CLI help text
  - The key for options will be the non-aliased option flag value with leading hyphens trimmed, e.g. `--repo-path` becomes `repo-path`
- In MCP mode: a key/value parameters object will be created from the variable names and values that are used to generate the tool's JSON schema

```csharp
public async Task<CLICheckResponse> FooBarCheck(string fooBarOption, CancellationToken ct = default)
{
    if (RepositoryService.HasImplementation("FooBarCheck"))
    {
        return RepositoryService.Invoke("FooBarCheck", new()
        {
            [nameof(fooBarOption)] = fooBarOption
        });
    }

    // do fallback work here or throw not implemented
}
```

```csharp
// RepositoryService simple implementation
public async Task<CLICheckResponse> Invoke(string commandName, OrderedDictionary<string, SomeParameterUnionType> params)
{
    var scriptPath = GetCommand(commandName);
    var paramJson = JsonSerializer.Serialize(params, new JsonSerializerOptions { WriteIndented = false });
    var command = $"$params = ('{paramJson}' | ConvertFrom-Json -AsHashtable); & {scriptPath} @params";
    var options = new PowershellOptions(args: command);
    var result = await _powershellHelper.Run(options, ct);
    return new CLICheckResponse(result.ExitCode, result.Output);
}
```

### Cross-Language Considerations

_How does this design work across different SDK languages?_

| Language   | Approach | Notes |
|------------|----------|-------|
| .NET       | [How it works in .NET] | [Any specific considerations] |
| Java       | [How it works in Java] | [Any specific considerations] |
| JavaScript | [How it works in JS] | [Any specific considerations] |
| Python     | [How it works in Python] | [Any specific considerations] |
| Go         | [How it works in Go] | [Any specific considerations] |

### User Experience

[How will developers interact with this? Show examples of commands, outputs, or workflows]

```bash
# foobar command maps to eng/scripts/FooBar.ps1 in config
# --foo-bar option maps to `foo-bar` key via helper method
# The following powershell is executed by the repository service:
#   $params = '{"foo-bar": "foobar"}' | ConvertFrom-Json -AsHashtable
#   eng/scripts/FooBar.ps1 @params
$ azsdk foobar --foo-bar foobar
foobar
```

### Architecture Diagram

TBD

---

## Alternatives Considered _(optional)_

_What other approaches did you evaluate? Why was this design chosen?_

_Micro-Alternative 0_:

As an alternative to the embedded param splatting command, the logic could instead be checked into `eng/common/scripts/invoke-script.ps1`
and then called directly via `_powershellHelper.Run(engCommonInvokeScript, ['-Json', paramJson]);`. This would provide optionality
to make script invocation changes en-masse across the repos without requiring an `azsdk` release. This idea was shelved because
it wouldn't support repositories without `eng/common` syncing. If a need arises, both modes could be supported (invoke eng/common if
it exists, then fall back to embedded).

**Copied from the [package updater spec](https://github.com/Azure/azure-sdk-tools/blob/cc5bae3dd44b02c01ebb8d7546fdbeb0838b665d/tools/azsdk-cli/docs/specs/6-package-updater.spec.md#proposed-approaches-for-the-intergration-between-language-specific-logic-and-tools) by [@raych1](http://github.com/raych1):**

### Alternative 1: CLI class-based integration

**Description**

Implement language-specific behavior inside the CLI (C# classes / services), e.g. DotNetLanguageSpecificChecks.cs style. CLI performs discovery, normalization, and calls language-specific logic directly (possibly invoking lower-level commands like msbuild/tools).

**Pros:**

Centralized logic: all language integrations live inside the CLI — easier to reason about and test in one place. Leverages shared helpers, BCL types. Avoids an extra layer of script indirection (fewer wrappers). Easier to integrate complex cross-language logic (normalization, LLM hooks, common result schema).

**Cons:**

Tight coupling to language repo layout and script parameters → brittle when files/params move. CLI releases must be coordinated with language repo changes (versioning friction). Harder to change language-specific behavior quickly — needs a CLI release or pinned CLI in each repo. Some repo-specific tasks (exact file locations, repo branching differences) are easier in scripts that live next to the source.

### Alternative 2: Repo-hosted script integration (script-first)

Keep the authoritative implementation in each language repo as scripts (PowerShell or other). CLI calls those scripts (or a small wrapper script living in the language repo) as the integration point. Example: Export-API.ps1 or update-metadata.ps1 inside the language repo.

**Pros:**

Locality: the code that knows repo layout and tool invocation lives with the repo it affects; easy to update in one repo PR. Versioning: language repo can change its script and tune parameters without requiring immediate CLI updates. Enables running the same script locally, in CI, and via the CLI — fewer surprises across environments.

**Cons:**

Indirection: multiple wrapper scripts across repos can become confusing (many layers: CLI -> wrapper -> script -> tool). Harder to reason about platform-wide behavior if each repo varies. CLI must handle fragility: script might be missing on branch, parameters may differ, behavior may drift. Harder to apply shared validations/normalization or to test language-agnostic logic centrally.

## Open Questions

_Unresolved items that need discussion and input from reviewers._

- [ ] **Question 1**: Will this work for our anticipated use cases
  - Context: Is this approach too loosely coupled?
  - Options: Add more to the contract (parameter schemas, inputs/outputs)

- [ ] **Question 2**: Do non-powershell (or pwsh wrapped) commands need to be supported?
  - Context: There are many places where we call binaries directly in the current repo automation
  - Options: Will need to re-assess parameter mapping strategy

---

## Success Criteria

_Measurable criteria that define when this feature/tool is complete and working as intended._

This feature/tool is complete when:

- [ ] Developers are able to iterate on repository scripts without creating breaks or incurring toil
- [ ] Developers are able to migrate script automation into `azsdk` easily

---

## Implementation Plan

_If this is a large effort, break down the implementation into phases._

### Phase 1: [Name]

- Milestone: Core implementation
- Timeline: 2 weeks
- Dependencies:
  - Existing hardcoded script calls must be refactored to use the contract instead
  - Language repos must initialize the contract config, if utilizing this feature

### Phase 2: [Name]

- Milestone: Script migration / centralization
- Timeline: 2 weeks
- Dependencies:
  - Identify automation that should be centralized

---

## Testing Strategy

_How will this design be validated?_

### Unit Tests

Tests to call sample scripts using different parameter types. Invoked from mcp, cli and helper modes.

- Test multi-tag references
- Test multi-alias references
- Test sanitization of parameters
- Test powershell execution
- Test

### Integration Tests

Pipeline tests to validate references from `azsdk` to contracts, if applicable

### Manual Testing

Developers go through generation and release process using `azsdk` tooling with repo script references.

### Cross-Language Validation

- Existing CI in langauge repos should catch issues as it is migrated to use `azsdk` as a pipeline entrypoint for tasks.
- Potential CI to validate that scripts referenced in the contract file support `Alias`, if applicable

---

## Metrics/Telemetry

_What data should we collect to measure success or diagnose issues?_

### Metrics to Track

| Metric Name | Description | Purpose |
|-------------|-------------|---------|
| repo_script_fallback_call | How often we call out to repo scripts and succeed | Understand usage of repo automation to inform refactoring/development |

---

## Documentation Updates

_What documentation needs to be created or updated?_

- [ ] README updates to the core guidelines for how to handle repo script references
- [ ] README updates to eng/common so language repos have a doc reference on maintaining the contract file
