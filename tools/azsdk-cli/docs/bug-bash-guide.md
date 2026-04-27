# Azure SDK Tools Agent Bug Bash — Participant Guide

Welcome! You've been invited to help bug-bash the **Azure SDK Tools Agent** (`azsdk-cli`) — an AI-assisted CLI and MCP server that helps Azure SDK engineers convert Swagger to TypeSpec, generate SDKs, manage release plans, diagnose pipelines, work with APIView, and more.

We want you to use the agent like you'd use any new tool: try the scenarios below, push on edge cases, and tell us where it breaks, surprises you, or just feels wrong. **Good feedback is specific** — a clear repro, what you expected, what actually happened, and a screenshot or log if you have one. Small papercuts count.

## Table of Contents

- [Setup](#setup)
- [Verification Checklist](#verification-checklist)
- [Scenarios to Try](#scenarios-to-try)
- [How to File Feedback](#how-to-file-feedback)
- [Tips](#tips)

## Setup

Do this **before** the bug bash starts so you don't burn session time on installs.

### Prerequisites

**OS:** Windows 10/11, macOS 12+, or Linux (Ubuntu 20.04+).

**Software:**
- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)
- [Node.js 18+ and npm](https://nodejs.org/) (for TypeSpec scenarios)
- [Git](https://git-scm.com/)
- [GitHub CLI (`gh`)](https://cli.github.com/) — authenticated (`gh auth login`)
- [VS Code](https://code.visualstudio.com/) with the [GitHub Copilot extension](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot) (only needed for MCP scenarios)

**Optional:**
- Azure CLI (`az`) for Azure-authenticated scenarios
- Java 11+ / Python 3.8+ if you want to test those SDK languages

### Account access

- A GitHub account with read access to `Azure/azure-sdk-tools`, at least one `Azure/azure-sdk-for-*` repo, and `Azure/azure-rest-api-specs`
- A GitHub PAT with `repo` and `workflow` scopes (set as `GITHUB_TOKEN`)
- For pipeline / release-plan scenarios: an Azure DevOps PAT for the `azure-sdk` org (set as `AZURE_DEVOPS_PAT`)
- **Internal Microsoft only:** APIView access and Azure SDK Architecture Board access — needed for the release-plan and APIView scenarios. If you don't have these, just skip those scenarios.

### ⚠️ Required: set `AZSDKTOOLS_AGENT_TESTING=true`

**Set this environment variable before you run anything.** This is the single most important step in the whole guide.

```bash
export AZSDKTOOLS_AGENT_TESTING=true
```

PowerShell:

```pwsh
$env:AZSDKTOOLS_AGENT_TESTING = "true"
```

This flag tells the agent that your session is bug-bash activity, **not a real release**. Without it, you can accidentally create real release plans, kick off real pipelines, and notify real partner teams — which pollutes production tracking and wastes other people's time chasing fake work. Set it in every shell you use, and put it in your MCP `env` block (below) too.

### Install the agent

Pick whichever option matches how you want to test. You can do more than one.

**Option A — Standalone CLI**

```bash
git clone https://github.com/Azure/azure-sdk-tools.git
cd azure-sdk-tools
./eng/common/mcp/azure-sdk-mcp.ps1 -UpdatePathInProfile
azsdk --version
```

**Option B — MCP server in VS Code (recommended for "agent" testing)**

Create or update `.vscode/mcp.json` in your test workspace. **Note `AZSDKTOOLS_AGENT_TESTING` is in the `env` block** — it has to be set here too, not just in your shell:

```json
{
  "servers": {
    "azure-sdk": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<path-to-azure-sdk-tools>/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj",
        "--",
        "mcp"
      ],
      "env": {
        "AZURE_DEVOPS_PAT": "<your-ado-pat>",
        "GITHUB_TOKEN": "<your-github-token>",
        "AZSDKTOOLS_AGENT_TESTING": "true"
      }
    }
  }
}
```

Reload VS Code, open Copilot Chat, and confirm the `azure-sdk` tools show up.

**Option C — GitHub Coding Agent**

Fork an Azure SDK language repo (e.g. `azure-sdk-for-net`). The repo already ships a `.vscode/mcp.json`. Open an issue in your fork and invoke the GitHub Coding Agent on it.

### Test workspace

Make a scratch directory you can throw away afterwards:

```bash
mkdir ~/azsdk-bugbash-workspace
cd ~/azsdk-bugbash-workspace
gh repo clone Azure/azure-sdk-for-net
gh repo clone Azure/azure-rest-api-specs
```

## Verification Checklist

Before you start scenarios, confirm all of these:

- [ ] `azsdk --version` prints a version number
- [ ] `azsdk --help` lists commands
- [ ] `gh auth status` shows you're authenticated
- [ ] You can `cd` into at least one cloned Azure SDK repo
- [ ] **`echo $AZSDKTOOLS_AGENT_TESTING` prints `true`** (and your `.vscode/mcp.json` `env` block has it too if you're using MCP)
- [ ] If using MCP: `azure-sdk` tools appear in VS Code Copilot Chat
- [ ] You know where you'll file feedback (see [How to File Feedback](#how-to-file-feedback))

If any of these fail, that's already feedback worth filing — setup friction counts.

## Scenarios to Try

Each scenario is a "Try this:" — pick whatever looks interesting and run it yourself. Aim for **3–5 scenarios** during the session, mixed across categories. If something blows up, file it (see the next section).

### TypeSpec

**Try this: Convert a Swagger to TypeSpec**

1. Find a service with Swagger but no TypeSpec yet (older Azure services often qualify).
2. Run `azsdk tsp convert --swagger-path <path-to-swagger> --output-dir <output-path>`.
3. Check that the generated TypeSpec compiles and looks reasonable compared to a hand-written one.
4. Push on it: try complex inheritance, custom `x-ms-*` extensions, discriminated unions.

**Try this: Generate an SDK from TypeSpec**

1. From inside a TypeSpec project, run `azsdk tsp client generate --language <lang> --typespec-project <path>`.
2. Test at least one of: .NET, Java, Python, JavaScript.
3. Build the generated SDK and see whether the build succeeds (or whether the error is actionable).
4. Try variations: brand-new project, regenerating an existing SDK, custom emitter options.

**Try this: Customized code update**

1. Generate an SDK (above), then manually edit some generated code (add a custom method or comment).
2. Run `azsdk tsp client customized-update --language <lang>`.
3. Confirm your customizations survived. Try forcing a conflict between your edits and a spec change.

**Try this: Find modified TypeSpec projects**

1. In `azure-rest-api-specs`, create a branch and modify 1–2 TypeSpec projects.
2. Commit and run `azsdk tsp project modified-projects`.
3. Verify only the projects you changed are listed — no false positives, no false negatives.

### SDK packages

**Try this: Check API spec readiness**

1. Open (or find) a PR in `azure-rest-api-specs` with TypeSpec changes.
2. Run `azsdk release-plan check-api-readiness --pr <number> --tsp-config <path>`.
3. Read the report. Is the feedback actionable when the spec isn't ready? Is it correct when the spec is ready? Try an incomplete spec, a bad `tspconfig.yaml`, and a spec with breaking changes.

**Try this: Analyze failed test cases**

1. Run SDK tests that fail and find the resulting `.trx` file.
2. Run `azsdk pkg test results --trx-file <path>`.
3. Can you actually figure out the root cause from the output? Try an empty TRX, a malformed TRX, and a huge TRX (100+ tests).

### Release plans *(internal Microsoft only)*

These need Architecture Board access. **Make sure `AZSDKTOOLS_AGENT_TESTING=true` is set** so you don't create a real release plan that someone has to clean up.

**Try this: Create a release plan**

1. Pick a TypeSpec project that's "ready" for SDK release.
2. Run `azsdk release-plan create --typespec-project <path> --service-id <id> --product-id <id>`.
3. Confirm the work item shows up in Azure DevOps with the right metadata. Try the variant where service/product are auto-discovered from just the TypeSpec project.

**Try this: Check release plan status**

1. Use the plan from the previous scenario, or pick an existing one.
2. Run `azsdk release-plan status --id <plan-id>`.
3. Does the status (Draft / In Progress / Released / Abandoned) match what's actually in ADO? Do the linked items resolve?

**Try this: Abandon a release plan**

1. Pick a test plan you created.
2. Run `azsdk release-plan abandon --id <plan-id>`.
3. Confirm it's marked Abandoned in ADO and that the audit trail is intact.

### Pipeline diagnostics

**Try this: Analyze a pipeline failure**

1. Find a recent failed pipeline run in ADO and grab its build ID.
2. Run `azsdk azp analyze --build-id <id>`.
3. Did it actually identify the root cause? Try one of each: compile failure, test failure, infra/timeout, config error.

**Try this: Analyze a log file**

1. Download a log from a failed pipeline run.
2. Run `azsdk azp log analyze --log-file <path>`.
3. Are the errors and warnings extracted with enough surrounding context? Are duplicates collapsed?

**Try this: Check pipeline status**

1. Run `azsdk azp status --build-id <id>` against a running and a completed build.
2. Is the status, duration, and summary right?

**Try this: Download test artifacts**

1. Run `azsdk azp test-results --build-id <id>`.
2. Confirm artifacts land in your local directory and match what's in ADO.

### APIView *(internal Microsoft only)*

**Try this: Get an APIView review URL**

1. Run `azsdk apiview get-review-url --package-name <name> --language <lang>`.
2. Open the URL — does it land on the right review?

**Try this: Pull APIView comments**

1. Pick a package with existing review comments.
2. Run `azsdk apiview get-comments --package-name <name> --language <lang>`.
3. Are author, timestamp, and resolved/unresolved status all there?

**Try this: Request a Copilot review**

1. Run `azsdk apiview request-copilot-review --api-text <text-or-url>`, save the job ID.
2. Poll with `azsdk apiview get-copilot-review --job-id <id>`.
3. Try a very large API surface, malformed text, and an unsupported language.

### CODEOWNERS

**Try this: Look up CODEOWNERS associations**

1. Run `azsdk config codeowners view --package <package-name>`.
2. Are the owners, labels, and paths all correct?
3. Try the variations: `--github-user <username>`, `--label <label>`, `--path <file-path>`.

**Try this: Refresh the CODEOWNERS cache**

> Make sure `AZSDKTOOLS_AGENT_TESTING=true` is set — this triggers a real ADO pipeline.

1. Run `azsdk config codeowners update-cache`.
2. Confirm the pipeline is triggered, watch it complete, and re-run the previous scenario to see updated data.

### Agent integration (MCP / GitHub Coding Agent)

**Try this: Ask the agent in VS Code Copilot Chat**

1. With the MCP server connected, open Copilot Chat.
2. Ask: *"What TypeSpec projects were modified in this branch?"*
3. Confirm it actually invokes the `azsdk_get_modified_typespec_projects` tool, and the answer is right.
4. More prompts to try:
   - *"Analyze the latest pipeline failure for this repo"*
   - *"Create a release plan for the TypeSpec project at specification/foo/bar"*
   - *"Get APIView comments for package Azure.Foo"*

**Try this: GitHub Coding Agent on an issue**

1. In your fork, open an issue and trigger the GitHub Coding Agent (label or `@-mention`, depending on repo).
2. Watch the workflow run. Did the agent use the right `azsdk` MCP tools? Did it produce something useful or ask sensible clarifying questions?
3. Sample issues to try:
   - *"Generate SDK for the TypeSpec project at specs/foo/Foo.Management"*
   - *"Analyze why the latest CI run failed"*
   - *"Add a CODEOWNERS entry for package Azure.Foo.Bar with owner @username"*

**Try this: Multi-step agent workflow**

Chain a real workflow end-to-end in Copilot Chat:

1. *"Convert the Swagger at specification/foo/stable/2023-01-01/swagger.json to TypeSpec"*
2. *"Generate the .NET SDK from the new TypeSpec"*
3. *"Build the SDK and report any compilation errors"*
4. *"Fix the compilation errors"*
5. *"Create a PR with these changes"*

Where does it lose the thread? Where does it shine? File both.

### Stress and edge cases

**Try this: Concurrent commands**

Run several different `azsdk` commands at once in different terminals. Watch for crashes, hangs, or interleaved output.

**Try this: Big inputs**

- Analyze a >50 MB log file
- Convert a Swagger with 100+ operations
- Generate an SDK for a service with 50+ models

Look for memory blowups, unreasonable runtime, or unclear timeouts.

**Try this: Bad inputs**

Throw garbage at it: non-existent paths, malformed TypeSpec, invalid build IDs, missing required parameters, wrong types. You want clear error messages, no exposed stack traces (unless verbose), and a non-zero exit code.

**Try this: Offline behavior**

Disconnect your network (or block specific endpoints) and run network-dependent commands. Are timeouts reasonable? Do error messages clearly say "network problem"? Do offline-capable commands still work?

## How to File Feedback

**File one GitHub issue per finding** in [`Azure/azure-sdk-tools`](https://github.com/Azure/azure-sdk-tools/issues/new/choose). One issue per bug — don't batch.

**Use this template** (copy/paste into the issue body):

```markdown
## Bug Bash Feedback

**Scenario:** <which "Try this:" you were running, or "ad-hoc">

**What I did:**
1. ...
2. ...

**What I expected:**
...

**What actually happened:**
...

**Environment:**
- OS: <Windows 11 / macOS 14 / Ubuntu 22.04>
- `azsdk --version`: <output>
- Mode: <CLI / MCP server / GitHub Coding Agent>
- Language SDK (if relevant): <.NET / Java / Python / JS>

**Logs / screenshots:**
<attach or paste>
```

**Labels to apply:**
- `bug-bash` (so we can find them all)
- `azsdk-cli`
- One of `bug` / `enhancement` / `documentation` / `question`
- A severity label if you can: `severity:critical`, `severity:high`, `severity:medium`, `severity:low`

**What makes a feedback issue great:**
- A repro another engineer can run without asking you anything
- The exact command, exact error, and exact `azsdk --version`
- Screenshots or attached log files instead of "it looked weird"
- Expected vs. actual stated explicitly — even if it feels obvious

If you're not sure whether something is a bug or "working as designed" — file it anyway. We'd rather close a few extra issues than miss real friction.

## Tips

- **Try things you'd never normally try.** The agent should handle weird, partial, or unrealistic inputs gracefully. Throw the dumb stuff at it.
- **File small papercuts.** Confusing wording, slightly-off help text, a missing example — those add up. Don't only file the big bugs.
- **Don't fix bugs, just file them.** Even if you know exactly what's wrong, filing the issue is more valuable than a private patch — it gives us coverage data.
- **Ask in chat if you're stuck.** Setup snags are themselves feedback (file the issue first, then ask).
- **Note things you liked.** "This was great" comments help us know what not to break.

---

*For organizers: see [bug-bash-organizer-notes.md](./bug-bash-organizer-notes.md).*
