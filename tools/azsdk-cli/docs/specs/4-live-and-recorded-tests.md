# Spec: Live and Recorded Tests

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Alternatives Considered](#alternatives-considered)
- [Success Criteria](#success-criteria)
- [Agent Prompts](#agent-prompts)
- [CLI Commands](#cli-commands)
- [Testing Strategy](#testing-strategy)

---

## Definitions

- **Test Mode**: The mode in which tests are executed - Record, Playback, or Live.
- **Record Mode**: Tests run against real Azure resources and record HTTP interactions for later playback.
- **Playback Mode**: Tests run against previously recorded HTTP interactions without hitting real Azure resources.
- **Live Mode**: Tests run against real Azure resources without recording.
- **Test Resources**: Azure resources required to run live or recorded tests (e.g., storage accounts, key vaults).
- **Test Proxy**: The tool that intercepts and records/plays back HTTP traffic during test execution.

---

## Background / Problem Statement

### Current State

As part of the scenario 1 work, we support running tests in **playback mode only** in all supported languages.

### Why This Matters

Being able to run tests against live resources, and in record mode, is necessary step for brand new packages. Before tests can be run in playback mode, tests must be recorded and written.

---

## Goals and Exceptions/Limitations

### Goals

- [ ] Support for running tests in record, playback, and live mode
- [ ] Test resource deployment and cleanup
- [ ] Stretch goal: Bicep authoring support. To be discussed in a future spec.
- [ ] Stretch goal: Test authoring support. To be discussed in a future spec.

---

## Design Proposal

### Running tests in record and playback mode

The `azsdk_package_run_tests` tool will be enhanced to support live and recorded tests. This will be done via a new `TestMode` enum with values `Record`, `Live`, and `Playback` passed to the `LanguageService` `RunAllTests` method. Language owners will be responsible for handling the enum value and using it to set the correct test mode. If no test mode was specified by the user, the tool will select `Playback` as the default value.

In live and record modes, a dictionary of environment variables will also be provided which should be passed through to the language's test runner. These environment variables will typically come from the New-TestResources.ps1 script.

```cs
public enum TestMode
{
    Record,
    Live,
    Playback
}

public abstract class LanguageService
{
    // Update to RunAllTests method to be implemented by each language
    public abstract Task<TestRunResponse> RunAllTests(string packagePath, TestMode testMode, IDictionary<string, string>? liveTestEnvironment, CancellationToken ct = default);
}
```

### Test resource deployment and cleanup

Deployment of test resources involves many different variables depending on the user's needs. We do not know which subscription, tenant, etc. the user might have available or want to use. For example, devs on the SDK team might use the Playground subscription to deploy test resources, but devs on service teams may have their own subscriptions they might want to use. Or, maybe the user wants to deploy to TME. Given the array of possibilities here, a __skill__ will be developed for deploying test resources in the context of running live and recorded tests.

#### Skill outline

The skill will guide the agent through these steps:

1. Ensure that the user has an active Azure PowerShell context (`Get-AzContext`), and, if not, guide them through logging in via Azure PowerShell (`Connect-AzureAccount`).
2. Run the `eng/common/TestResources/New-TestResources.ps1` PowerShell script with the necessary parameters (service directory, resource group name, deployment region, any additional parameters provided by the user e.g. enableHsm for Key Vault...)
3. Save the environment variables output by the script to a well-known location to be passed the run tests tool
4. Invoke the test tool in the selected test mode with the test environment
5. Clean up test resources when done using the `eng/common/TestResources/Remove-TestResources.ps1` script.

The skill will also include troubleshooting steps. For example, for when test resource deployment fails, it will have information about potential causes and fixes (e.g. auth issues).

---

## Alternatives Considered

### Alternative: New tool instead of skill

**Description:**
Create a new tool with a rigid set of steps for deploying test resources.

**Why not chosen:**
A skill is more flexible and should do a better job at 'guiding' the user along the right path, instead of having a tool with a rigid set of steps that might not all apply to whatever the user is trying to do.

---

## Success Criteria

This feature/tool is complete when:

- [ ] All five languages successfully execute tests in all three modes
- [ ] Test resources can be deployed and cleaned up via the skill

---

## Agent Prompts

### Run Tests in Playback Mode

**Prompt:**

```text
Run the tests for this package
```

**Expected Agent Activity:**

Playback mode should be the default behavior.

1. Agent should call the run test tool in playback mode and report the results.

### Run Tests in Record Mode

**Prompt:**

```text
Run the tests in record mode using the live test deployment specified in #path/to/.env
```

**Expected Agent Activity:**

1. Agent should call the run test tool in record mode directly with the provided environment variables instead of attempting to deploy test resources.

### Deploy Test Resources and Run Live Tests

**Prompt:**

```text
Deploy test resources and run live tests
```

**Expected Agent Activity:**

1. Agent should follow steps in the test resource deployment skill to deploy the test resources and run the tests.

---

## CLI Commands

### Run Tests

**Command:**

```bash
azsdk package test run --mode <playback|record|live> [--package-path <path>] [--test-environment <path-to-.env>]
```

**Options:**

- `-m, --mode <mode>`: Test mode - playback, record, or live (default: playback)
- `-p, --package-path <path>`: Path to the package to test (optional)
- `--test-environment <path>`: Path to a .env file containing deployment environment variables (optional)

**Expected Output:**

```
[[ a log of the test run; will vary by language. ]]
```

---

## Testing Strategy

- Unit and integration tests to ensure that the test tool passes through the correct parameters to the language implementations
- Evaluation of the skill description and tool calls made to ensure the skill is working as expected