# User Org cache rewrite plan

## Problem
`github-team-user-store` currently mixes two responsibilities: it builds cache content and uploads that content to blob storage while running. We want to keep the tool focused on **building cache files locally**. The user-org visibility cache should still switch from per-user GitHub `CheckMemberPublic` calls to the `go.ps1` bulk `public_memberships` flow, but the tool should stop uploading caches itself and instead write cache files for a separate upload step.

## Scope
- Change the **generation path only** in `tools/github-team-user-store`.
- Preserve the existing consumer contract in `tools/codeowners-utils`.
- Keep team hierarchy and repo-label generation on their current source APIs.
- Change the tool output contract from **blob URIs** to **local cache files** for all generated caches.

## Non-goals
- Do not change the blob schema read by `UserOrgVisibilityCache`.
- Do not move this logic into runtime consumers.
- Do not implement upload inside `github-team-user-store`.
- Do not design the separate upload step here; it will consume the files this tool writes.
- Do not add a fallback from the Open Source API back to per-user GitHub membership checks. If the new source fails, fail the run clearly.

## Observed runtime measurements
- These were observed from a live run and should be revalidated during implementation; they are not derivable from repository contents alone.
- `go.ps1` returns `200 OK` from `public_memberships` with a JSON array shaped like `[{ "login": "...", "id": ..., "avatar_url": "..." }]`.
- The current `azure-sdk-write` team blob contains 2528 write users.
- Building `user -> publicMemberships.Contains(user)` from the bulk endpoint exactly matches the current stored `user-org-visibility-blob` today: same 2528 keys, same 1265 `true` values, zero diffs.
- Reproduce this before rollout by fetching the live `public_memberships` payload and comparing the projected dictionary to the current `user-org-visibility-blob`.

## Files expected to change
- `tools/github-team-user-store/GitHubTeamUserStore/GitHubTeamUserStore/Program.cs`
- `tools/github-team-user-store/GitHubTeamUserStore/GitHubTeamUserStore/TeamUserGenerator.cs`
- `tools/github-team-user-store/GitHubTeamUserStore/GitHubTeamUserStore/RepositoryLabelGenerator.cs`
- `tools/github-team-user-store/GitHubTeamUserStore/GitHubTeamUserStore/GitHubEventClient.cs` or a new adjacent helper for the Open Source API
- `tools/github-team-user-store/GitHubTeamUserStore/GitHubTeamUserStore/GitHubTeamUserStore.csproj`
- `eng/pipelines/pipeline-owners-extraction.yml` to switch the tool invocation to local file output
- `tools/github-team-user-store/README.md`

## Design constraints from skill rules
- Keep the design **simple and direct**: one bulk fetch, one set build, one dictionary projection, one local file write per cache.
- Keep file I/O, credential acquisition, and HTTP fetch at the workflow edge; helper methods should operate on explicit inputs and outputs.
- Preserve existing behavior unless the contract is intentionally changing.
- Fail fast on bad auth, bad HTTP responses, malformed payloads, invalid output paths, or parity mismatches, and do not add retries or recovery logic around API calls.
- Keep documentation and config examples aligned with actual file paths, commands, and parameter names.
- Reuse the existing non-managed-identity Azure credential pattern already used by `github-team-user-store` in CI, rather than introducing `ManagedIdentityCredential` into this tool's pipeline path.
- Use a single required output directory with fixed file names, rather than three separate output-path options, so the later upload step has a simple and stable contract.

## Output contract
- Replace the three blob-URI output options with one required `--outputDirectory` option.
- The tool writes these files into that directory:
  - `azure-sdk-write-teams-blob`
  - `user-org-visibility-blob`
  - `repository-labels-blob`
- These names intentionally match the current blob names so the later upload step can map files to blobs without translating names.

## Algorithm
1. Replace the CLI output contract in `Program.cs`:
   1. Remove the three required blob-URI options.
   2. Add one required `--outputDirectory` option.
   3. Pass that directory through the workflow to both cache generators.
2. Keep the existing team-user generation flow unchanged until `teamUserDict` has been built.
3. Add an Open Source API fetch path in `github-team-user-store` that:
   1. Creates a `TokenCredential` with Azure Identity using a chain that matches this tool's current CI-safe behavior: prefer environment, Visual Studio, Azure CLI, Azure PowerShell, and interactive browser credentials, and do not introduce `ManagedIdentityCredential` into the generator path.
   2. Requests a token for the Open Source API with scope `api://2efaf292-00a0-426c-ba7d-f5d2b214b8fc/.default`.
   3. Sends `GET https://repos.opensource.microsoft.com/api/organizations/Azure/public_memberships` with `Authorization: Bearer <token>`.
   4. Deserializes the response into a minimal model containing `login`.
   5. Builds a case-insensitive `HashSet<string>` of public Azure member logins.
   6. Does not add retries, backoff, or local recovery logic around the API call; token acquisition, request, and deserialization failures should bubble to the top-level CLI entrypoint.
4. Rewrite `GenerateAndStoreUserOrgData` so it:
   1. Reads the distinct write-user list from `teamUserDict["azure-sdk-write"]`.
   2. Projects `Dictionary<string, bool>` as `writeUser -> publicMemberSet.Contains(writeUser)`.
   3. Serializes the exact same JSON shape used today.
   4. Writes that JSON to `<outputDirectory>/user-org-visibility-blob`.
   5. Re-reads the written file and verifies its contents against the in-memory dictionary.
5. Rewrite the team-user and repo-label generators to follow the same local-output pattern:
   1. Serialize their current JSON shapes unchanged.
   2. Write to `<outputDirectory>/azure-sdk-write-teams-blob` and `<outputDirectory>/repository-labels-blob`.
   3. Re-read the written files and verify them against the in-memory data structures.
6. Remove or stop using the blob upload/readback helpers from the generator flow.
7. Update docs/comments to explain that:
   1. GitHub still supplies the write-team hierarchy and repo labels.
   2. The Open Source API now supplies the public Azure membership list.
   3. The tool now emits local cache files and does not upload them.
   4. The tool requires Azure credentials for the Open Source API, but no longer needs blob-upload credentials itself.
8. Update the pipeline/tool invocation so `github-team-user-store` runs with an output directory instead of blob URIs.
   1. The tool step should run under the identity needed for the Open Source API.
   2. The later upload step is outside this plan's scope and will consume the generated files.

## Exit codes
- `0`: all cache files are generated and locally verified successfully.
- `non-zero`: any token acquisition failure, HTTP failure, deserialization error, malformed payload, file write error, invalid output path, or verification mismatch.

## Error handling
- Do not catch token acquisition, HTTP request, or JSON deserialization failures below the top-level CLI flow just to recover or continue.
- Let API call failures bubble to the top-level CLI entrypoint and terminate the run with a failure.
- Treat missing or blank `login` fields as a payload error, not as ignorable records.
- Do not log bearer tokens.
- Do not silently fall back to GitHub per-user membership checks.
- Create the output directory if it does not exist.
- Fail immediately if the output directory path is invalid, points at a file, or a cache file cannot be written.
- Re-read each generated file and fail the run if the file contents differ from the in-memory dictionary/list used to create it.

## Edge cases
- The public-membership payload includes users outside `azure-sdk-write`: ignore them.
- A write user missing from the public-membership payload must still appear in the output dictionary with value `false`.
- Case differences between `azure-sdk-write` users and returned `login` values must still match correctly.
- Empty payloads, or payloads whose entries do not yield any valid `login` values, should fail rather than producing an all-false blob.
- The Open Source API contract may drift; missing `login` fields should be treated as a hard error.
- The output directory may already contain prior files; overwrite deterministically.
- The tool should not partially succeed silently: if one cache file fails generation or verification, the run exits non-zero.

## Multi-agent review workflow
- `./plan.md` is the only planning source of truth.
- The review agent should read `./plan.md` and the cited code directly, with no prior conversational context.
- Review feedback should be organized as `Critical`, `Important`, and `Nice-to-have`.
- If work is handed off later, the implementation agent should start by reading `./plan.md`; if another handoff is needed after implementation, write `./continue.md` in the repo root.

## Test matrix
| Scenario | Expected result |
|---|---|
| Token acquisition succeeds | Open Source API request is authorized and returns JSON |
| Public member appears in `azure-sdk-write` | Output dictionary contains that user with `true` |
| Non-public write user | Output dictionary contains that user with `false` |
| Public-membership payload contains extra users | Extra users are ignored and not added to the dictionary |
| Case differences in usernames | Matching remains case-insensitive |
| Token acquisition fails | Run exits non-zero with clear auth failure |
| Open Source API returns non-200 | Run exits non-zero with response context |
| Open Source API returns malformed JSON or missing `login` | Run exits non-zero without writing a replacement visibility file |
| Output directory does not exist | Tool creates it and writes all three files |
| Output path is invalid or unwritable | Run exits non-zero with clear file-system failure |
| Re-read verification fails for any generated file | Run exits non-zero |
| Current live payload compared against current stored blob | Generated dictionary matches existing blob exactly |

## Validation plan
1. Build `tools/github-team-user-store/GitHubTeamUserStore/GitHubTeamUserStore/GitHubTeamUserStore.csproj`.
2. Verify the tool writes the three expected files with the exact stable names in the configured output directory.
3. Re-read the generated files and compare them to the in-memory data structures; expected result is exact parity.
4. Recompute the visibility dictionary from the live `public_memberships` payload and compare it against the current `user-org-visibility-blob`; expected result is exact parity.
5. Validate the updated tool invocation in `eng/pipelines/pipeline-owners-extraction.yml` against the actual step names, service connections, and output-directory contract.
6. Update `tools/github-team-user-store/README.md` so it describes file output instead of in-tool blob upload and no longer implies SAS-token-based upload behavior.
7. Run any existing tests for that tool if present; if no test project exists, validate the owned boundary with targeted execution plus parity checks.

## Stop point
Do not implement until the user explicitly says to proceed.
