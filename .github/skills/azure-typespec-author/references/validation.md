# Validation

Run all sub-steps in order after applying changes. Run **5.1 General Validation** for every case; run **5.2 Case-Specific Validation** only when the case matches.

| Sub-step | Action                                        | When                    |
| -------- | --------------------------------------------- | ----------------------- |
| 5.1.1    | `azure-sdk-mcp:azsdk_run_typespec_validation` | Always                  |
| 5.1.2    | `tsp compile .`                               | Always                  |
| 5.2      | Case-specific validation                      | Case matches (see §5.2) |

---

## 5.1 General Validation (All Cases)

### 5.1.1: TypeSpec Validation

Invoke `azure-sdk-mcp:azsdk_run_typespec_validation` with the project root. On failure → fix → re-run. Limit to 3 retry attempts; if still failing after 3 retries, stop and report the remaining errors to the user.

### 5.1.2: Compile

Run `tsp compile .` from the project root. Verify `.json` output under the directory specified by the `@azure-tools/typespec-autorest` entry in the project's tspconfig.yaml. Fix compile errors if any.

> 5.1.1 checks for errors/warnings; 5.1.2 generates the OpenAPI output. Both are required.

---

## 5.2 Case-Specific Validation

Run the sub-section matching the case identified in [intake.md](intake.md) §2.1. If no case matches, skip this step — 5.1 is sufficient.

### Case 3 — API Version Evolution (ARM / Data-plane)

Run 5.2.1 and 5.2.2 after 5.1, in order.

#### 5.2.1: Example Verification

Verify `{project-root}/{version-status}/{target-version}/examples/` exists with `.json` files using the correct `api-version`. If missing, copy from the previous version's examples and update `api-version`. Skip example verification for XML-based specs, as the tooling does not support examples for XML specifications.

Verify that any example folder for an API version that no longer exists in the `Versions` enum has been deleted. For each folder under `{project-root}/{version-status}/`, check that the folder name matches an entry in the `Versions` enum. If a folder exists for a removed version, delete it.

#### 5.2.2: Version-Evolution Consistency Loop

It is a loop: run all checks, and if any fails, fix the code and re-run from 5.1. Repeat until every check passes (max 3 iterations; if still failing, stop and report).

1. **Enumerate versions.** List every entry in the `Versions` enum (e.g. `v2025_05_04_preview`).
2. **No dangling version references.** Every version identifier used in a versioning decorator argument (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`, `@madeOptional`, `@returnTypeChangedFrom`, etc.) across all `.tsp` files **must** exist in the `Versions` enum. If a decorator references a version that is no longer in the enum (because a previous version was superseded/renamed rather than kept), rebase that decorator to the correct current version or remove it — do not leave the stale reference.
3. **Superseded version fully removed.** If a previous version was superseded/renamed (its enum entry no longer present), confirm there are **no** remaining occurrences of that old version identifier anywhere in the `.tsp` files, and that its example folder has been deleted (per 5.2.1).
4. **Carried-over vs. excluded features.** Confirm every feature the user chose to carry over is present in the new version, and every feature the user chose to exclude is not reintroduced (including any transitional decorator scaffolding — e.g. a property whose added default value was excluded must end up as a plain optional property, not a decorator-bridged rename).
5. **Re-validate.** Re-run 5.1.1 and 5.1.2 and confirm both pass.

If any check fails, apply the fix and re-run the loop from 5.1.
