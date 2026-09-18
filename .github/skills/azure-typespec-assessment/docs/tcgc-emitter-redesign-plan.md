# In-Memory TCGC Assessment Emitter Redesign

Status: proposed; this document does not implement or switch the runtime.
Date: September 17, 2026.

## Decision

Build a dedicated assessment emitter that receives a TypeSpec `Program`,
creates a TCGC `SdkContext` in memory, and exports deterministic source, HTTP,
and SDK evidence. Keep `source/source-index.json` as the coordinator's
canonical source-evidence entry point.

**Do not replace AutoRest with `SdkHttpOperation` alone.** Use three related
views from the same compilation:

| View | Authority | Purpose |
| --- | --- | --- |
| Source | Original compiler `Program`, semantic types, source locations, and Git hunks | Changed declarations, documentation presence, decorators, references, and source ownership |
| HTTP | In-memory AutoRest documents plus version-selected compiler HTTP operations and wire types | REST comparison, including operations not represented in the SDK, with source associations |
| SDK | In-memory `sdkContext.sdkPackage` | Language-neutral SDK methods, models, serialization, paging, and LRO contracts |

Generate all three views in memory from one compiler `Program`. Use AutoRest's
public `getAllServicesAtAllVersions(program, options)` API for normalized REST
documents without invoking its file-writing wrapper, `@typespec/http` for the
complete version-selected operation inventory and source associations, and
TCGC for the SDK package. This avoids standalone emitter processes, Swagger
output-file round trips, and TCGC YAML.

This is an evidence-extraction redesign. It does not redesign semantic
grouping, candidate rules, Agent judgments, or report presentation at the
same time.

## 1. Current implementation

Paths below are relative to the skill directory.

| Stage | Current implementation | Consequence |
| --- | --- | --- |
| Git evidence | `scripts/source-index.mjs:218-287`, `buildSourceIndex` | Captures revisions, hunks, provisional declarations, and source IDs |
| Compiler source evidence | `scripts/source-index.mjs:421-672`, `addCompilerEvidence` | Independently compiles base and current source with `noEmit`; resolves declarations, references, resource models, and effective documentation |
| Version selection | `scripts/api-version-selection.mjs:44-131` | Chooses source revision and API version separately for baseline and target |
| Contract emission | `scripts/compiler-runner.mjs:125-193`, `runProjectCompilers` | Runs AutoRest and TCGC separately for each comparison role |
| Wire normalization | `scripts/autorest-contract.mjs` | Loads Swagger, resolves references and `allOf`, and normalizes operation contracts |
| SDK normalization | `scripts/tcgc-contract.mjs:904-1032` | Parses cyclic/aliased YAML, then calls the already object-based `normalizeTcgcPackage` |
| Semantic analysis | `scripts/analyze-semantic-intents.mjs` | Joins source evidence to AutoRest operations, with TCGC identity resolution, then groups hunks |
| Compatibility analysis | `scripts/analyze-rest-breaking.mjs`, `scripts/analyze-downstream-breaking.mjs` | Compares normalized contracts and emits candidates, not final findings |

For an ordinary affected project this means six compiler invocations:
two source-evidence compilations plus two emitters for each of two roles.
These are invocation counts, not a claim that compiler work accounts for all
elapsed time.

There is already a useful reuse boundary: `normalizeTcgcPackage(root)` takes
an object. The new emitter should reuse its semantics rather than write a
second SDK normalizer. Its compatibility with live objects, including Maps
and raw TypeSpec references, must still be exercised.

## 2. What the upstream APIs establish

The Java example creates its context using:

```ts
this.sdkContext = await createSdkContext(
  this.emitterContext,
  LIB_NAME,
  sdkContextOptions,
);
this.program.reportDiagnostics(this.sdkContext.diagnostics);
```

That is the correct construction pattern, but Java's emitter identity and
customization options are not appropriate defaults for this assessment.

The upstream sources establish the following:

- `createSdkContext` accepts an `EmitContext`, builds `sdkPackage`, and exports
  YAML only when `exportTCGCoutput` is enabled.
- `SdkHttpOperation` includes `path`, `uriTemplate`, `verb`, parameters,
  `bodyParam`, normal responses, exceptions, and `__raw: HttpOperation`.
- SDK methods can refer to raw compiler operations; SDK types and properties
  can refer to raw compiler types. Some raw references are optional.
- The standalone TCGC YAML exporter intentionally removes `__*` fields.
  Keeping the graph in memory preserves source-linking opportunities that the
  current YAML boundary loses.
- Public compiler HTTP APIs expose service-wide operation enumeration.
  Enumeration must not be limited to SDK clients or convenience methods.
- TCGC performs versioning mutations. Calling `getAllHttpServices` on the
  original, unversioned program is not automatically equivalent to the
  selected SDK version.

### AutoRest also exposes an in-memory integration API

The package root publicly exports all of the following at the pinned revision:

| API | Role |
| --- | --- |
| `resolveAutorestOptions(program, emitterOutputDir, options)` | Resolves defaults, paths, and emitter settings |
| `getAllServicesAtAllVersions(program, resolvedOptions)` | Builds document records, including version projections and split feature documents |
| `getOpenAPIForService(context, documentOptions)` | Lower-level document generation for an already prepared service context |
| `sortOpenAPIDocument(document)` | Materializes late-bound references and returns the sorted plain document |
| `$onEmit(context)` | Normal emitter entry point; includes file emission and is not the preferred API here |

The construction path is:

```text
$onEmit
  -> resolveAutorestOptions
  -> emitAllServiceAtAllVersions
       -> getAllServicesAtAllVersions
            -> version mutation / getOpenAPIForService
            -> document records returned in memory
       -> emitOutput                         [do not call]
       -> emitServiceYaml                    [do not call]
```

`getAllServicesAtAllVersions` takes an existing `Program`; it does not call
`compile`. Its `version` option filters versioned service snapshots despite
the function's "all versions" name. Results are a discriminated union:
unversioned records contain a document directly; versioned records contain
`versions`. Each document record includes `document`, `outputFile`, `context`,
optional `feature`, and examples.

Illustrative integration, not a tested emitter implementation:

```ts
import {
  getAllServicesAtAllVersions,
  resolveAutorestOptions,
  sortOpenAPIDocument,
} from "@azure-tools/typespec-autorest";
import { getHttpService } from "@typespec/http";

const options = resolveAutorestOptions(
  context.program,
  logicalAutorestOutputDir,
  { ...effectiveAutorestOptions, version: selectedVersion },
);
const services = await getAllServicesAtAllVersions(context.program, options);

for (const service of services) {
  const records = service.versioned ? service.versions : [service];
  for (const record of records) {
    if (record.feature !== undefined) {
      record.context.proxy?.setCurrentFeature(record.feature);
    }
    const document = sortOpenAPIDocument(record.document);
    // Add this plain object and record.outputFile to the in-memory registry.
    const [httpService, diagnostics] = getHttpService(
      context.program,
      record.context.service.type,
    );
    context.program.reportDiagnostics(diagnostics);
    // Index httpService.operations against compiler source declarations.
  }
}
```

In production, deduplicate HTTP enumeration across feature records sharing
the same selected namespace. Check compiler diagnostics before and after
generation, reject missing selected versions, and publish only validated
snapshots. The sketch omits those guards and the coordinator's registry.

Important details:

- Use `record.context.service.type` for the selected namespace, not the
  original top-level service record. AutoRest prepares its versioned service
  context before document generation.
- **Set the current feature before materializing each document.**
  AutoRest's own `emitOutput` does this because `LateBoundReference.toJSON()`
  can produce different local/cross-file references for each feature.
- `sortOpenAPIDocument` internally performs JSON stringify/parse to resolve
  `toJSON` objects. This is in-memory serialization, not zero serialization.
  Do not hand unresolved reference objects to the existing normalizer.
- Reuse `normalizeAutorestDocuments(entries)` with these plain objects and a
  logical output-path registry. It already accepts documents directly.
  Preserve all split/common documents needed for reference resolution.
  Logical paths are reference-resolution bases, not claims that files exist;
  evidence links must resolve through the persisted snapshot.
- This removes output writes, not all input reads: AutoRest still loads
  examples and may need referenced resources. `skip-example-copying` does
  not disable example loading. Preserve existing example options first.
- AutoRest already uses `@typespec/http` internally, including `getHttpService`,
  `createMetadataInfo`, and request visibility resolution. Calling those APIs
  alone does not reproduce its schema naming, reference, versioning,
  extension, and feature-splitting policies.
- AutoRest creates its own lightweight TCGC context with the `autorest`
  emitter scope. Share the compiler `Program`, **not** the SDK context:
  replacing AutoRest's scoped context with the assessment SDK context could
  change the emitted contract.
- AutoRest also filters operations by its emitter scope. Keep the raw HTTP
  inventory distinct from both the AutoRest and SDK inventories; preserve
  current REST scope rather than silently expanding it during migration.

This confirms an exported source-level integration point. Same-program
AutoRest/TCGC ordering, installed-version availability, diagnostics, and
contract parity remain requirements for the feasibility experiment.

## 3. Proposed pipeline and artifact boundary

```text
Git comparison / sparse worktrees / locked toolchain
                       |
          changed files + hunks + revision hashes
                       |
          existing API-version pair selection
                       |
       assessment emitter per compilation snapshot
                       |
          original compiler source evidence
                       |
           shared compiler Program
        +--------------+----------------+
        |                               |
 getAllServicesAtAllVersions       createSdkContext
        |                               |
 AutoRest document records         SDK package
        |                               |
 materialize documents            SDK normalization
 + @typespec/http inventory              |
        |                               |
 existing AutoRest normalization         |
        +--------------+----------------+
                       |
  projects/<project>/<snapshot>/source-index.json
                       |
     coordinator validates and merges source evidence
                       |
           source/source-index.json
       + normalized snapshot references in manifest
                       |
   existing semantic / REST / downstream analyzers
                       |
 existing bounded inputs / Agent judgment / finalization
```

There is **one new emitter implementation**,
`@azure-tools/typespec-assessment-emitter` (proposed private package name).
The coordinator invokes that same emitter separately for each project's
baseline/target compilation snapshot. AutoRest and TCGC are library calls
inside it, not separately invoked output-producing emitters.

Each invocation writes to its assigned snapshot directory, for example:

```text
projects\<project>\baseline-<snapshot-id>\source-index.json
projects\<project>\target-<snapshot-id>\source-index.json
```

After the required invocations finish, the coordinator merges their source
evidence and writes the single root `source\source-index.json`. Only the
coordinator writes that root file; repeated invocations of the same emitter
must not overwrite or append to it. Run snapshots sequentially initially;
this ownership rule does not require concurrent execution.

### Connection to the existing E2E workflow

The public workflow entry point remains `run-assessment-analysis.mjs`;
the user does not run the new emitter manually. The planned call chain is:

```text
run-assessment-analysis.mjs --pr <PR> --repo <repo> --output <work>
  -> prepareAssessment(...)
     -> existing PR/Git/worktree/toolchain/version preparation
     -> compiler-runner: integrated backend
        -> compile each required snapshot with the assessment emitter
           -> $onEmit(context)
              -> compiler source evidence
              -> AutoRest document-generation API, in memory
              -> @typespec/http operation inventory
              -> TCGC createSdkContext, in memory
              -> snapshot source-index.json
     -> merge snapshot source evidence into root source-index.json
     -> write preparation-manifest.json with snapshot references
  -> existing deterministic dimension analyzers and bounded model-input.json
  -> existing Agent judgment and guarded finalization, for full assessments
```

The integrated compiler runner replaces the current separate AutoRest/TCGC
invocations with this emitter. `prepareAssessment` also stops independently
recompiling source evidence already provided by an emitter snapshot. The
coordinator and analyzers remain responsible for comparisons across snapshots;
one emitter invocation only extracts evidence from its own program.

For the PR 43718 benchmark, use only the deterministic portion of this chain.
Do not trigger the Agent/Guidelines/final-report stages. Backend selection and
a deterministic-only boundary must be wired explicitly when implementation
starts; this diagram does not claim those new CLI options already exist.

The optional HTTP-only backend later replaces AutoRest generation and
normalization with the direct HTTP projection; downstream artifact boundaries
stay the same.

The root index retains `sourceChanges`, `referencedDeclarations`,
`resourceModels`, and compiler/documentation status. It adds references to
the per-snapshot indexes. Each snapshot index contains:

- snapshot provenance and diagnostics;
- source declarations and source-to-contract associations;
- normalized HTTP evidence, identifying whether AutoRest or the direct HTTP
  projection produced it;
- the normalized SDK contract.

Use a versioned envelope, not a dump of `SdkContext`. Do not serialize the
compiler program, AST parents, raw pointers, private caches, full source
files, or examples. Preserve shared identities and cycle boundaries.
Keep the existing normalized SDK shape initially; a new graph-compression
format is not required for the first migration.

An illustrative root-index addition is:

```json
{
  "schemaVersion": 2,
  "compilerSnapshots": [
    {
      "snapshotId": "snapshot-...",
      "projectId": "project-...",
      "sourceRevision": "current",
      "apiVersion": "2026-03-01",
      "path": "projects/project-.../snapshot-.../source-index.json",
      "contentHash": "sha256:..."
    }
  ]
}
```

Artifact JSON paths above retain the repository's existing portable slash
format. Runtime filesystem operations continue to use `path.join`.

### Provenance and identity requirements

Every snapshot must record source commit, working-tree overlay digest when
applicable, selected service/version map, project, toolchain lock hash, actual
package versions, emitter version, effective compiler/emitter options, and
configuration hash. Baseline/target are references to snapshots, not synonyms
for base/current source.

Retain existing source/hunk/declaration IDs during the adapter migration.
Add fully qualified compiler identities separately instead of silently
changing existing short-name matching. New graph keys must include project,
service, snapshot, and declaration identity; SDK names or HTTP verb/path alone
are insufficient.

Source records and projected contract records are distinct. A declaration
removed from the selected API version may still exist in the source and must
still be indexed. Added/deleted files, augment decorators, inherited
documentation, and context-only diff lines retain their current treatment.

Preserve existing candidate IDs where normalized payloads remain identical.
Where provenance, operation identity, or representation necessarily changes
an ID, version that boundary and invalidate prior judgments. Never invent an
AutoRest document path or Swagger pointer to keep an old hash.

## 4. Emitter design

Add a small private emitter package named
`@azure-tools/typespec-assessment-emitter` under `scripts/assessment-emitter`,
exporting the standard TypeSpec `$onEmit(context)` entry point. This is a
proposed name, not an existing implementation or a request to publish to npm.
Use TypeScript for checked TCGC/compiler API integration; keep the surrounding
Node.js orchestration in `.mjs`.

The entry point's responsibilities are:

1. Validate project, revision, version-selection, and changed-source inputs.
   Check captured source hashes against the program's actual source files.
2. Extract source declarations from the original program before introducing
   a version-selected view. Reuse current hunk intersection and documentation
   presence semantics.
3. Generate AutoRest document records in memory for the requested snapshot,
   materialize their references, and normalize them with the existing
   document normalizer. Do not invoke AutoRest `$onEmit`.
4. Enumerate the selected service namespace with `@typespec/http`, independent
   of SDK inclusion, and associate operations with compiler declarations.
   Then create one SDK context for that snapshot. Report all diagnostics and
   stop contract publication on errors.
5. Normalize `sdkContext.sdkPackage` directly and record explicit method,
   property, and raw-operation associations.
6. Validate the snapshot schema, ownership, reference closure, and selected
   versions before publishing the output atomically.

Construction sketch, not a complete emitter:

```ts
const sdkContext = await createSdkContext(
  context,
  effectiveAssessmentSdkScope,
  { exportTCGCoutput: false },
);
context.program.reportDiagnostics(sdkContext.diagnostics);
```

`effectiveAssessmentSdkScope` is deliberate: reproduce the current TCGC
invocation's effective emitter-name setting, including its existing default.
Do not use the Java emitter's `LIB_NAME`, invent a new language scope from
the assessment package name, or claim that one context covers every language.
Negated language selectors must retain their current behavior.

Do not introduce Java's extra decorator allowlist or force protocol/convenience
generation options. Match existing effective options first. Continue directing
example discovery to the existing empty-example layout; disabling YAML output
does not itself disable TCGC example processing.

### Invocation contract

Pass one run-owned JSON request file to the emitter rather than encoding the
snapshot graph in TypeSpec CLI options. The emitter option surface should stay
small:

```yaml
options:
  snapshot-request-file:
    type: string
    required: true
```

The runner invokes the emitter with its normal output directory and:

```text
--option=@azure-tools/typespec-assessment-emitter.snapshot-request-file=<absolute path>
```

The snapshot request path may be absolute because it is process-local input.
Every path persisted in the request or emitted snapshot must be relative to
its declared root and use portable slash separators.

The first request schema is:

```json
{
  "schemaVersion": 1,
  "snapshotId": "snapshot-...",
  "projectId": "project-...",
  "projectPath": "specification/networkcloud/NetworkCloud.Management",
  "mode": "contracts",
  "source": {
    "revision": "current",
    "commit": "<sha>",
    "overlayDigest": null
  },
  "selection": {
    "services": [
      {
        "namespace": "Microsoft.NetworkCloud",
        "apiVersion": "2026-03-01"
      }
    ]
  },
  "sourceChanges": [
    {
      "sourceId": "source-...",
      "path": "specification/.../main.tsp",
      "contentHash": "sha256:..."
    }
  ],
  "sdk": {
    "emitterName": "@azure-tools/typespec-client-generator-core",
    "options": {
      "examplesDir": "inputs/empty-examples"
    }
  },
  "autorest": {
    "options": {}
  }
}
```

`mode` is `contracts` or `source-only`. In `source-only`, `selection`, `sdk`,
and `autorest` are absent and the emitter must not construct either contract
context. The runner, not the emitter, derives this request from the comparison
plan and current effective options. The emitter rejects unknown keys, duplicate
service namespaces, non-canonical project/source paths, missing source hashes,
and a `contracts` request without an explicit selection for every versioned
service. Represent an unversioned service with `"apiVersion": null`; absence
and a missing version selection are not interchangeable.

Do not pass arbitrary module paths or package versions in the request. Those
are discovered from the process's selected toolchain and recorded in output.
Do not allow a request to redirect output outside the compiler-provided emitter
directory.

### Snapshot envelope

The snapshot is a validated, provider-neutral envelope. The normalized
AutoRest and TCGC contracts remain their existing versioned payloads; the
envelope does not merge their schemas.

```json
{
  "schemaVersion": 1,
  "snapshot": {
    "id": "snapshot-...",
    "projectId": "project-...",
    "projectPath": "specification/networkcloud/NetworkCloud.Management",
    "mode": "contracts",
    "sourceRevision": "current",
    "sourceCommit": "<sha>",
    "overlayDigest": null,
    "selectedServices": [
      {
        "namespace": "Microsoft.NetworkCloud",
        "apiVersion": "2026-03-01"
      }
    ]
  },
  "status": "ready",
  "blockers": [],
  "diagnostics": {
    "errors": [],
    "warnings": []
  },
  "provenance": {
    "configurationHash": "sha256:...",
    "toolchainLockHash": "sha256:...",
    "packages": [],
    "resolvedModules": [],
    "emitterVersion": "..."
  },
  "sourceEvidence": {
    "sourceChanges": [],
    "referencedDeclarations": {},
    "resourceModels": {},
    "operationProjectionStats": []
  },
  "contracts": {
    "http": {
      "provider": "autorest",
      "normalized": {}
    },
    "sdk": {
      "provider": "tcgc",
      "normalized": {}
    }
  },
  "inventory": {
    "httpOperations": []
  },
  "associations": {
    "httpToSource": [],
    "sdkToHttp": [],
    "sdkToSource": []
  },
  "metrics": {}
}
```

The JSON above defines ownership and required top-level fields, not every leaf
schema. Add a checked JSON Schema beside the emitter before phase 0 fixtures.
`sourceEvidence` stores only evidence observed in this program/revision. The
coordinator reconstructs the current root source-index contract by merging
snapshot evidence with Git hunks; it does not copy one snapshot wholesale.
The envelope schema version is independent of the root source-index schema
version. In envelope version 1, a published snapshot always has
`"status": "ready"`, an empty `blockers` array, and no error diagnostics;
failed attempts are represented by the runner result and logs instead.

`contracts` is absent in `source-only` mode. In `contracts` mode, both
normalized contracts are required for `ready`. The HTTP inventory is required
even while AutoRest is authoritative because it proves operation coverage and
provides raw source identities. Inventory entries must identify service,
selected version, compiler operation identity, verb, URI template, and source
origin where available.

Associations are explicit edges, not embedded raw objects:

```json
{
  "from": "sdk-operation:<stable identity>",
  "to": "http-operation:<stable identity>",
  "kind": "raw-operation",
  "confidence": "exact"
}
```

Allowed confidence values are `exact` and `unresolved`; do not introduce
heuristic name matching into the emitter. One-to-many and many-to-one edges
are valid. An unresolved edge includes a reason code and enough identities to
debug it, but never a guessed target.

### Publication and failure semantics

The emitter writes `source-index.pending.json`, validates the complete
envelope, flushes it, and atomically renames it to `source-index.json`.
Validation failure, compiler errors, contract-generation errors, missing
selected services/versions, or reference-closure errors must leave no
published snapshot. A stale pending file is never treated as evidence and is
removed before a retry of the same run-owned snapshot directory.

Warnings are retained in the snapshot. Any compiler diagnostic with severity
`error` prevents publication. Library APIs that return diagnostics must have
those diagnostics reported to the program and copied into the snapshot's
diagnostic representation before the final error check.

The child-process result and the evidence artifact have separate authority:

- the runner records command, exit code, duration, peak memory when available,
  log path, request hash, and whether a valid snapshot was published;
- the emitter records semantic diagnostics, extracted evidence, contracts,
  package provenance, and extraction subphase timings;
- the coordinator accepts a snapshot only when the process succeeded, the
  file validates, its embedded request/configuration identity matches the
  manifest, and its content hash is recorded.

There is no automatic integrated-to-legacy fallback. A failed integrated
snapshot creates a backend-specific blocker and remains attributable to its
project, source revision, selected service/version, and failed phase. A
source-only failure blocks source/documentation evidence for the affected
project. An AutoRest failure blocks REST and HTTP-dependent semantic mapping;
a TCGC failure blocks downstream SDK analysis. Until analyzers support
dimension-scoped partial snapshots, phase 1 treats any contract-mode failure as
failure of that snapshot rather than publishing a success-shaped partial file.

### Toolchain isolation

The emitter must resolve compiler, HTTP, versioning, Azure Core, ARM, AutoRest,
and TCGC
libraries from the selected specification's locked toolchain, not from a
second copy installed next to the skill. A duplicate compiler/library instance
can invalidate decorator state and type identity.

Build/package the assessment emitter separately, with checked peer dependency
ranges. Stage the built package in a run-owned location whose dependency
resolution reaches the selected toolchain. Do not modify specification
manifests or write into a shared dependency cache merely to install the emitter.
Record the resolved module paths and versions in the proof-of-concept.

Run snapshots in bounded child processes. This isolates toolchain versions,
versioning state, and memory. Do not bundle compiler, AutoRest, or TCGC into the emitter.
Do not upgrade the specification's lockfile to make an unsupported API work.

## 5. Compilation and version selection

Keep `selectApiVersionPair` policy unchanged:

- Existing version: base source at the selected version versus current source
  at that same version.
- New version: current source projected to the previous selected version
  versus current source projected to the newest added version.
- Unversioned: base source versus current source.

**New-version comparisons still need base source evidence.** Two current-source
SDK contexts cannot establish whether a declaration or its documentation is
new in the Git diff.

Initial invocation targets per project:

| Case | Required work |
| --- | --- |
| Existing version / unversioned | Two emitter compilations, each collecting original source evidence and one contract snapshot |
| New version | One base-source-only compilation, plus two current-source emitter compilations for the selected versions |

Add a source-only mode to the same extraction implementation; it does not
create an unnecessary SDK context. Deduplicate identical source records
from the two current compilations and reject inconsistent evidence.

Within one snapshot, run source extraction, AutoRest generation/materialization,
and SDK generation sequentially. Test both library execution orders and
repeated extraction against independently compiled results to detect state
leakage. Do not initially parallelize libraries against the same program or
create multiple differently versioned SDK contexts over it. Reusing a program
across snapshot requests is a separate optimization requiring isolation tests.

Implement a narrow version-adapter boundary. On supported toolchains, use the
selected namespace with `getHttpService` or equivalent public HTTP enumeration,
and check its operations against raw links from SDK methods. Do not call
`getAllHttpServices(originalProgram)` and label the result with the requested
version afterward.

The integrated backend can get this namespace from AutoRest's returned
`document.context.service.type`. The HTTP-only backend must establish an
equivalent projection without depending on AutoRest to prepare it. AutoRest's
version filter is one string, not TCGC's per-service version map: validate each
returned service/version explicitly. If a multi-service request cannot be
represented by that filter, explicitly plan the required per-service/version
work or block; do not relabel mismatched results.

The inspected TCGC revision treats a scalar explicit API version differently
for multi-service packages: it does not select that version for every service.
Resolve and verify a per-service version map, including versioned dependencies.
Network/Common/Vmss is a required regression case. Missing versions or
unsupported projection APIs produce a blocker, never a silent latest-version
fallback. Do not import TCGC private mutation helpers as the production API.

## 6. HTTP parity matrix and AutoRest removal gate

For the first integrated backend, AutoRest's in-memory documents remain the
REST authority and use the existing normalization. The following matrix gates
only the optional replacement with a direct HTTP projection. That projection
emits the logical shape consumed by the REST analyzer; it does not regenerate
Swagger. Separate representation differences from real contract changes.

| Evidence needed today | Proposed source | Required parity work |
| --- | --- | --- |
| Complete operation inventory | Selected compiler HTTP service operations | Include omitted/internal/non-convenience operations; separate service operations from SDK overrides |
| Operation identity and route | Compiler operation identity, HTTP route/URI template, explicit operation-ID metadata | Test AutoRest naming, overloads, literal query routing, and identical verb/path collisions |
| Parameters and body | Raw HTTP parameters/body plus compiler types | Preserve wire names, locations, optionality, body-root/spread behavior, and client-level wire parameters |
| Wire schemas | Compiler types, HTTP payload visibility/metadata APIs, encoding and constraint metadata | Preserve required properties, encoded names, arrays, dictionaries, inheritance, enums, nullability, discriminator behavior, and constraints |
| Responses and headers | Raw HTTP response alternatives | Preserve exact codes, ranges, default, error classification, body-less responses, and case-insensitive headers; do not copy SDK-promoted exceptions |
| Content types and multipart | Raw HTTP bodies/responses and serialization metadata | Preserve request/response media types, part names, required parts, file parts, and multiple representations |
| Paging and LRO | Compiler/Azure Core metadata and explicit relevant extensions; SDK metadata as corroboration | Distinguish wire metadata from SDK traversal/result shape; preserve existing candidate behavior |
| Source associations | Raw compiler links and indexed declaration graph | Handle generated/spread/renamed types and multiple SDK methods per raw operation without guessing |
| AutoRest-specific representation | Explicit compatibility adapter or unsupported-feature diagnostic | Resolve `x-ms-paths`, extension overrides, schema reference aliases, and other emitter transformations before claiming replacement |

Important boundaries:

- A global SDK model's optionality/visibility is not a substitute for an
  operation-specific request or response payload. Key wire projections by
  type, selected version, visibility, and media type as required.
- REST `operationId` and SDK `crossLanguageDefinitionId` are not interchangeable.
  Keep both and an explicit association.
- `routeSource` currently distinguishes `paths` and `x-ms-paths` and participates
  in comparison. Define an equivalent logical route discriminator or document
  an approved compatibility change; do not hard-code all routes to `paths`.
- Do not infer `@doc` presence from SDK `doc`: client documentation can differ
  from source documentation, and declarations may not appear in the SDK.
- Do not recursively stringify `__raw`; convert references to stable IDs.
  Missing raw links on generated types require explicit origin evidence or
  an unresolved association.
- Do not rely only on `getHttpOperationWithCache` for validation: the inspected
  TCGC helper suppresses diagnostics from HTTP resolution. Collect diagnostics
  through the compiler HTTP APIs.
- The current REST analyzer does not issue candidates for `new-api-version`
  comparisons. Preserve that policy during migration and test HTTP parity
  separately, so a zero-candidate result cannot conceal missing evidence.
- Keep currently extracted fields even where the analyzer does not yet compare
  them. Adding new checks, such as broader security or content-type rules, is
  outside this migration.

## 7. Change map

| File / area | Planned change | What must remain |
| --- | --- | --- |
| New `scripts/assessment-emitter` package | Typed entry point, AutoRest/TCGC in-memory integration, snapshot schema, HTTP inventory/projection helpers, and focused compiler fixtures | No LLM calls or assessment judgments |
| New snapshot request and schema files | Strict request validation, provider-neutral envelope validation, association-edge definitions, and atomic publication | No absolute persisted paths, raw compiler objects, or success-shaped partial snapshots |
| `scripts/source-index.mjs` | Extract program traversal from `addCompilerEvidence`; add snapshot merging; replace its independent compiler loop after integration | Git diff/source IDs, hunk ownership, snippets, documentation presence, resource evidence, source hash checks |
| `scripts/compiler-runner.mjs` | Add assessment-emitter invocation, source-only mode, per-snapshot diagnostics/timings; retain old runner for legacy parity runs | Worktree isolation, selected version, logs, configuration provenance, bounded processes |
| `scripts/prepare-assessment.mjs` | Select snapshots before emitter invocation; merge emitted indexes; record backend and capability results; make preflight packages backend-specific | PR/local comparison, sparse scope, overlays, lockfile reuse, failure reporting |
| `scripts/api-version-selection.mjs` | Preserve pair policy; add validation/translation into the emitter's service-version request | Existing baseline/current revision semantics and version choice; compiler-based discovery is a separately gated improvement |
| `scripts/tcgc-contract.mjs` | Reuse `normalizeTcgcPackage` on live objects; add a normalized-snapshot loader; retain YAML loader for old artifacts | Normalized SDK semantics, conflict detection, reachability, resource limits, version-aware indexes |
| New `scripts/http-contract.mjs` | Provider-neutral normalized HTTP contract loading/comparison boundary, shared by the backends | One contract definition rather than separate analyzer implementations |
| `scripts/autorest-contract.mjs` | Reuse `normalizeAutorestDocuments` for live materialized documents; retain file reader for legacy; separate logical reference paths from persisted evidence locations | Existing Swagger semantics, split/common document registry, fixtures, and reference normalization |
| `scripts/analyze-semantic-intents.mjs` | Replace direct AutoRest loading with the HTTP boundary; consume explicit raw-source associations where proven equivalent | Grouping rules/tags, publication ownership/deduplication, action classification, source-first units |
| `scripts/analyze-rest-breaking.mjs` | Replace artifact loading and provider-specific blocker names; adapt unavoidable route/reference representation differences explicitly | Candidate rules, severity defaults, version policy, before/after evidence, Agent review requirement |
| `scripts/analyze-downstream-breaking.mjs` | Load normalized SDK snapshots instead of YAML on the new backend | Method/type/client comparisons, customizations, propagation graph, root causes |
| `scripts/run-assessment-analysis.mjs` | Thread backend/provenance/snapshot references through inputs and accounting; accept source-to-contract evidence | Dimension sequencing, bounded inputs, unknown-hunk inference gate, informational-intent policy |
| `scripts/build-agent-workspace.mjs`, `scripts/workflow-state.mjs` | Include referenced snapshots in canonical hashes and backend/version identity in resume validation | Exact evidence validation, tamper detection, phase/state behavior |
| `scripts/assemble-assessment.mjs`, `scripts/assessment.schema.json` | Update backend-dependent artifact provenance/pointers only where needed; continue reading old reports | Finding relationships, safety definition, judgment coverage, public dimension structure |
| Package/build metadata and directly related documentation | Add emitter build/test metadata; update README/design when the runtime is adopted | Specification lockfiles and toolchains are not rewritten |

### Intentionally unchanged

- `git-evidence.mjs` and `assessment-input.mjs` comparison and PR discovery
  behavior.
- `semantic-assessment-scope.mjs` thresholds and informational classification.
- `document-quality-input.mjs` and `document-quality-assessment.mjs` criteria
  and Agent-free documentation presence checks.
- `compliance-search-request.mjs`, `compliance-assessment.mjs`, the Guidelines
  catalog, retrieval requirements, and suppression policy.
- `sdk-method-delta.mjs` comparison meaning.
- `inference.schema.json` and `assessment-judgment.schema.json` decision
  contracts, unless a separately reviewed behavior change requires otherwise.
- Finalizer validation ordering and report UI/CSS/rendering behavior. Preserve
  readable source links; only evidence-provider labels change if necessary.
- Existing eval reports, historical measurements, and unrelated materializer
  or suppression proposals. New experiments use separate output directories.

Changing input authority may reveal existing mapping bugs. Record each as a
separate, explained behavior delta; do not quietly rewrite grouping or
compatibility rules to make the new backend's counts match.

## 8. Implementation sequence

### Before implementation: capture the PR 43718 baseline

Before changing runtime code, run the existing implementation against
[Azure/azure-rest-api-specs#43718][validation-pr]. Retain its completed
`source/source-index.json`, timing measurements, comparison commits, options,
and toolchain provenance in a separate baseline directory. Do not substitute
an earlier historical run or regenerate this baseline using modified code.

Then implement phases 0 and 1, rerun the same pinned PR comparison with the
integrated backend, and compare the two source indexes using the protocol in
section 9. This before/after experiment is the first real-PR acceptance gate.
The optional HTTP-only phases are not required for this experiment.

This sequence describes future execution. Updating this plan does not start
the baseline run, implementation, or validation.

### Phase 0: bounded integrated feasibility emitter

Create the private emitter and compile small checked fixtures with a supported
locked toolchain. Produce a snapshot `source-index.json` with source
declarations, normalized SDK evidence, in-memory AutoRest-normalized contracts,
and a compiler HTTP inventory. Keep the current coordinator untouched.

Prove module resolution, diagnostics, no Swagger output writes or YAML export,
same-program library isolation, feature-sensitive reference materialization,
raw source associations, version selection, omitted-operation coverage, and
unchanged SDK/AutoRest normalization.
Validate the strict request schema, atomic ready-snapshot publication, and
failure result for every rejected or incomplete snapshot.
Use the Java example only for context construction, not its code-model
translation or Java customization policies.

Exit: a real compile produces a schema-valid snapshot matching separately
emitted AutoRest and TCGC contracts. AutoRest is integrated, not replaced.

### Phase 1: remove redundant SDK/source work behind an opt-in backend

Integrate the runner, snapshot merge, and normalized contract loaders under
`integrated`. Keep in-memory AutoRest authoritative for REST/semantic HTTP
evidence. Compare original versus emitter source evidence, file-based versus
in-memory AutoRest normalization, and YAML versus live-object SDK normalization.

Exit: declaration/documentation coverage and SDK candidates match on the
existing fixtures and representative real cases; no standalone source,
AutoRest, or TCGC compilation remains where an emitter invocation already
supplies that snapshot. The integrated backend can ship after its own
correctness and performance gates without waiting for HTTP-only parity.

### Phase 2 (optional): implement the direct HTTP projection in shadow mode

Use `http-shadow` to implement each row of the parity matrix, preserving REST/SDK separation.
Compare normalized operation inventories, fields, rule-level candidates,
source ownership, and semantic grouping. Keep every mismatch attributable to
an operation and field, not just a total count.

Exit: all supported wire features have fixtures and no unexplained
candidate/coverage differences. Unsupported cases are explicit.

### Phase 3 (optional): opt-in AutoRest-free execution, then default switch

Enable the `http` backend after direct-HTTP parity gates pass. Verify resume,
provenance, bounded Agent inputs, source links, and blocked paths end to end.
Make it the default only after the performance and compatibility review.

Remove AutoRest from this backend's preflight and generation requirements.
The integrated backend still requires the locked AutoRest package. Retain
legacy readers for historical artifacts; remove the production legacy runner
only when the supported toolchain/capability policy allows it.

## 9. Validation and performance gates

### Required before/after experiment: PR 43718

Use [Azure/azure-rest-api-specs#43718][validation-pr] for the first controlled
validation. **Only this PR is in scope for the initial experiment; do not run
the 12-PR batch.** Execute in this order:

1. **Before implementation:** use the unchanged runtime to generate the final
   compiler-enriched `source/source-index.json` and record elapsed time.
2. **Implement:** complete the in-memory AutoRest/TCGC integration described
   in phases 0 and 1, preserving the saved baseline.
3. **After implementation:** run the integrated backend against the exact
   same PR commits and generate a second `source/source-index.json`, recording
   time with the same measurement definitions.
4. **Compare:** compare both complete files and the preserved source-evidence
   contract. Report whether they are identical, list every difference, and
   compare elapsed time. Investigate unexpected differences before accepting
   the implementation.

The experiment ends at deterministic artifact generation and comparison; it
does not require Agent judgment, Guidelines retrieval, or HTML generation.
Do not change assessment semantics or historical reports to satisfy this gate.

#### Freeze inputs and retain both outputs

Capture the PR's actual target baseline, merge-base commit, head commit,
selected projects and API-version pairs, effective configuration, dependency
lock hashes, package versions, Node version, and baseline implementation
revision/diff hash. The second run must use those captured inputs rather than
the PR's potentially newer head or a moving target branch.

Keep independent `legacy` and `integrated` output directories under a chosen
benchmark work directory, each retaining:

- `source/source-index.json`;
- preparation manifest, compiler logs, and relevant contract artifacts;
- timing and invocation provenance.

Write comparison results separately, with both file hashes, byte sizes,
equality outcomes, and JSON-path-level differences. Never overwrite the
baseline or copy its source index into the integrated output.

The legacy implementation writes a provisional source index before compiler
enrichment. That provisional file is not the baseline. Require completed
compiler source evidence and inspect compilation/documentation blockers;
an incomplete or failed run is not a passing equality/performance sample.

#### Define timing boundaries

Record these measurements for both runs:

| Measurement | Boundary |
| --- | --- |
| Source-index ready wall time | Invocation start through publication of the final compiler-enriched root source index, not its first provisional write |
| Deterministic preparation wall time | Invocation start through completed source evidence and both selected REST/SDK contract snapshots |
| Extraction subphase times | Source traversal, AutoRest generation/materialization, TCGC context creation, normalization, and serialization where instrumented |
| Setup time | PR resolution/fetch, worktree creation, and dependency preparation, recorded separately |

The integrated root index may become ready later in its pipeline because it
references contract snapshots, while the legacy index is enriched before
standalone emitter execution. Report that boundary difference explicitly.
Use completed deterministic preparation as the common end-to-end comparison;
do not claim a speedup by comparing different amounts of work.

Record start/end timestamps and monotonic elapsed durations. Do not fabricate
an uninstrumented legacy subphase time. Preserve the first before/after pair;
if dependency warmth differs, label it and run additional matched warm samples
before making a performance claim. Do not include implementation time or Agent
waits in either run.

#### Define what "the two source indexes are consistent" means

Report three separate outcomes:

| Check | Definition |
| --- | --- |
| Byte equality | Compare the original file hashes without normalization |
| Full JSON equality | Compare parsed JSON while ignoring object-key order only; retain array order and all values |
| Source-evidence parity | Compare the complete pre-existing source-index contract, with explicitly declared new envelope fields reported separately |

The plan proposes a schema-version change and new snapshot references, so
full-file equality is not guaranteed by design. Report it as unequal when
those fields differ; do not call the complete files identical just because
their source evidence matches.

For source-evidence parity, require equality of all legacy fields, including
`sourceChanges`, IDs, hunks, declaration names/kinds, source locations,
snippets, decorator/version metadata, compiler reference evidence,
documentation presence and eligibility, `referencedDeclarations`,
`resourceModels`, analysis status, and blockers.

Define any allowed envelope-only differences before examining the integrated
result: the schema version and newly introduced snapshot/provenance fields
are additions to validate separately, not fields to erase from the original
files. Do not ignore arbitrary IDs, source paths, snippets, arrays, missing
declarations, or blocker differences. If a run-specific absolute work path
appears, record its exact JSON path and normalize only its known work-root
prefix for the parity view, retaining the raw difference in the report.

Acceptance requires no unexplained differences in the legacy source-evidence
contract, valid new snapshot references, and successful deterministic
preparation. Source-index equality alone does not prove REST/SDK parity:
also compare normalized contracts and candidates using the existing gates
below. Any intended source-evidence behavior change must be reviewed
separately rather than hidden in the comparator.

#### Required result summary

Record the actual results in the benchmark artifacts and summarize them here
after execution; leave them pending until then.

| Result | Legacy | Integrated |
| --- | --- | --- |
| Pinned comparison and toolchain | Pending | Must match legacy |
| Final source-index location and hash | Pending | Pending |
| Source-index ready wall time | Pending | Pending |
| Deterministic preparation wall time | Pending | Pending |
| Setup/dependency warmth | Pending | Pending |
| Source evidence status/blockers | Pending | Pending |

Then report byte equality, full JSON equality, source-evidence parity,
field-level differences, and timing deltas. One PR is the initial integration
gate, not proof of compatibility across every supported toolchain or feature.

### Correctness

Reuse the existing `node:test` suites and add typed emitter compilation tests.
Add real compiler fixtures rather than relying solely on mocked SDK objects.
The initial regression matrix must include:

- Existing-version, new-version, unversioned, multi-service, and dependency
  version selection; source-only base evidence.
- Added/deleted declarations, decorator/doc-only changes, inherited docs,
  augment decorators, shared models, and context-only diff lines.
- Operations absent from the SDK; explicit operation IDs; overload/query
  routes; multiple projections of one operation; namespace collisions.
- SDK-only renames, alternate types, overrides, client moves, and positive/
  negative language scopes that must not become REST breaks.
- Constraints, wire encoding/names, visibility-sensitive bodies, inheritance,
  dictionaries, open/closed enums, unions, nullable and recursive schemas.
- Multipart, no-body responses, exact/range/default statuses, error models,
  HEAD/boolean handling, paging, LRO, and explicit extension metadata.
- Live Maps/cycles, missing raw origins, incompatible package APIs, compiler
  warnings/errors, partial writes, corrupted hashes, and stale resume state.
- AutoRest split-feature references, common/external references, effective
  options, and no output/example/service-manifest writes from document generation;
  same-program AutoRest/TCGC execution order and isolation.

For the initial implementation, run the focused automated fixtures and the
single PR 43718 before/after experiment. The 12 retained assessment cases are
deferred broader coverage, not a task to launch or an acceptance prerequisite
for this first experiment. Expanding to those PRs requires a later scope
decision.

Reproduce the same commits, toolchains, options, and versions. Compare fresh
legacy/emitter deterministic outputs rather than treating historical
Agent-authored finding totals as an exact oracle.

Required checks:

1. Every legacy source hunk/declaration remains accounted for, or a reviewed
   correction explains the delta.
2. Every HTTP operation and SDK method/type has an explicit identity and
   ownership mapping; no silent inventory shrinkage.
3. Rule-level candidates and normalized contracts have no unexplained
   differences; compare new-version wire evidence even when REST candidates
   are intentionally suppressed.
4. Unsupported evidence produces blockers, not compatible/default facts.
5. Old reports remain readable; new backend snapshots are hashed and resume
   rejects changes to backend, versions, options, or artifact content.

### Performance

Measure matched warm-toolchain runs sequentially before increasing concurrency.
Separate dependency setup, compiler process wall time, source extraction,
TCGC context creation, AutoRest document generation/materialization,
HTTP/SDK normalization, serialization, analyzer time,
peak process memory, bytes written/read, and Agent time.

Structural acceptance criteria:

- Integrated and HTTP-only runs spawn zero standalone AutoRest or TCGC YAML
  emitters. Integrated runs still execute AutoRest's document-generation code
  in memory; HTTP-only runs do not.
- Existing-version/unversioned cases use at most two compiler invocations per
  project; new-version cases initially use at most three.
- No YAML graph serialization/parsing occurs on either new backend. Integrated
  AutoRest document materialization still uses an in-memory JSON round trip.
- Integrated runs do not write intermediate Swagger, copied examples, or
  `service.yaml`; preserve logical paths for reference resolution.
- Snapshots do not contain full compiler graphs or source-file dumps.
- Canonical evidence remains outside the bounded Agent payload except for
  selected summaries and references.

Initially report the PR 43718 before/after measurements as a single-case
result. Any repetitions needed to control dependency warmth must reuse that
same pinned PR, not expand to other PRs. Broader performance claims and a
default switch require later matched repetitions across representative cases;
that larger benchmark is deferred. Report artifact size and peak memory where
measured, and do not generalize one PR's speedup to all assessments.
Do not promise a five-minute end-to-end assessment from this change: it does
not remove Agent judgment or external scheduling delays.

## 10. Decisions fixed by this proposal

- Use one emitter implementation for source, SDK, and HTTP extraction.
- Keep compiler source truth distinct from version-selected contract truth.
- Preserve `source/source-index.json` as the canonical root; emit isolated
  per-snapshot files and merge deterministically.
- Use a strict run-owned request file and publish only schema-valid, atomic
  ready snapshots; retain failed-attempt details in runner results and logs.
- Reuse existing SDK normalization and downstream rules.
- Integrate public AutoRest document generation in memory first; reuse existing
  REST normalization instead of reimplementing Swagger semantics up front.
- Use `@typespec/http` for raw inventory/source links immediately, with an
  optional direct REST projection later; never use SDK convenience signatures
  as the wire authority.
- Preserve version-pair and semantic-grouping policy during migration.
- Remove AutoRest only if direct HTTP projection reaches field-level parity;
  retaining it in memory is a valid production outcome.

## 11. Questions the feasibility phase must resolve

These are bounded research gates, not reasons to assume a fallback:

- Which locked compiler/TCGC versions expose the required public versioned
  namespace and raw-origin APIs? Define a supported matrix.
- Which locked AutoRest versions export the document-generation APIs, and do
  shared-program runs preserve independent-run results and diagnostics?
- Can all existing AutoRest operation IDs, overload routes, wire constraints,
  and relevant extension effects be represented without importing emitter
  internals? Identify exact exceptions.
- Does `normalizeTcgcPackage` produce identical contracts on live and exported
  objects for each supported version? Adapt only confirmed representation gaps.
- Can the full HTTP inventory be collected at the exact selected version even
  when SDK scoping omits operations or merges services?
- Which provenance-only changes require new fact IDs, and which existing IDs
  can remain stable?

## 12. Source references

Repository references above describe the working implementation inspected for
this plan. Upstream links are pinned so future API changes do not silently
change the basis of the proposal:

- [Java context-construction example][java-example].
- [TCGC context creation, options, standalone emitter, and YAML export][tcgc-context].
- [TCGC interfaces: SDK types, raw links, HTTP operations and responses][tcgc-interfaces].
- [TCGC HTTP conversion and SDK-specific transformations][tcgc-http].
- [TCGC emitter-name/scoping and version mutation implementation][tcgc-internals].
- [TCGC public helpers, raw HTTP lookup, and cross-language identities][tcgc-public].
- [TypeSpec HTTP operation and service enumeration][http-operations].
- [AutoRest package-root exports][autorest-exports].
- [AutoRest option resolution, document generation, and separate output stage][autorest-emit].
- [AutoRest HTTP usage and document materialization][autorest-openapi].
- [AutoRest document records and late-bound references][autorest-types].

[java-example]: https://github.com/microsoft/typespec/blob/7cf425fe40573d8c70c228355e16659778e5df3f/packages/http-client-java/emitter/src/code-model-builder.ts#L307-L321
[tcgc-context]: https://github.com/Azure/typespec-azure/blob/357dfca56015a02c80996b81f2205dd1ad189a1b/packages/typespec-client-generator-core/src/context.ts
[tcgc-interfaces]: https://github.com/Azure/typespec-azure/blob/357dfca56015a02c80996b81f2205dd1ad189a1b/packages/typespec-client-generator-core/src/interfaces.ts#L1025-L1097
[tcgc-http]: https://github.com/Azure/typespec-azure/blob/357dfca56015a02c80996b81f2205dd1ad189a1b/packages/typespec-client-generator-core/src/http.ts
[tcgc-internals]: https://github.com/Azure/typespec-azure/blob/357dfca56015a02c80996b81f2205dd1ad189a1b/packages/typespec-client-generator-core/src/internal-utils.ts
[tcgc-public]: https://github.com/Azure/typespec-azure/blob/357dfca56015a02c80996b81f2205dd1ad189a1b/packages/typespec-client-generator-core/src/public-utils.ts
[http-operations]: https://github.com/microsoft/typespec/blob/7cf425fe40573d8c70c228355e16659778e5df3f/packages/http/src/operations.ts#L39-L117
[autorest-exports]: https://github.com/Azure/typespec-azure/blob/357dfca56015a02c80996b81f2205dd1ad189a1b/packages/typespec-autorest/src/index.ts
[autorest-emit]: https://github.com/Azure/typespec-azure/blob/357dfca56015a02c80996b81f2205dd1ad189a1b/packages/typespec-autorest/src/emit.ts
[autorest-openapi]: https://github.com/Azure/typespec-azure/blob/357dfca56015a02c80996b81f2205dd1ad189a1b/packages/typespec-autorest/src/openapi.ts
[autorest-types]: https://github.com/Azure/typespec-azure/blob/357dfca56015a02c80996b81f2205dd1ad189a1b/packages/typespec-autorest/src/types.ts
[validation-pr]: https://github.com/Azure/azure-rest-api-specs/pull/43718
