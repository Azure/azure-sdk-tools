# Executive Summary

The plan is directionally sound, but it is not fully implementable as written. The biggest problems are an under-specified `Custom.MicrosoftIdentity` implementation, several rule mappings that do not match the current code paths closely enough to make `--fix` safe, and a contradictory exit-code contract.

# Feasibility Assessment

## Phase 1: Infrastructure & Data Fetching

- **Mostly feasible**
  - The CLI integration is straightforward: `CodeownersTool` already follows a helper-based pattern and is registered through DI (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/Config/CodeownersTool.cs:187-209`, `tools/azsdk-cli/Azure.Sdk.Tools.Cli/Services/ServiceRegistrations.cs:53-68`).
  - Bulk fetching is also feasible because `IDevOpsService.FetchWorkItemsPagedAsync` already exists and supports `WorkItemExpand.All` (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Services/DevOpsService.cs:762-794`).

- **Missing implementation detail**
  - The plan assumes delete support for orphaned Label Owner work items, but `IDevOpsService` has no generic `DeleteWorkItemAsync` surface today (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Services/DevOpsService.cs:89-122`). The only deletes in the file are hardcoded for release-plan flows (`.../DevOpsService.cs:469-473`).
  - This makes STR-001/002/005 fix behavior incomplete as planned.

## Phase 2: Audit Rule Engine

- **Feasible**
  - A rule engine abstraction is reasonable.
  - The main caution is that some rules need to run on a **mutated in-memory graph** after fixes, or they must refetch state after each fix batch. The plan says "re-evaluate structure rules after owner fixes" (`plan.md:205-207`) but does not specify whether this is against local state or ADO state.

- **Risk**
  - Without explicit state management, cascade rules will be flaky or double-report/delete the same Label Owner.

## Phase 3: Owner Validation Rules

### AUD-OWN-001 / AUD-OWN-003
- **Feasible, but unsafe as written**
  - Bulk owner validation is possible, but current GitHub validation behavior is not compatible with the proposed retry strategy.
  - `CodeownersValidatorHelper.ValidateCodeOwnerAsync` catches `RateLimitExceededException` and `SecondaryRateLimitExceededException` and converts them into error results instead of surfacing retriable exceptions (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Helpers/CodeownersValidatorHelper.cs:79-95`).
  - So "batch + retry with backoff" (`plan.md:288`) cannot be implemented cleanly unless the helper changes first.

### AUD-OWN-004
- **Not feasible as written**
  - `OwnerWorkItem` has no `Custom.MicrosoftIdentity` property (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Models/AzureDevOps/OwnerWorkItem.cs:11-31`).
  - `WorkItemMappers.MapToOwnerWorkItem` does not read that field (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Models/Codeowners/WorkItemMappers.cs:37-44`).
  - `IDevOpsService.UpdateWorkItemAsync` only accepts `Dictionary<string, string>` (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Services/DevOpsService.cs:1480-1495`), but the plan itself notes that ADO Identity fields require structured identity payloads, not plain strings (`plans/AUD-OWN-004-msft-identity-missing.md:33-38`).
  - `GitHubToAADConverter` only returns a UPN string (`tools/identity-resolution/Helpers/GitHubToAADConverter.cs:41-50`), not an ADO descriptor or `IdentityRef`.

## Phase 4: Label & Structure Rules

- **Partially feasible**
  - Basic count-based rules are easy to implement against hydrated work items.
  - The problem is correctness: several of these rules do not line up with how labels and owners are actually emitted.

## Phase 5: Rule Ordering & Integration

- **Conceptually correct**
  - Running owner rules before structure rules is the right idea (`plan.md:201-207`).

- **Missing**
  - No dedupe strategy for a Label Owner that is:
    - emptied by owner-fix,
    - already missing labels,
    - and then considered by STR-001, STR-002, and STR-005 in the same run.

## Phase 6: Testing

- **Incomplete**
  - The test plan is missing:
    - identity field serialization tests against a real/sandbox ADO field,
    - mixed transient GitHub failures vs truly invalid owners,
    - duplicate-delete / repeated-fix cases,
    - package-side `Service Attention` misuse,
    - existing bad `RepoPath` values already in ADO.

# Correctness Review

## Rule mappings

### 1. AUD-OWN-001 is stricter than the mapped linter rules
- The linter docs map OWN-003 to **public Azure membership** (`rules/OWN-003-user-public-azure-member.md:7-18`).
- `CodeownersValidatorHelper` requires membership in **both** `Microsoft` and `Azure` orgs (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Helpers/CodeownersValidatorHelper.cs:28-32`, `:44-57`).
- That is not the same rule.
- If the audit is supposed to mirror linter parity, this is overreach. If it is intentionally stricter, the plan should say so explicitly.

### 2. AUD-OWN-001 uses a fixed repo permission proxy, not "the relevant repo"
- The detailed rule says "write permission on the relevant repo" (`plans/AUD-OWN-001-invalid-owner.md:13`).
- The actual helper checks only `Azure/azure-sdk-for-net` (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Helpers/CodeownersValidatorHelper.cs:54-57`).
- For a global audit that then removes relations across all repos, that is a major policy assumption and should be documented.

### 3. AUD-OWN-002 is narrower than OWN-004
- `OwnerWorkItem.IsGitHubTeam` only returns true if the alias contains `/` (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Models/AzureDevOps/OwnerWorkItem.cs:17-19`).
- The plan for AUD-OWN-002 only evaluates owners where `IsGitHubTeam == true` (`plans/AUD-OWN-002-malformed-team.md:7-9`).
- But the linter's malformed-team logic can still detect bare team slugs, because `OwnerDataUtils.IsWriteTeam()` accepts both `Azure/<team>` and plain `<team>` (`tools/codeowners-utils/Azure.Sdk.Tools.CodeownersUtils/Utils/OwnerDataUtils.cs:71-79`).
- So malformed ADO values like `sdk-write-core` would be reclassified as "invalid user" instead of "malformed team".

### 4. AUD-LBL-002 misses package PR labels
- The detailed rule plan only inspects **Label Owner** work items (`plans/AUD-LBL-002-service-attention-misuse.md:7-13`).
- But Package work items carry PR labels in generation (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Helpers/CodeownersGenerateHelper.cs:203-214`).
- The same plan file says the rule is about "Label Owner or Package entries" (`plans/AUD-LBL-002-service-attention-misuse.md:3-5`).
- As written, the rule would miss `Service Attention` misuse directly on Packages.

### 5. STR-005 mixes linter semantics with check-package semantics
- The main plan correctly distinguishes:
  - linter minimum = 1 owner (`plan.md:99-105`)
  - check-package minimum = 2 individual owners
- But STR-005 says its purpose is to prevent the generated CODEOWNERS from failing the linter's minimum-owner checks while using the **2-owner** threshold (`plans/AUD-STR-005-orphaned-label-owner.md:20-22`, `:9-17`).
- That is incorrect.
- `CodeownersEntry.FormatCodeownersEntry()` only drops a path block when source owners go to **zero**, not one (`tools/codeowners-utils/Azure.Sdk.Tools.CodeownersUtils/Parsing/CodeownersEntry.cs:236-303`).

## Data model assumptions

### `Custom.MicrosoftIdentity`
- The plan correctly calls out serialization risk (`plans/AUD-OWN-004-msft-identity-missing.md:33-38`), but it still treats the feature as a normal model-field addition (`plan.md:186-187`).
- The current update API is string-only (`.../DevOpsService.cs:1480-1495`), so this is not just "add one field".

### Label existence
- The plan says to check whether labels exist in "target GitHub repos" (`plans/AUD-LBL-001-label-not-in-github.md:6-9`).
- But `IGitHubService` currently has no repo-label enumeration API (`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Services/GitHubService.cs:100-126`).
- So the rule is not grounded in current service capabilities.

# Pitfalls & Risks

## Critical

### 1. `--fix` can make destructive bulk changes based on brittle validation
- Invalid-owner fixes remove relations from every linked Package/Label Owner (`plans/AUD-OWN-001-invalid-owner.md:15-20`, `plans/AUD-OWN-003-team-not-write.md:12-15`).
- But the validator:
  - is stricter than the linter,
  - uses a fixed repo permission proxy,
  - and converts rate-limit failures into "error" results rather than retriable conditions.
- Consequence: a bad auth token, transient GitHub issue, or policy mismatch could sever valid ownership relations across many repos.

### 2. `Custom.MicrosoftIdentity` is not well-understood enough for implementation
- Current code has no end-to-end example of writing an ADO Identity field.
- The converter only gives UPNs.
- The update API only writes strings.
- Consequence: Phase 3 can stall or produce invalid ADO payloads late in the effort.

## High

### 3. Project reference to `identity-resolution` is dependency-risky
- `azsdk-cli` uses stable 19.225.1 DevOps packages (`Azure.Sdk.Tools.Cli.csproj:49-51`).
- `identity-resolution` uses 19.239.0-preview packages (`tools/identity-resolution/identity-resolution.csproj:9-13`).
- Consequence: restore/build conflicts or subtle runtime behavior differences.

### 4. Current rule ordering does not define duplicate-delete behavior
- STR-001/002/005 can all converge on the same Label Owner after owner removals.
- Consequence: duplicate delete attempts, noisy reporting, or non-deterministic fix output.

## Medium

### 5. Exit-code contract is contradictory
- "`--fix` exits 0 if all fixable violations were resolved" (`plan.md:126-127`)
- vs "0 = no violations, 1 = violations found" (`plan.md:209-213`)
- Consequence: consumers cannot rely on the CLI contract.

### 6. Re-implement vs reuse is mostly right, but too absolute
- Re-implementing the audit against ADO models is reasonable (`plan.md:240-259`).
- But there are non-trivial pieces that should be reused, not rewritten:
  - `OwnerDataUtils.IsWriteTeam()` semantics for malformed-team detection (`.../OwnerDataUtils.cs:71-79`)
- Consequence: subtle rule drift from the linter.

# Gaps & Missing Items

- Missing **generic ADO delete API** in `IDevOpsService`.
- Missing plan to extend `IGitHubService` with **repo label enumeration/cache** for AUD-LBL-001.
- Missing explicit plan for **IdentityRef/descriptor lookup** for `Custom.MicrosoftIdentity`.
- Missing distinction between:
  - **deterministic invalid owners**,
  - **transient GitHub/API failures**,
  - **policy-only failures**.
- Missing package-side checks for `Service Attention` misuse.
- Missing test cases for:
  - repeated fixes on the same WI,
  - partially applied fixes,
  - sandbox ADO identity writes,
  - fixed-repo permission proxy behavior.

# Recommendations

1. **Split AUD-OWN-004 into a prerequisite spike**
   - First prove:
     - how to resolve ADO identity descriptors,
     - how to PATCH an Identity field,
     - and whether `IDevOpsService` needs an object-valued update API.
   - Do not treat this as a normal rule implementation task.

2. **Tighten `--fix` safety**
   - Only auto-remove relations for **deterministic** failures.
   - Treat rate limits, GitHub 5xx, auth issues, and ambiguous states as report-only.
   - Strongly consider a scoped mode before global bulk mutation.

3. **Document the permission policy explicitly**
   - If `azure-sdk-for-net` write permission is the org-wide proxy, say so.
   - If not, the helper must change before the audit is correct.

4. **Broaden AUD-LBL-002**
   - Check both:
     - Package PR labels,
     - Label Owner service labels.

5. **Use selective reuse, not blanket reimplementation**
   - Rebuild the audit engine on ADO models, but reuse existing linter utility logic where semantics are non-obvious.

6. **Resolve the exit-code contract now**
   - Decide whether `--fix` reports:
     - success when all fixable issues were fixed,
     - or failure when any violations remain.
   - Encode that explicitly in the response model.

7. **Add a dedicated dependency-risk section to the plan**
   - GitHub API rate limits
   - Open Source Portal availability
   - ADO Graph/Identity APIs
   - cross-project package-version compatibility
