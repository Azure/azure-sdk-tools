# Azure TypeSpec Assessment Design

## Goal

Define the target architecture for
`.github/skills/azure-typespec-assessment`, following
`tools/azsdk-cli/docs/specs/typespec-assessment.spec.md` at commit
`0c73c7462073e857b490d76e27a05a02eed4a1ed`.

The assessment is a read-only review of the current TypeSpec Git diff. It compares
the merge base of `HEAD` and a selected branch with `HEAD` plus staged,
unstaged, and relevant untracked changes.

Included:

- semantic understanding from changed TypeSpec and AutoRest;
- REST breaking candidates from AutoRest;
- downstream SDK breaking candidates from TCGC;
- documentation-grounded Azure compliance from the four highest-ranked
  retrievable official documents for each Semantic intent;
- optional bounded AI inference for source hunks that deterministic analysis
  cannot classify;
- one bounded Agent judgment;
- validated `assessment.json`;
- readable `assessment.html`.

Document Quality is a separate visible dimension with status `not-assessed`.

**Implementation status:** Semantic, REST, downstream, and documentation-
and optional AI inference are active. Document Quality is present but not
assessed.

## End-to-end flow

```text
             Current TypeSpec Git diff
                         |
                         v
       Deterministic preparation and compilation
          (source index + AutoRest + TCGC)
                         |
       preparation-manifest.json
       source/source-index.json
                         |
       +-----------------+-----------------+-----------------+
       |                 |                 |                 |
       v                 v                 v                 v
   Semantic            REST            Downstream        Compliance
 deterministic     deterministic      deterministic     deterministic
 review units       candidates         candidates      search requests
       |                 |                 |                 |
 dimensions/        dimensions/       dimensions/       complianceSearchRequests
 semantic-intents-  rest-breaking-    downstream-       embedded below
 input.json          input.json         breaking-input.json
       |                 |                 |                 |
       +-----------------+-----------------+-----------------+
                         |
                         v
                  model-input.json
                         |
                         v
          Check deterministic hunk coverage
                         |
                  +------+------+
                  |             |
          all classified    unknown hunks
                  |             |
                  |             v
                  |      Bounded AI inference
                  |      for unknown hunks only
                  |             |
                  |       inference.json
                  |             |
                  +------+------+
                         |
                         v
              One bounded AI judgment
       +-----------------+-----------------+-----------------+
       |                 |                 |                 |
       v                 v                 v                 v
 Summarize each      Classify each     Classify each    Rank and fetch
 semantic intent    deterministic or  deterministic or official guidance,
 once               inferred REST     inferred SDK     then assess each
                    candidate          candidate        intent once
       |                 |                 |                 |
       +-----------------+-----------------+-----------------+
                         |                                   |
                         v                                   v
             assessment-judgment.json       compliance-search-evidence.json
                         \                                   /
                          +---------------+-----------------+
                                          |
                                          v
                              Assemble and validate
                                          |
                                          v
                            assessment.json + assessment.html
```

No Node.js script produces `inference.json` or
`compliance-search-evidence.json`, or calls an LLM API. Preparation, dimension
analysis, and coverage accounting are deterministic.
`compliance-search-request.mjs` only builds the query profiles embedded in
`model-input.json`. The main Agent reads that bounded input and the local
document catalog, calls `web_fetch` until it obtains the four highest-ranked
retrievable official documents per review unit or exhausts the catalog, and
writes both `compliance-search-evidence.json` and
`assessment-judgment.json`. `compliance-assessment.mjs`, called during
assembly, consumes and validates the search evidence; it does not produce it.

Compliance is parallel to Semantic, REST, and Downstream as an assessment
dimension, but not as an initial deterministic analyzer. Its search requires
the deterministic Semantic review units and their TypeSpec evidence, so it
branches from `model-input.json`. It does not consume REST or Downstream
candidates; those take the direct branch from `model-input.json` into the same
bounded Agent judgment.

Coverage accounting is embedded in `model-input.json`; it is not a separate
artifact and does not rewrite the file after creation. When every hunk has a
deterministic candidate or explicit deterministic classification, the Agent
skips inference. Otherwise, the Agent reads only the bounded inference requests
for unknown hunks, writes `inference.json`, and then judges the combined
deterministic and inferred candidates.

## 1. Preparation manifest

File: `preparation-manifest.json`

```json
{
  "schemaVersion": 1,
  "repository": {
    "root": "C:\\repo",
    "remoteUrl": "https://github.com/Azure/azure-rest-api-specs"
  },
  "comparison": {
    "baseRef": "origin/main",
    "mergeBaseCommit": "base-sha",
    "headCommit": "head-sha",
    "workingTree": {
      "staged": true,
      "unstaged": true,
      "untracked": true
    }
  },
  "sparseCheckout": {
    "mode": "cone",
    "roots": ["specification/<service>"],
    "verified": true
  },
  "changedFiles": [
    {
      "path": "specification/<service>/<project>/main.tsp",
      "status": "added|modified|removed",
      "origins": ["committed", "staged", "unstaged", "untracked"]
    }
  ],
  "projects": [
    {
      "id": "project-<hash>",
      "path": "specification/<service>/<project>",
      "sourceChangeIds": ["source-<hash>"],
      "artifactComparison": {
        "mode": "new-api-version|existing-api-version|unversioned",
        "baseline": {
          "sourceRevision": "base|current",
          "commit": "base-sha|head-sha",
          "apiVersion": "2025-01-01",
          "reason": "previous-latest-stable|previous-latest-preview|affected-existing-version|unversioned"
        },
        "target": {
          "sourceRevision": "current",
          "commit": "head-sha",
          "apiVersion": "2026-01-01-preview",
          "reason": "newest-added-version|affected-existing-version|unversioned"
        },
        "addedVersions": ["2026-01-01-preview"],
        "available": {
          "base": ["2025-01-01"],
          "current": ["2025-01-01", "2026-01-01-preview"]
        }
      },
      "artifacts": {
        "baseline": {
          "autorest": {
            "status": "succeeded|failed",
            "format": "swagger-2.0",
            "sourceRevision": "base|current",
            "sourceCommit": "base-sha|head-sha",
            "selectedApiVersion": "2025-01-01",
            "files": [
              {
                "path": "projects/<id>/baseline/autorest/stable/2025-01-01/openapi.json",
                "apiVersion": "2025-01-01",
                "documentRole": "primary|feature|common",
                "contentHash": "<sha256>"
              }
            ],
            "serviceManifestPath": "projects/<id>/baseline/autorest/service.yaml",
            "command": {
              "executable": "node_modules\\.bin\\tsp.cmd",
              "args": []
            },
            "exitCode": 0,
            "durationMs": 1000,
            "configPath": "worktrees/<sourceRevision>/<project>/tspconfig.yaml",
            "configHash": "<sha256>",
            "logPath": "logs/<id>-baseline-autorest.log"
          },
          "tcgc": {
            "status": "succeeded|failed",
            "format": "tcgc-yaml",
            "sourceRevision": "base|current",
            "sourceCommit": "base-sha|head-sha",
            "selectedApiVersion": "2025-01-01",
            "files": [
              {
                "path": "projects/<id>/baseline/tcgc/tcgc-output.yaml",
                "contentHash": "<sha256>"
              }
            ],
            "command": {
              "executable": "node_modules\\.bin\\tsp.cmd",
              "args": []
            },
            "exitCode": 0,
            "durationMs": 1000,
            "configPath": "worktrees/<sourceRevision>/<project>/tspconfig.yaml",
            "configHash": "<sha256>",
            "logPath": "logs/<id>-baseline-tcgc.log"
          }
        },
        "target": {
          "autorest": {},
          "tcgc": {}
        }
      }
    }
  ],
  "blockers": [],
  "timings": {
    "totalMs": 0
  }
}
```

Preparation rules:

1. Resolve `git merge-base HEAD <baseline>`.
2. Capture committed, staged, unstaged, and relevant untracked TypeSpec files.
3. Discover affected `tspconfig.yaml` project roots.
4. Create detached service-scoped sparse base/current worktrees for source
   analysis. Artifact roles may both use the current worktree.
5. Apply dirty overlays only to the temporary current worktree.
6. Select one source revision and API version for each artifact-comparison
   role.
7. Compile AutoRest and TCGC with the same source revision and API version for
   each role.
8. Preserve commands, logs, exit codes, timings, and hashes.

API-version policy:

1. Source analysis always compares the merge-base source with current source,
   including dirty overlays.
2. When current source adds an API version:
   - baseline artifact: current source projected to the previous latest stable
     version;
   - if no previous stable exists, use the previous latest preview;
   - target artifact: current source projected to the newest added version.
3. When an existing API version is modified:
   - baseline artifact: merge-base source projected to the affected version;
   - target artifact: current source projected to the same affected version.
4. For an unversioned service:
   - baseline artifact: merge-base unversioned source;
   - target artifact: current unversioned source.
5. AutoRest and TCGC always use the same source revision and API version within
   each comparison role.

For PR 44988, both artifact roles use the head source:

```text
baseline = head source @ previous latest stable Network API version
target   = head source @ 2025-09-01
```

For PR 43308, which modifies an existing version:

```text
baseline = base source @ 2026-05-01-preview
target   = head source @ 2026-05-01-preview
```

Version-aware REST classification:

- In `new-api-version` mode, baseline-to-target wire differences describe
  version-scoped REST evolution and appear in Semantic operation cards.
- They do not create REST breaking findings solely because the newly added
  version differs from the previous version.
- The baseline projection must be successfully produced from current source;
  this proves the previous stable/preview version remains representable by the
  final TypeSpec.
- TCGC differences may still be downstream SDK breaking when the generated
  public method/type identity is reused across the version transition.
- In `existing-api-version` and `unversioned` modes, incompatible
  baseline-to-target wire differences are REST breaking candidates.

### Normative case: PR 44988

PR 44988 publishes Microsoft.Network API version `2025-09-01`. It uses
`new-api-version` artifact comparison: both roles compile the head source,
with the previous latest stable Network version as baseline and `2025-09-01`
as target.

The expected coherent Semantic intents are:

1. Publish the `2025-09-01` Network API version — 101 operations.
2. Add address prefix set child resources — 4 operations.
3. Add ExpressRoute LAG resources — 11 operations.
4. Allow AFC-managed firewall policy writes — 1 operation.
5. Add firewall policy Kubernetes selector groups — 4 operations.
6. Add first-party service tag resources — 6 operations.
7. Add network virtual appliance migration actions — 4 operations.
8. Add Connection Analyzer resources and query behavior — 6 operations.
9. Make service gateway update actions synchronous — 2 operations.
10. Add effective-route retrieval for virtual network gateways — 1 operation.
11. Add virtual-network IP configuration move behavior — 1 operation.

The API-version publication intent uses bounded rendering: show 3
representative operations and retain all 101 in JSON.

The service gateway intent has action `modify` and exactly two affected REST
operations:

- `ServiceGateways_UpdateAddressLocations`;
- `ServiceGateways_UpdateServices`.

The Semantic intent summarizes the shared change from LRO/202/Location
behavior to synchronous/200/`ServiceGatewayActionOkResponseBody` behavior in
`2025-09-01`. Its two operations remain deterministic supporting evidence;
they do not receive separate Semantic judgments. This is version-scoped REST
evolution, not a REST breaking finding.

Downstream contains two direct SDK method groups. Each merges `lro` to `basic`,
LRO metadata removal, response-type change, and explicitly unchanged
parameters. The versioned `updateAddressLocationsLro` and
`updateServicesLro` declarations are supporting TypeSpec evidence that
preserves the previous projection; they do not create additional target REST
operations because they retain the original operation IDs for older versions.

## 2. Source index

File: `source/source-index.json`

```json
{
  "schemaVersion": 1,
  "analysis": {
    "engine": "typespec-compiler",
    "compilerVersion": "<version>",
    "status": "ready|blocked"
  },
  "compilerEvidence": {
    "compiler-evidence-<hash>": {
      "id": "compiler-evidence-<hash>",
      "kind": "symbol-reference|template-instantiation|decorator-governance|operation-projection",
      "revision": "base|current",
      "sourceDeclarationId": "declaration-<hash>",
      "targetDeclarationId": "declaration-<hash>",
      "operationId": "Widgets_Get",
      "sourceLocation": {
        "path": "main.tsp",
        "startLine": 10,
        "endLine": 15
      }
    }
  },
  "referencedDeclarations": {
    "declaration-<hash>": {
      "id": "declaration-<hash>",
      "kind": "operation|model|template|trait|alias|enum|union",
      "qualifiedName": "Widgets.get",
      "revision": "base|current",
      "changed": false,
      "source": {
        "path": "main.tsp",
        "startLine": 30,
        "endLine": 35
      }
    }
  },
  "sourceChanges": [
    {
      "id": "source-<hash>",
      "path": "specification/<service>/<project>/models.tsp",
      "status": "added|modified|removed",
      "origins": ["committed", "unstaged"],
      "hunks": [
        {
          "id": "hunk-<hash>",
          "base": {
            "startLine": 10,
            "endLine": 12
          },
          "current": {
            "startLine": 10,
            "endLine": 15
          },
          "lines": [
            " model Widget {",
            "-  name: string;",
            "+  name: WidgetName;"
          ],
          "declarationOccurrenceIds": ["declaration-occurrence-<hash>"],
          "normalizedChanges": [
            {
              "kind": "type-change",
              "declarationOccurrenceId": "declaration-occurrence-<hash>",
              "before": "string",
              "after": "WidgetName"
            }
          ]
        }
      ],
      "declarations": [
        {
          "id": "declaration-<hash>",
          "occurrenceId": "declaration-occurrence-<hash>",
          "kind": "model|property|operation|interface|enum|union|alias",
          "qualifiedName": "Widgets.cancel",
          "compilerNodeKind": "OperationStatement",
          "decorators": ["@added(Versions.v2)"],
          "versionedMembers": ["@added(Versions.v2)"],
          "hunkIds": ["hunk-<hash>"],
          "references": [
            {
              "kind": "type-reference|template-reference|decorator-reference|import",
              "targetDeclarationId": "declaration-<hash>"
            }
          ],
          "instantiations": [
            {
              "templateDeclarationId": "declaration-<hash>",
              "operationDeclarationId": "declaration-<hash>",
              "compilerEvidenceId": "compiler-evidence-<hash>"
            }
          ],
          "source": {
            "revision": "base|current",
            "startLine": 10,
            "endLine": 15,
            "link": "https://github.com/...#L10-L15"
          }
        }
      ]
    }
  ]
}
```

Changed declarations/members are indexed with their complete changed-source
evidence. The bounded `referencedDeclarations` registry additionally retains
identity, kind, revision, and source location for unchanged declarations
required to prove a reference, template instantiation, or affected operation.
It does not retain unrelated unchanged declarations or unchanged source text.
Commit-backed source gets a GitHub link; dirty working-tree evidence uses local
path and line metadata.

Semantic source indexing requires the TypeSpec compiler AST/program for both
base and current revisions. It uses compiler node spans and symbol resolution
to associate hunks with declarations, references, decorators, imports, and
template instantiations. Regex or serialized-text matching is not an accepted
fallback for successful semantic analysis.

`normalizedChanges` records deterministic declaration-level transforms needed
for coherent grouping, such as type changes, decorator additions/removals,
operation signature changes, import changes, and documentation changes.

If compiler AST/program evidence cannot be produced, source indexing records a
blocker and the Semantic dimension becomes `not-assessed`. REST and downstream
dimensions may continue independently when their emitter evidence is
available. The report must not present regex-derived Semantic intents with
reduced or implied confidence.

## 3. Semantic analysis input

File: `dimensions/semantic-intents-input.json`

The original plan used AutoRest-change-first review units:

```json
{
  "schemaVersion": 1,
  "status": "ready|blocked",
  "facts": {
    "operation-<hash>": {
      "id": "operation-<hash>",
      "projectId": "project-<hash>",
      "comparisonRole": "baseline|target",
      "sourceRevision": "base|current",
      "sourceCommit": "base-sha|head-sha",
      "apiVersion": "2026-01-01-preview",
      "operationId": "Widgets_Get",
      "method": "get",
      "path": "/widgets/{id}",
      "parameters": [],
      "request": {},
      "responses": [],
      "paging": {},
      "lro": {}
    }
  },
  "reviewUnits": [
    {
      "id": "semantic-<hash>",
      "projectId": "project-<hash>",
      "resourceFamily": "Widgets",
      "changeKind": "added|modified|removed|version-propagation",
      "changedAspects": ["responses"],
      "sourceChangeIds": ["source-<hash>"],
      "declarationIds": ["declaration-<hash>"],
      "catalogRanking": [],
      "operationIds": ["operation-<hash>"],
      "beforeFactIds": ["operation-<hash>"],
      "afterFactIds": ["operation-<hash>"]
    }
  ],
  "blockers": []
}
```

The approved follow-up refactor changes this to TypeSpec-source-first:

```json
{
  "schemaVersion": 1,
  "status": "ready|blocked",
  "facts": {
    "operation-<hash>": {}
  },
  "reviewUnits": [
    {
      "id": "semantic-<hash>",
      "projectId": "project-<hash>",
      "action": "add|remove|modify",
      "changeKind": "add|remove|modify",
      "sourceChangeIds": ["source-<hash>"],
      "hunkIds": ["hunk-<hash>"],
      "declarationIds": ["declaration-<hash>"],
      "declarationNames": ["ScenarioRuns.cancel"],
      "groupingEvidence": [
        {
          "kind": "operation-references-declaration|template-instantiation|same-declaration-behavior|decorator-governance|same-operation-set|identical-declaration-transform|supporting-source-dependency",
          "fromHunkIds": ["hunk-<hash>"],
          "toHunkIds": ["hunk-<hash>"],
          "declarationIds": ["declaration-<hash>"],
          "factIds": ["operation-<hash>"]
        }
      ],
      "operations": [
        {
          "operationId": "ScenarioRuns_Cancel",
          "beforeFactId": "operation-<hash>",
          "afterFactId": "operation-<hash>",
          "restChanged": false,
          "matchBasis": "operation-identity|contract-containment|template-instantiation|version-decorator-governance",
          "mappingSummary": "Changed TypeSpec operation ScenarioRuns.cancel compiles to ScenarioRuns_Cancel.",
          "mappingEvidence": [
            {
              "kind": "operation-identity",
              "sourceChangeIds": ["source-<hash>"],
              "hunkIds": ["hunk-<hash>"],
              "declarationIds": ["declaration-<hash>"],
              "factIds": ["operation-<hash>"]
            }
          ]
        }
      ],
      "operationIds": ["operation-<hash>"],
      "beforeFactIds": ["operation-<hash>"],
      "afterFactIds": ["operation-<hash>"],
      "changedAspects": []
    }
  ],
  "blockers": []
}
```

Every meaningful changed TypeSpec hunk must be covered once even when there is
no directly affected REST operation or the REST contract is unchanged.

### Semantic intent grouping

Semantic intents are grouped by **coherent TypeSpec change**, not mechanically
by file, declaration, REST operation, or Git hunk. A coherent change is one
user-reviewable API behavior goal implemented by one or more related TypeSpec
hunks.

Cross-file grouping is allowed. For example, an LRO helper change, a polling
response model, and the operations that consume them may form one intent when
they jointly implement the same Location-based polling behavior.

Two hunks may be grouped only when deterministic TypeSpec evidence establishes
at least one of these relationships:

1. a changed operation directly references the changed model, alias, template,
   trait, or helper;
2. a changed template, trait, or helper is instantiated by the changed
   operation;
3. changed declarations are members of the same model or interface and express
   one indivisible behavior change;
4. a versioning or decorator change directly governs the changed declaration;
5. multiple changes have the same affected REST operation set and one cannot
   accurately describe the API behavior without the others.
6. independent declarations undergo the same normalized semantic transform,
   such as `string` to `Azure.Core.armResourceIdentifier` or a batch of
   language-specific `@clientName` additions.
7. an import-only, `using`-only, suppression-only, or documentation-only hunk
   directly supports another changed declaration in the same behavior change,
   and symbol/import resolution proves that dependency.

Do not group hunks merely because they are in the same file, project, resource
family, or pull request. Keep independent changes separate, including SDK
naming customizations, scalar/type corrections, documentation-only changes,
and unrelated operations that happen to use the same shared helper.

Identical transforms form their own coherent group; they do not merge with
other behavior groups. For example, all ARM identifier corrections may form
one intent, and all C# naming additions may form another, but neither merges
into the Location-based LRO intent.

Grouping is deterministic and happens before Agent judgment. The Agent writes
the title and summary for each supplied group but cannot merge, split, or move
hunks between groups.

### Bounded Semantic judgment

Semantic judgment is **intent-level only**. The Agent receives one compact
synopsis per deterministic review unit:

- action and grouped declaration names/kinds;
- changed TypeSpec constructs and up to three representative source excerpts;
- affected REST operation count and up to three representative operation
  identities;
- aggregate REST and downstream change signals; and
- deterministic grouping and mapping summaries.

The Agent produces one concise title and one behavior summary for the review
unit. It does not explain, classify, or restate every affected operation.
Complete operation facts, before/after contracts, and source inventories remain
deterministic evidence outside the Semantic prompt. They are available to REST
and downstream candidate judgment when needed, but are not duplicated into
Semantic analysis.

The summary describes the user-visible API goal shared by the group. It must
not enumerate all operations, declarations, or hunks. Operation count and
representative examples provide scale and traceability without expanding the
Agent workload with the size of the affected operation set.

Each non-standalone group records structured `groupingEvidence`. Every evidence
entry identifies the allowed relationship kind and the exact hunk,
declaration, and compiled fact IDs supporting that relationship. A group
containing one standalone hunk uses an empty `groupingEvidence` array.

Each meaningful hunk belongs to exactly one semantic intent. Declarations may
appear in more than one hunk, but each declaration-hunk occurrence belongs to
only one intent. A hunk with no indexed declaration is still retained and
grouped using its imports, decorators, references, and surrounding source
evidence. If no deterministic relationship to another hunk exists, it becomes
a standalone intent.

A coherent group has one `action`:

1. use `add` when every selected affected REST operation is newly introduced;
2. use `remove` when every selected affected REST operation is removed;
3. otherwise use `modify` when any existing declaration or operation is
   modified;
4. otherwise use `modify` when the group contains both removals and additions
   representing one replacement or rename;
5. otherwise use `add` when the group contains additions only;
6. otherwise use `remove` when the group contains removals only.

Supporting added or removed models, helpers, imports, decorators, or
documentation do not override a `modify` action for existing API behavior.
Therefore, PR 43308's Location-based LRO group is `modify` even though it adds
`ScenarioRunInProgressResponse`.

Conversely, supporting changes to an existing registration, import, version,
or compatibility file do not turn a newly introduced API surface into
`modify`. For example, an intent whose selected operations are all absent from
the baseline and present in the target is `add`, even when its coherent source
group also contains a modified `main.tsp`.

For PR 43308, the intended high-level grouping is:

1. semantic Location-based LRO behavior for scenario execution and
   cancellation, including the shared helper and polling response changes;
2. ARM resource identifier type corrections;
3. C# SDK naming customizations.

Other independently changed operations remain separate unless they satisfy the
coherence rules above.

### Affected REST operation mapping

List only REST operations that are directly affected by the TypeSpec change.
Operation mapping is deterministic and compiler-backed. The Agent cannot add,
remove, or select operation mappings.

Each mapped operation records structured `mappingEvidence` containing the
mapping kind and exact source-change, hunk, declaration, and compiled fact IDs.
It also records a deterministic `mappingSummary`. HTML shows only this concise
summary; complete mapping and grouping provenance remains in
`assessment.json`.

Allowed mapping evidence:

1. **Operation identity:** a changed TypeSpec operation maps to its exact
   compiled AutoRest operation.
2. **Contract containment:** a changed model, property, enum, union, or alias
   maps to an operation only when that exact changed declaration is contained
   in the operation's compiled request, response, parameter, or response
   header contract.
3. **Template instantiation:** a changed template, trait, or helper maps only
   to operations proven by compiler/source provenance to instantiate it.
4. **Version/decorator governance:** a changed version or decorator maps only
   to operations it directly governs.

Do not map an operation merely because:

- it references a broader shared model that contains or is related to the
  changed declaration;
- it is in the same interface, resource family, file, or project;
- its SDK method eventually returns a type related to the changed TypeSpec;
- its normalized operation fact contains a matching name or serialized text;
- a downstream TCGC change suggests that the operation may be related.

Transitive references count only when the exact changed wire declaration is
present in that operation's compiled REST contract. Downstream SDK propagation
may link a finding to an already mapped intent, but it cannot add an affected
REST operation.

Examples:

- A change to `ScenarioRuns.get` maps only to `ScenarioRuns_Get`.
- `ScenarioRunInProgressResponse` maps to `ScenarioRuns_Get` because that
  operation directly emits the response.
- `ScenarioRuns_Cancel` and `ScenarioConfigurations_Execute` are not added to
  that response-model intent merely because their SDK methods eventually
  return `ScenarioRun`.
- A shared helper change may map to several operations only when each
  operation is proven to instantiate that helper.

For a changed template, trait, or helper, retain **every proven consumer** in
the deterministic affected-operation inventory. A consumer remains
semantically affected even when its normalized AutoRest and TCGC outputs are
both unchanged, because its compiled behavior depends directly on the changed
TypeSpec abstraction. The intent records aggregate counts for REST-changed,
downstream-changed, and unchanged consumers; it does not require a Semantic
assessment for each consumer.

Do not reduce the affected-operation set to only operations with breaking
findings. Findings describe compatibility outcomes; they do not define the
source-derived semantic impact set.

If no operation is proven, retain the Semantic intent and report
`No directly affected REST operation deterministically established`. Do not
list plausible or ambiguous operations.

## 4. REST breaking input

File: `dimensions/rest-breaking-input.json`

```json
{
  "schemaVersion": 1,
  "status": "ready|blocked",
  "facts": {
    "rest-fact-<hash>": {
      "id": "rest-fact-<hash>",
      "projectId": "project-<hash>",
      "comparisonRole": "baseline|target",
      "sourceRevision": "base|current",
      "sourceCommit": "base-sha|head-sha",
      "apiVersion": "2026-01-01-preview",
      "operationId": "Widgets_Create",
      "method": "put",
      "path": "/widgets/{id}",
      "parameters": [],
      "request": {},
      "responses": []
    }
  },
  "candidates": [
    {
      "id": "rest-<hash>",
      "rule": "required-property-added",
      "defaultSeverity": "high",
      "actual": "Current request requires property mode.",
      "expected": "Existing requests remain valid without mode.",
      "operationIds": ["Widgets_Create"],
      "sourceChangeIds": ["source-<hash>"],
      "declarationIds": ["declaration-<hash>"],
      "evidenceFactIds": ["rest-fact-<hash>"],
      "reviewRequired": true
    }
  ],
  "blockers": []
}
```

Compared REST cases:

- operation removal or HTTP method/path change;
- parameter removal, requiredness, location, and wire type;
- request requiredness and schema;
- response status/header/schema;
- serialized property names and required properties;
- enum value removal or closed-enum restriction;
- paging and LRO HTTP behavior.

## 5. Downstream SDK breaking input

File: `dimensions/downstream-breaking-input.json`

Canonical SDK method:

```json
{
  "id": "sdk-method-<hash>",
  "crossLanguageDefinitionId": "Microsoft.Chaos.ScenarioRuns.cancel",
  "client": "Microsoft.Chaos.ScenarioRuns",
  "name": "cancel",
  "kind": "basic|paging|lro|lropaging",
  "access": "public",
  "parameters": [
    {
      "position": 0,
      "name": "runId",
      "type": "string",
      "optional": false,
      "onClient": false
    }
  ],
  "responseType": "Microsoft.Chaos.ScenarioRun",
  "operation": {
    "kind": "http",
    "path": "/scenarios/{scenarioName}/runs/{runId}/cancel",
    "uriTemplate": "/scenarios/{scenarioName}/runs/{runId}/cancel{?api-version}",
    "verb": "post",
    "parameters": [],
    "bodyParam": null,
    "responses": [],
    "exceptions": []
  },
  "paging": {},
  "lro": {}
}
```

Dimension output:

```json
{
  "schemaVersion": 1,
  "status": "ready|blocked",
  "facts": {
    "sdk-fact-<hash>": {
      "id": "sdk-fact-<hash>",
      "projectId": "project-<hash>",
      "comparisonRole": "baseline|target",
      "sourceRevision": "base|current",
      "sourceCommit": "base-sha|head-sha",
      "apiVersion": "2026-01-01-preview",
      "factKind": "method|model|enum|union|client|customization",
      "kind": "basic|paging|lro|lropaging",
      "references": [
        {
          "kind": "parameter|response|property|lro-result|paging-item",
          "targetFactId": "sdk-fact-<hash>"
        }
      ]
    }
  },
  "rootCauses": [
    {
      "id": "downstream-root-cause-<hash>",
      "kind": "method-return-propagation|type-contract-propagation|enum-union-propagation|unresolved",
      "directCandidateIds": ["downstream-<hash>"],
      "propagatedCandidateIds": ["downstream-<hash>"],
      "operationFactIds": ["sdk-fact-<hash>"],
      "methodFactIds": ["sdk-fact-<hash>"],
      "typeFactIds": ["sdk-fact-<hash>"],
      "referenceEvidence": [
        {
          "fromFactId": "sdk-fact-<hash>",
          "toFactId": "sdk-fact-<hash>",
          "kind": "response|lro-result|property"
        }
      ]
    }
  ],
  "candidates": [
    {
      "id": "downstream-<hash>",
      "rule": "method-response-changed",
      "defaultSeverity": "high",
      "actual": "ScenarioRuns.cancel has a different response type.",
      "expected": "ScenarioRuns.cancel preserves its SDK contract.",
      "crossLanguageDefinitionId": "Microsoft.Chaos.ScenarioRuns.cancel",
      "rootCauseIds": ["downstream-root-cause-<hash>"],
      "sourceChangeIds": ["source-<hash>"],
      "declarationIds": ["declaration-<hash>"],
      "evidenceFactIds": ["sdk-fact-<hash>"],
      "reviewRequired": true
    }
  ],
  "blockers": []
}
```

`factKind` is the evidence discriminator used by assembly and validation.
`kind` retains the native TCGC entity kind. For method facts it is
`basic|paging|lro|lropaging`; non-method facts retain their own normalized
TCGC kind. Never infer `factKind` from `kind`.

The downstream analyzer builds `rootCauses` before Agent judgment by traversing
normalized TCGC method-to-type and type-to-type reference edges. Candidates
carry their deterministic `rootCauseIds`. The Agent approves or rejects
candidates but cannot create, merge, split, or assign root causes.

Compared SDK cases:

- client/operation-group ownership and method location;
- complete ordered parameters;
- response type;
- all four method kinds;
- access, paging, and LRO metadata;
- public model properties;
- enum values/extensibility;
- public reachability/usage;
- SDK customization decorators.

PR 43308 is the required regression: parameters remain equal while
`ScenarioRuns.cancel` and `ScenarioConfigurations.execute` change from
`basic`/void to `lro`/`ScenarioRun` with Location polling metadata.

LRO comparison uses the language-neutral LRO behavior contract, not a second
copy of the method HTTP signature. Compare final-state-via, polling and final
steps, status-monitor behavior, logical/envelope/final results, and result
paths. Retain the originating operation method/path identity for provenance,
but ignore `lro.operation.uriTemplate` differences that only repeat a public
method parameter change. A query parameter added to both the method and its
nested LRO operation URI produces one `method-parameters-changed` finding, not
an additional `method-lro-changed` finding.

### Downstream type presentation and root-cause aggregation

REST-compatible model, enum, union, and other shared SDK type findings are
grouped by **root cause** in JSON for causal traceability and deduplication.
HTML renders one default-collapsed breaking-change card per distinct affected
SDK type, alongside the operation-level downstream cards. It does not render
one large `Shared SDK type impact` card containing several types.

A root cause is the nearest deterministic SDK contract change that caused the
types to acquire a breaking public-surface difference. Examples include:

- one or more SDK methods beginning to return a previously input-only model;
- a directly changed public model propagating through methods that expose it;
- an enum or union contract change propagating through containing public
  models.

Root-cause relationships must come from TCGC type and method reference
provenance. Matching only by project, source file, name, or simultaneous usage
changes is insufficient.

Each type card summary contains:

1. severity;
2. the short SDK type name;
3. an `SDK type` tag.

Do not show the contract-change count or affected REST operation count in the
collapsed summary. Those details belong in the expanded body.

Expanding the card shows:

1. the stable cross-language SDK contract identity and a concise change
   summary;
2. a structured `SDK contract member | Before | After` table for the changed
   type shape, properties, or enum members;
3. a highlighted `Why this is breaking` explanation;
4. a default-collapsed `Affected REST operations (N)` disclosure whose rows
   show operation ID, HTTP method, and path;
5. changed TypeSpec source links and related Semantic intent links.

The outer type card and all nested detail disclosures, including affected REST
operations, are collapsed by default. A type card with one or more related
REST operations uses the same visual hierarchy as the REST-contract and direct
operation-level downstream cards. The report labels the type as an
`SDK contract` rather than presenting it as a secondary aggregate.

Mapped SDK method data and root-cause provenance remain in `assessment.json`
for deterministic traceability but are not rendered in the SDK type card.

Counts are based on distinct stable identities, not finding count. The same
underlying downstream finding must appear in exactly one type card. Multiple
property, enum-member, union, or usage deltas for the same cross-language type
are merged into that type's card and remain separately traceable in JSON. If
several method deltas are parts of one method contract change, such as method
kind, response type, and LRO metadata, they count as one affected SDK method
and one affected REST operation.

When one type is affected by multiple independent root causes, render one type
card containing separate root-cause references rather than duplicate cards.
The JSON records the canonical type-card identity and root-cause memberships
so validation can enforce complete, non-overlapping finding coverage.

Related operations require deterministic evidence: TCGC type/method reference
provenance, or exact changed TypeSpec declaration identity linked to a Semantic
intent's operation inventory. Source-file coincidence alone is insufficient.
Do not present a related REST operation as a mapped SDK method.

If deterministic evidence cannot establish a root cause or an operation/method
relationship, retain the type card and show the unavailable relationship
explicitly. Do not attach operations or methods based on guesses; counts for
unproven relationships remain zero. A confirmed type-level breaking change
must not disappear merely because operation or method mapping is unavailable.

For PR 43308, the ten public-type usage findings are one root-cause group:

- root cause: `ScenarioConfigurations.execute` and `ScenarioRuns.cancel`
  change from `basic`/void to `lro`/`ScenarioRun`;
- affected SDK types: 10;
- affected REST operations: 2;
- affected SDK methods: 2;
- the two direct methods remain separate operation cards containing their
  merged kind, response, and LRO deltas;
- HTML renders ten collapsed SDK type cards, each linked to the shared root
  cause and its deterministically related operations/methods.

### Semantic and finding relationships

Relationships between Semantic intents and confirmed findings are
**bidirectional** and deterministic:

- each REST finding records its related Semantic intent IDs;
- each downstream operation group and SDK type card records its related
  Semantic intent IDs;
- each Semantic intent records its related REST finding, downstream operation
  group, and SDK type card IDs.

The primary affected list inside a Semantic intent contains REST operations
only. SDK methods, models, enums, unions, and other generated symbols appear
only in a separate `Related findings` area.

Allowed relationship evidence:

1. REST finding to Semantic intent: exact project, API version, and REST
   operation identity.
2. Downstream method group to Semantic intent: exact project and compiled HTTP
   method/path mapped to an affected REST operation in that intent.
3. SDK type card to Semantic intent: deterministic TCGC root-cause
   provenance through its related downstream method groups or an exact changed
   TypeSpec declaration.
4. Unique source fallback: allowed only when one and only one Semantic intent
   owns the relevant changed declaration/hunk and no stronger identity mapping
   exists.

Project-wide source lists are not unique source evidence. A downstream
candidate referencing every changed file in a project cannot use source
fallback.

When deterministic evidence cannot identify one or more correct relationships,
leave the item unlinked and record an unresolved relationship reason in JSON.
Do not link every plausible intent and do not ask the Agent to choose.

Validation requires:

- all relationship IDs exist and are unique;
- every stored relationship is reciprocal;
- every relationship includes its match basis;
- its match basis is supported by deterministic evidence;
- rejected findings are never linked;
- ambiguous or unsupported relationships remain absent.

## 6. Documentation-grounded Azure compliance

Compliance is an independent assessment dimension. It consumes Semantic
intents and their bounded TypeSpec query profiles while retaining links to the
complete deterministic source evidence. It does not consume or derive
conclusions from downstream SDK breaking input. Document Quality remains a
separate `not-assessed` dimension.

### Goal and evidence boundary

Compliance assesses each Semantic intent once against applicable first-party
TypeSpec and Azure TypeSpec documentation. The decision uses the intent's
changed TypeSpec constructs and source evidence, but it does not assess each
affected REST operation or generate a document-by-declaration decision matrix.
It does not infer rules from generated OpenAPI, TCGC output, compiler
diagnostics, catalog descriptions, prior reports, or model knowledge.

The search uses the local [agentic search
procedure](references/agentic-search.md) and its [official document
catalog](references/reference-document-links.md). The copied catalog is
navigation metadata. Only successfully fetched page content can establish an
expected compliance pattern.

### Query profile

Build one query profile per Semantic intent from deterministic evidence:

- ARM or data-plane service kind;
- intent action and summary;
- changed declaration kinds and qualified names;
- decorators and augment decorators;
- templates, traits, base resource types, and operation interfaces;
- versioning, paging, LRO, warning-suppression, and client-customization
  constructs;
- normalized added and removed TypeSpec tokens;
- up to three representative source excerpts; and
- affected-operation counts, without operation-by-operation contracts.

The deterministic request retains the complete source/hunk inventory for
traceability, but the compliance prompt is bounded to the compact intent
profile above. Operation facts are excluded because compliance evaluates the
TypeSpec design intent, not each compiled operation.

### Select the four highest-scoring documents

Score every entry in `reference-document-links.md` for each intent:

| Signal           | Points | Meaning                                                                                                                                    |
| ---------------- | -----: | ------------------------------------------------------------------------------------------------------------------------------------------ |
| Exact symbol     |      4 | Catalog title or description names a changed decorator, template, base type, interface, or other exact TypeSpec construct.                 |
| Pattern/category |      3 | The catalog section directly matches the changed resource, operation, versioning, LRO, paging, model, enum, decorator, or warning pattern. |
| Service plane    |      2 | The document applies to the intent's ARM or data-plane service kind.                                                                       |
| Change context   |      1 | The document matches the add/remove/modify action or the stable/preview version transition.                                                |

Scores are additive, from 0 through 10. Rank the complete catalog by descending
score and break ties by catalog order. Select the first four retrievable
documents, recording each score component and a concise selection rationale.
Do not allow the Agent to add an uncataloged URL.

For PR 44988's `Add address prefix set child resources` intent, the expected
catalog ranking is:

1. ARM resource types and modeling;
2. ARM resource operations;
3. Azure.ResourceManager interface reference;
4. Evolving APIs.

The first three match the new ARM child-resource and lifecycle-operation
patterns; the fourth covers introducing that surface in a new API version.
This is a ranking example, not compliance evidence: the fetched sections must
still prove applicability.

Fetch the initial four URLs concurrently with `web_fetch`. Cache fetched
markdown by canonical URL and content hash for the assessment run. When
retrieval fails, retain the failed attempt as provenance and fetch the
next-ranked catalog entry until four documents have been retrieved or the
catalog is exhausted.

### Extract applicable guidance

Search each fetched page for exact query-profile terms and their surrounding
section. Retain:

- document title, canonical URL, section heading, retrieval timestamp, and
  content hash;
- a concise verbatim excerpt containing the normative guidance;
- one or two directly relevant TypeSpec examples, each at most 12 lines;
- the query terms that matched;
- `no-relevant-guidance` when the fetched page does not govern the intent.

Do not treat the catalog description, a search-result summary, or a generated
code example as documentation evidence. Do not broaden beyond the ranked
catalog during this phase.

### Assess the Semantic intent once

After guidance extraction, produce exactly one Compliance decision for the
Semantic intent. The selected documents are evidence sources, not independent
assessment units. The decision records:

- Semantic intent ID and the source-change, hunk, and declaration IDs used as
  supporting evidence;
- applicable document URLs and guidance sections;
- `expected`: a concise synthesis of the applicable fetched guidance;
- `actual`: a concise description of the intent's changed TypeSpec pattern;
- `decision`: `applicable-pass`, `applicable-fail`,
  `no-applicable-guidance`, or `not-assessed`; and
- a short rationale grounded only in the expected and actual evidence.

`applicable-pass` requires at least one fetched guidance section that governs
the intent and no direct contradiction. `applicable-fail` requires a direct
contradiction between fetched guidance and changed code. Similar wire
behavior, an undocumented legacy helper, or a suppression is not equivalent to
the documented pattern. Use `no-applicable-guidance` when search completed but
no selected document governs the intent. Use `not-assessed` only when
retrieval, evidence, or execution is incomplete.

One failing intent produces one intent-level finding. The finding may cite
multiple source declarations and guidance sections, but it must describe one
coherent compliance gap for the intent. Affected operations never receive
separate Compliance decisions or findings.

### Dimension status and safety

- `failed`: one or more intent decisions are `applicable-fail`.
- `passed`: every Semantic intent is `applicable-pass` or
  `no-applicable-guidance`, with no incomplete evidence.
- `not-assessed`: retrieval/evidence is insufficient or fewer than four
  documents can be retrieved after catalog exhaustion.

Documents with `no-relevant-guidance` support a
`no-applicable-guidance` decision but do not support a pass against a specific
rule.
Compliance status is reported independently and does not change
REST/downstream scoped code safety.

### Bounded Agent behavior

The Agent performs catalog scoring, document search, excerpt selection, and
one intent-level evidence comparison in the existing bounded judgment phase.
It may not change Semantic intent membership, invent source IDs, invent URLs,
or use unfetched knowledge. The judgment schema requires exactly one
Compliance decision per Semantic intent, and deterministic assembly rejects
unknown, duplicate, or missing intent decisions.

The same Agent phase records retrieval and extracted evidence separately from
its decisions in `compliance-search-evidence.json`:

```json
{
  "schemaVersion": 1,
  "intents": [
    {
      "reviewUnitId": "semantic-<hash>",
      "queryProfile": {},
      "catalogRanking": [
        {
          "rank": 1,
          "catalogOrder": 3,
          "title": "ARM resource types and modeling",
          "canonicalUrl": "https://azure.github.io/typespec-azure/docs/...",
          "score": {
            "exactSymbol": 4,
            "patternCategory": 3,
            "servicePlane": 2,
            "changeContext": 1,
            "total": 10
          },
          "selectionRationale": "Matches the changed ARM child-resource pattern."
        }
      ],
      "rankedDocuments": [
        {
          "rank": 1,
          "catalogOrder": 3,
          "title": "ARM resource types and modeling",
          "canonicalUrl": "https://azure.github.io/typespec-azure/docs/...",
          "score": {
            "exactSymbol": 4,
            "patternCategory": 3,
            "servicePlane": 2,
            "changeContext": 1,
            "total": 10
          },
          "selectionRationale": "Matches the changed ARM child-resource pattern.",
          "retrieval": {
            "status": "fetched",
            "retrievedAt": "2026-01-01T00:00:00.000Z",
            "contentHash": "sha256:<hash>"
          },
          "guidance": [
            {
              "section": "Resource types",
              "excerpt": "Concise normative guidance.",
              "queryTerms": ["TrackedResource"],
              "examples": [
                "model Widget is TrackedResource<WidgetProperties> {}"
              ],
              "applicableDeclarationIds": ["declaration-<hash>"]
            }
          ],
          "noRelevantGuidance": false
        }
      ],
      "retrievalAttempts": [
        {
          "rank": 4,
          "canonicalUrl": "https://azure.github.io/typespec-azure/docs/...",
          "status": "failed",
          "error": "Fetch failure."
        }
      ],
      "blockers": []
    }
  ],
  "inputAccounting": {
    "catalogEntriesScored": 0,
    "documentsFetched": 0,
    "documentBytesFetched": 0,
    "guidanceExcerptsRetained": 0,
    "guidanceExcerptBytesRetained": 0
  }
}
```

`catalogRanking` contains every catalog URL in score order.
`rankedDocuments` contains the first four successfully fetched entries from
that ranking unless catalog exhaustion is recorded as a blocker.
`retrievalAttempts` retains failed top-ranked URLs and their replacement
history.

The HTML report will show compliance by Semantic intent:

- overall status and coverage;
- the four ranked documents and selection scores;
- one intent-level expected/actual comparison with supporting TypeSpec
  evidence;
- failing intent assessments expanded by default;
- passing intent assessments collapsed by default;
- retrieval failures and unassessed intents explicitly, never as zero findings
  that imply success.

## 7. Bounded Agent input

File: `model-input.json`

```json
{
  "schemaVersion": 1,
  "context": {
    "sourceComparison": {
      "baseCommit": "base-sha",
      "headCommit": "head-sha",
      "baseRef": "origin/main",
      "workingTree": {}
    },
    "projects": [
      {
        "id": "project-<hash>",
        "path": "specification/<service>/<project>",
        "artifactComparison": {
          "mode": "new-api-version",
          "baseline": {
            "sourceRevision": "current",
            "commit": "head-sha",
            "apiVersion": "2025-01-01"
          },
          "target": {
            "sourceRevision": "current",
            "commit": "head-sha",
            "apiVersion": "2026-01-01-preview"
          }
        }
      }
    ]
  },
  "sourceChanges": {
    "source-<hash>": {
      "id": "source-<hash>",
      "path": "main.tsp",
      "status": "modified",
      "origins": ["committed"],
      "hunks": [],
      "declarations": []
    }
  },
  "facts": {
    "operation-<hash>": {},
    "rest-fact-<hash>": {},
    "sdk-fact-<hash>": {}
  },
  "semanticReviewUnits": [
    {
      "reviewUnitId": "semantic-<hash>",
      "action": "add",
      "declarationKinds": ["model"],
      "qualifiedNames": ["Contoso.AddressPrefixSet"],
      "changedConstructs": ["TrackedResource"],
      "representativeSourceExcerpts": [
        {
          "hunkId": "hunk-<hash>",
          "text": "model AddressPrefixSet is TrackedResource<...>;"
        }
      ],
      "affectedOperationCount": 4,
      "representativeOperationIds": [
        "AddressPrefixSets_Get",
        "AddressPrefixSets_CreateOrUpdate",
        "AddressPrefixSets_Delete"
      ],
      "restChangedOperationCount": 4,
      "downstreamChangedOperationCount": 4,
      "groupingSummaries": [
        "The resource model and lifecycle operations form one child-resource change."
      ],
      "deterministicCoverage": {
        "restCandidateIds": [],
        "downstreamCandidateIds": [],
        "complianceSearchRequestIds": ["compliance-search-<hash>"],
        "relatedOperationIds": [
          "AddressPrefixSets_Get",
          "AddressPrefixSets_CreateOrUpdate",
          "AddressPrefixSets_Delete"
        ],
        "coveredHunkIds": ["hunk-<hash>"],
        "uncoveredHunkIds": [],
        "classifications": [
          {
            "hunkId": "hunk-<hash>",
            "status": "candidate-generated|no-impact|semantic-only|unknown|blocked",
            "reason": "declaration-and-operation-mapped"
          }
        ],
        "gaps": []
      },
      "inferenceRequired": false
    }
  ],
  "restCandidates": [],
  "downstreamRootCauses": [],
  "downstreamCandidates": [],
  "complianceSearchRequests": [
    {
      "reviewUnitId": "semantic-<hash>",
      "sourceChangeIds": ["source-<hash>"],
      "hunkIds": ["hunk-<hash>"],
      "declarationIds": ["declaration-<hash>"],
      "queryProfile": {
        "servicePlane": "resource-manager",
        "action": "add",
        "declarationKinds": ["model"],
        "qualifiedNames": ["Contoso.AddressPrefixSet"],
        "symbols": ["TrackedResource"],
        "categories": ["resource", "operations", "versioning"],
        "changedTokens": ["AddressPrefixSet", "TrackedResource"],
        "representativeSourceExcerpts": [
          {
            "hunkId": "hunk-<hash>",
            "text": "model AddressPrefixSet is TrackedResource<...>;"
          }
        ],
        "affectedOperationCount": 4
      }
    }
  ],
  "inferenceRequests": [],
  "deferredDimensions": {
    "documentQuality": "not-assessed"
  },
  "blockers": [],
  "inputAccounting": {
    "budgetTier": "small|medium|large|configured-maximum",
    "budgetBytes": 307200,
    "bytes": 0,
    "estimatedTokens": 0,
    "retained": {
      "sourceChanges": 0,
      "facts": 0,
      "semanticReviewUnits": 0,
      "restCandidates": 0,
      "downstreamRootCauses": 0,
      "downstreamCandidates": 0,
      "complianceSearchRequests": 0,
      "complianceSearchSourceBytes": 0
    },
    "omittedRedundant": {
      "rawEmitterArtifacts": true,
      "compilerLogs": true,
      "unchangedInventories": true,
      "unreferencedFacts": true
    }
  }
}
```

Only transitively referenced compact facts enter model input. Required evidence
is never silently dropped to fit the budget.

### 7.1 Deterministic coverage and optional inference

Coverage is calculated before `model-input.json` is written. For each Semantic
review unit, every member hunk receives exactly one deterministic status:

- `candidate-generated`: at least one mapped REST or downstream candidate;
- `no-impact`: the relevant deterministic contracts were compared and did not
  produce an impact candidate;
- `semantic-only`: the indexed change does not affect a REST or SDK contract;
- `unknown`: the source change cannot be mapped reliably to the available
  language-neutral artifacts;
- `blocked`: required deterministic artifacts or analysis are unavailable.

Only `unknown` hunks set `inferenceRequired: true`. An empty candidate list
alone does not trigger inference. Compliance retrieval failure also does not
trigger inference.

Each unknown hunk produces one bounded `inferenceRequests` entry:

```json
{
  "requestId": "inference-request-<hash>",
  "reviewUnitId": "semantic-<hash>",
  "sourceChangeId": "source-<hash>",
  "hunkId": "hunk-<hash>",
  "reason": "source-change-not-represented-in-language-neutral-artifacts",
  "sourceExcerpt": "@@clientLocation(..., \"!csharp,!go\");",
  "relatedOperationIds": [],
  "allowedDimensions": ["rest", "downstream"]
}
```

When `inferenceRequests` is empty, `inference.json` must not be required. When
requests exist, the Agent writes one result per request to `inference.json`:

```json
{
  "schemaVersion": 1,
  "results": [
    {
      "requestId": "inference-request-<hash>",
      "reviewUnitId": "semantic-<hash>",
      "hunkId": "hunk-<hash>",
      "decision": "candidates",
      "rationale": "The scoped customization changes generated Go client placement.",
      "candidates": [
        {
          "id": "inferred-downstream-<hash>",
          "dimension": "downstream",
          "rule": "client-location-changed",
          "defaultSeverity": "high",
          "actual": "The affected operations move in the generated Go client hierarchy.",
          "expected": "Existing generated Go client placement remains stable.",
          "crossLanguageDefinitionId": "ProtectionContainersOperationGroup",
          "sourceChangeIds": ["source-<hash>"],
          "hunkIds": ["hunk-<hash>"],
          "operationIds": [],
          "evidenceFactIds": [],
          "reviewRequired": true
        }
      ]
    }
  ]
}
```

The other valid inference decisions are `no-impact` and `blocked`; both require
a rationale and contain no candidates. Inferred candidates must use only the
request's review unit, source, hunk, operations, and allowed dimensions.
Multiple requests from the same review unit and source may reference the same
identical inferred candidate when one SDK impact spans several adjacent hunks;
that candidate lists every covered request hunk and is deduplicated by ID.
Assembly rejects missing, conflicting, unknown, or out-of-scope inference
results. The Agent never modifies `model-input.json`.

## 8. Agent judgment

File: `assessment-judgment.json`

```json
{
  "schemaVersion": 1,
  "semanticIntents": [
    {
      "reviewUnitId": "semantic-<hash>",
      "title": "Return scenario-run resources from run actions",
      "summary": "The TypeSpec change makes scenario execution and cancellation return pollable scenario-run resources."
    }
  ],
  "restDecisions": [
    {
      "candidateId": "rest-<hash>",
      "decision": "approve|reject",
      "severity": "high|medium|low",
      "rationale": "Caller-visible compatibility rationale."
    }
  ],
  "downstreamDecisions": [
    {
      "candidateId": "downstream-<hash>",
      "decision": "approve|reject",
      "severity": "high|medium|low",
      "rationale": "Language-neutral SDK compatibility rationale."
    }
  ],
  "downstreamRootCauseDecisions": [
    {
      "rootCauseId": "downstream-root-cause-<hash>",
      "decision": "approve|reject",
      "severity": "high|medium|low",
      "rationale": "The method return change propagates output usage to these public types.",
      "excludedCandidateIds": []
    }
  ],
  "complianceDecisions": [
    {
      "reviewUnitId": "semantic-<hash>",
      "applicableGuidance": [
        {
          "canonicalDocumentUrl": "https://azure.github.io/typespec-azure/docs/...",
          "guidanceSection": "Resource types"
        }
      ],
      "sourceChangeIds": ["source-<hash>"],
      "hunkIds": ["hunk-<hash>"],
      "declarationIds": ["declaration-<hash>"],
      "decision": "applicable-pass|applicable-fail|no-applicable-guidance|not-assessed",
      "title": "Required for applicable-fail: concise compliance gap title",
      "severity": "Required for applicable-fail: high|medium|low",
      "expected": "Concise synthesis of applicable fetched guidance.",
      "actual": "Concise description of the intent's changed TypeSpec pattern.",
      "rationale": "Evidence-grounded comparison rationale."
    }
  ],
  "overallConfidence": "high|medium|low",
  "blockers": []
}
```

Exactly one semantic result is required per review unit and one decision per
deterministic or inferred REST candidate. Direct deterministic or inferred
downstream candidates require one decision per candidate. Propagated
shared-type candidates require one decision per deterministic root cause
instead of repetitive per-type decisions.

An internal `approve` root-cause decision retains every `propagatedCandidateId` in that
root cause except IDs explicitly listed in `excludedCandidateIds`. Exclusions
must be members of that root cause and are treated as rejected. A rejected
root-cause decision rejects every propagated candidate and omits severity.
Every downstream candidate must be covered exactly once by either a direct
candidate decision or one canonical root-cause decision.

Every Semantic intent must have exactly one Compliance decision. Every
applicable guidance URL must identify a successfully fetched
`rankedDocuments` entry, and all source, hunk, and declaration IDs must already
exist in `model-input.json`. Decisions may quote only guidance recorded in
`compliance-search-evidence.json`; the Agent cannot add URLs, evidence,
declarations, operations, or assessment units during judgment.
`applicable-pass` and `applicable-fail` require non-empty expected and actual
evidence. `applicable-fail` also requires a concise finding title and severity
for the finding-first HTML presentation. `no-applicable-guidance` requires
actual changed-code evidence and a rationale explaining why the completed
search found no governing guidance. `not-assessed` requires actual evidence
and a retrieval, evidence, or intent-coverage blocker.

## 9. Final assessment

File: `assessment.json`

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-01-01T00:00:00.000Z",
  "title": "TypeSpec assessment: specification/<service>/<project>",
  "repository": {
    "root": "C:\\repo",
    "remoteUrl": "https://github.com/Azure/azure-rest-api-specs"
  },
  "comparison": {
    "baseRef": "origin/main",
    "baseCommit": "base-sha",
    "headCommit": "head-sha",
    "workingTree": {}
  },
  "artifactComparisons": [
    {
      "projectId": "project-<hash>",
      "mode": "new-api-version|existing-api-version|unversioned",
      "baseline": {
        "sourceRevision": "base|current",
        "commit": "base-sha|head-sha",
        "apiVersion": "2025-01-01",
        "reason": "previous-latest-stable|previous-latest-preview|affected-existing-version|unversioned"
      },
      "target": {
        "sourceRevision": "current",
        "commit": "head-sha",
        "apiVersion": "2026-01-01-preview",
        "reason": "newest-added-version|affected-existing-version|unversioned"
      }
    }
  ],
  "confidence": "high|medium|low",
  "safety": {
    "scope": "rest-and-downstream-only",
    "status": "passed|failed|not-assessed"
  },
  "dimensions": {
    "semantic": {
      "status": "assessed|not-assessed",
      "sourceHunkIds": ["hunk-<hash>"],
      "items": [
        {
          "id": "semantic-<hash>",
          "action": "add|remove|modify",
          "title": "Modify scenario run actions",
          "summary": "Source-first TypeSpec intent.",
          "sourceChangeIds": ["source-<hash>"],
          "hunkIds": ["hunk-<hash>"],
          "declarationIds": ["declaration-<hash>"],
          "groupingEvidence": [
            {
              "kind": "template-instantiation",
              "fromHunkIds": ["hunk-<hash>"],
              "toHunkIds": ["hunk-<hash>"],
              "declarationIds": ["declaration-<hash>"],
              "factIds": ["operation-<hash>"]
            }
          ],
          "operations": [
            {
              "operationId": "ScenarioRuns_Cancel",
              "apiVersion": "2026-05-01-preview",
              "method": "post",
              "path": "/scenarios/{scenarioName}/runs/{runId}/cancel",
              "restChanged": false,
              "downstreamChanged": true,
              "changedAspects": [],
              "restOutcome": "HTTP signature and represented payload contract unchanged.",
              "downstreamOutcome": "SDK method changed from basic/void to lro/ScenarioRun.",
              "downstreamGroupIds": ["downstream-group-<hash>"],
              "matchBasis": "operation-identity",
              "mappingSummary": "Changed TypeSpec operation ScenarioRuns.cancel compiles to ScenarioRuns_Cancel.",
              "mappingEvidence": [
                {
                  "kind": "operation-identity",
                  "sourceChangeIds": ["source-<hash>"],
                  "hunkIds": ["hunk-<hash>"],
                  "declarationIds": ["declaration-<hash>"],
                  "factIds": ["operation-<hash>"]
                }
              ],
              "before": {},
              "after": {}
            }
          ],
          "sources": [],
          "relatedFindings": {
            "rest": [],
            "downstream": ["downstream-group-<hash>"],
            "sharedTypeImpact": []
          }
        }
      ],
      "blockers": []
    },
    "rest": {
      "status": "passed|failed|not-assessed",
      "findings": [],
      "rejectedCandidateCount": 0,
      "blockers": []
    },
    "downstream": {
      "status": "passed|failed|not-assessed",
      "findings": [],
      "operationGroups": [
        {
          "id": "downstream-group-<hash>",
          "operationId": "ScenarioRuns_Cancel",
          "symbol": "Microsoft.Chaos.ScenarioRuns.cancel",
          "method": "post",
          "path": "/scenarios/{scenarioName}/runs/{runId}/cancel",
          "apiVersion": "2026-05-01-preview",
          "parametersUnchanged": true,
          "deltas": [
            {
              "findingId": "downstream-<hash>",
              "rule": "method-kind-changed",
              "field": "kind",
              "severity": "high",
              "before": "basic",
              "after": "lro",
              "actual": "The SDK method kind changed from basic to lro.",
              "expected": "The SDK method kind remains stable.",
              "rationale": "Existing callers must use LRO APIs."
            }
          ],
          "relatedSemanticIntents": ["semantic-<hash>"]
        }
      ],
      "sharedTypeImpacts": [
        {
          "id": "shared-type-impact-<hash>",
          "rootCauseId": "downstream-root-cause-<hash>",
          "rootCauseSummary": "Two methods now return ScenarioRun through Location-based LROs.",
          "findingIds": ["downstream-<hash>"],
          "canonicalFindingIds": ["downstream-<hash>"],
          "typeCount": 10,
          "types": ["Microsoft.Chaos.ScenarioRun"],
          "sampleTypes": ["Microsoft.Chaos.ScenarioRun"],
          "affectedOperationCount": 2,
          "affectedOperations": [
            "ScenarioConfigurations_Execute",
            "ScenarioRuns_Cancel"
          ],
          "affectedMethodCount": 2,
          "affectedMethods": [
            "Microsoft.Chaos.ScenarioConfigurations.execute",
            "Microsoft.Chaos.ScenarioRuns.cancel"
          ],
          "sampleOperationMethods": [
            {
              "operationId": "ScenarioConfigurations_Execute",
              "methodId": "Microsoft.Chaos.ScenarioConfigurations.execute"
            },
            {
              "operationId": "ScenarioRuns_Cancel",
              "methodId": "Microsoft.Chaos.ScenarioRuns.cancel"
            }
          ],
          "relatedSemanticIntents": ["semantic-<hash>"],
          "unresolvedRelationshipReason": null
        }
      ],
      "impliedByRest": [
        {
          "findingId": "downstream-<hash>",
          "operationId": "Widgets_Delete",
          "methodId": "Microsoft.Contoso.Widgets.delete",
          "rule": "method-removed",
          "restFindingIds": ["rest-<hash>"],
          "causalMatchBasis": "operation-removed-implies-method-removed"
        }
      ],
      "rejectedCandidateCount": 0,
      "blockers": []
    },
    "compliance": {
      "status": "passed|failed|not-assessed",
      "summary": "Documentation-grounded compliance result.",
      "coverage": {
        "semanticIntentCount": 1,
        "assessedIntentCount": 1,
        "selectedDocumentCount": 4,
        "unassessedIntentIds": []
      },
      "intentAssessments": [
        {
          "semanticIntentId": "semantic-<hash>",
          "sourceChangeIds": ["source-<hash>"],
          "hunkIds": ["hunk-<hash>"],
          "declarationIds": ["declaration-<hash>"],
          "documents": [
            {
              "rank": 1,
              "catalogOrder": 3,
              "title": "ARM resource types and modeling",
              "canonicalUrl": "https://azure.github.io/typespec-azure/docs/...",
              "score": {
                "exactSymbol": 4,
                "patternCategory": 3,
                "servicePlane": 2,
                "changeContext": 1,
                "total": 10
              },
              "retrievedAt": "2026-01-01T00:00:00.000Z",
              "contentHash": "sha256:<hash>",
              "guidance": [
                {
                  "section": "Resource types",
                  "excerpt": "Concise normative guidance.",
                  "queryTerms": ["TrackedResource"],
                  "examples": [],
                  "applicableDeclarationIds": ["declaration-<hash>"]
                }
              ],
              "noRelevantGuidance": false
            }
          ],
          "decision": "applicable-fail",
          "applicableGuidance": [
            {
              "canonicalDocumentUrl": "https://azure.github.io/typespec-azure/docs/...",
              "section": "Resource types"
            }
          ],
          "expected": "Use the documented resource template.",
          "actual": "The intent introduces the resource with a different pattern.",
          "gap": "The intent does not apply the required template.",
          "sourceLinks": [
            {
              "path": "specification/contoso/Contoso/main.tsp",
              "startLine": 40,
              "endLine": 44
            }
          ],
          "codeSnippets": ["model AddressPrefixSet { ... }"],
          "blockers": []
        }
      ],
      "findings": [
        {
          "id": "compliance-<hash>",
          "semanticIntentId": "semantic-<hash>",
          "applicableGuidance": [
            {
              "canonicalDocumentUrl": "https://azure.github.io/typespec-azure/docs/...",
              "section": "Resource types",
              "excerpt": "Concise normative guidance."
            }
          ],
          "declarationIds": ["declaration-<hash>"],
          "sourceChangeIds": ["source-<hash>"],
          "hunkIds": ["hunk-<hash>"],
          "expected": "Use the documented resource template.",
          "actual": "The intent introduces the resource with a different pattern.",
          "gap": "The intent does not apply the required template.",
          "sourceLinks": [
            {
              "path": "specification/contoso/Contoso/main.tsp",
              "startLine": 40,
              "endLine": 44
            }
          ],
          "codeSnippets": ["model AddressPrefixSet { ... }"]
        }
      ],
      "retrievalFailures": [],
      "blockers": []
    },
    "documentQuality": {
      "status": "not-assessed",
      "summary": "Document quality is not assessed."
    }
  },
  "changedFiles": [],
  "projects": [],
  "blockers": [],
  "provenance": {
    "modelInput": "model-input.json",
    "complianceSearchEvidence": "compliance-search-evidence.json",
    "judgment": "assessment-judgment.json",
    "preparationManifest": "preparation-manifest.json"
  },
  "inputAccounting": {},
  "timings": {}
}
```

Every downstream operation group merges all confirmed deltas for one project,
REST operation, and SDK method. Each delta retains its underlying finding ID,
rule, severity, concise actual/expected behavior, and rationale, and adds
structured `field`, `before`, and `after` values.

Structured fields include:

- `kind`: `basic|paging|lro|lropaging`;
- `parameters`: a changed-only parameter diff containing added, removed,
  modified, and reordered parameters plus the unchanged parameter count;
- `responseType`: stable cross-language type identity or `void`;
- `lro`: final-state-via, logical result, polling step, and final result;
- `paging`: item and continuation metadata;
- `access`: public accessibility;
- `client`: owning operation group/client.

When parameters are equal but another method aspect changes, set
`parametersUnchanged: true` and render `Parameters: unchanged`. Do not imply a
parameter change from a method-kind, response, LRO, or paging change.

When parameters change, the parameter delta uses `changes` instead of complete
`before` and `after` arrays. It records:

- `added`: the projected parameter and target position;
- `removed`: the projected parameter and baseline position;
- `modified`: the before/after projection and changed attributes;
- `reordered`: the relative baseline/target positions among retained
  parameters;
- `unchangedCount`: retained parameters whose attributes and relative order
  did not change.

Adding a parameter does not mark retained parameters as reordered when their
relative order is unchanged.

HTML renders only the changed parameters:

- added parameters as a green `+ name?: type` row;
- removed parameters as a red `- name: type` row;
- modified parameters with only changed attributes highlighted;
- reordered parameters with old and new positions;
- a concise count of unchanged parameters.

For example, adding an optional boolean to a method with three retained
parameters renders:

```text
Parameters: 1 added, 3 unchanged
+ afcManagedSync?: boolean
```

All method-delta labels use readable field names instead of internal rule IDs.
Before values use removal styling and after values use addition styling, so
the changed portion is visually prominent. A method card contains only
semantic deltas whose normalized before and after values differ; unchanged
LRO, paging, response, kind, access, or client metadata is not listed.

Each SDK method card uses the same contract-focused hierarchy as an SDK type
card:

1. the default-collapsed summary shows severity, REST operation ID, an
   `SDK method` tag, and the SDK contract-change count;
2. expanded metadata shows the stable SDK method identity, HTTP method/path,
   and a concise change summary;
3. a structured `SDK method member | Before | After` table renders parameter
   additions/removals/modifications/reordering and non-parameter method deltas;
4. a highlighted `Why this is breaking` block combines the confirmed
   method-delta rationales;
5. a compact footer retains parameter counts, changed TypeSpec source links,
   and related Semantic intent links.

The card does not repeat unchanged parameters as table rows. Added or removed
parameters use `not present` on the missing side, modified parameters show the
projected before/after signatures, and reordered parameters show their
one-based positions.

### REST/downstream deduplication

When an operation is already REST breaking, suppress only downstream SDK
deltas that are deterministically caused by that REST break. Preserve
independent REST-compatible SDK breaks for the same operation.

Examples:

- a removed REST operation causally implies removal of its generated SDK
  method, so the method-removal delta is omitted from downstream detail;
- a changed REST response schema may causally imply the corresponding SDK
  response-type change;
- an unrelated `@clientName`, client-location, access, paging, or LRO
  customization may remain an independent downstream finding even when the
  operation also has a REST break.

Each suppressed delta records the confirmed REST finding IDs and deterministic
causal match basis in `impliedByRest`. HTML presents one unified **Downstream
breaking changes (N)** list. `N` counts the visible entries: linked REST
findings, direct SDK method groups, and distinct SDK type cards. Each REST
breaking finding also appears in that list with a `REST breaking` tag and a
direct link to its REST finding details; the REST details are not duplicated.
Independent REST-compatible SDK method and type breaks remain full collapsed
downstream cards. If an operation group contains both implied and independent
deltas, render the group with only its independent deltas.

Do not suppress an entire operation group based only on matching operation ID,
HTTP method/path, project, or API version. Deduplication requires a supported
rule-to-rule causal relationship and matching deterministic evidence.

## 10. Validation

Assembly validates the small Agent answer and joins complete deterministic
evidence. Final validation independently checks:

1. allowed schemas and enums;
2. exact review-unit, direct-candidate, and downstream root-cause coverage,
   with every underlying candidate covered exactly once;
3. no invented or duplicate IDs;
4. complete actual/expected/evidence/source for findings;
5. exact one-time changed TypeSpec hunk coverage;
6. complete REST operation evidence;
7. reciprocal semantic/finding relationships;
8. complete downstream aggregation traceability;
9. derived counts, dimension status, and scoped safety;
10. exactly four successfully fetched ranked documents per Semantic intent,
    or an explicit catalog-exhaustion blocker;
11. valid 0-10 score components, exact totals, unique catalog URLs, rank
    ordering, catalog-order tie breaking, and query profiles identical to
    their deterministic requests;
12. canonical URL, retrieval timestamp, content hash, section, excerpt, and
    matched-term provenance for every guidance item;
13. exact one-time Compliance decision coverage for every Semantic intent;
14. intent-level Compliance findings with complete source IDs, applicable
    guidance links, expected guidance, actual TypeSpec pattern, gap, and
    changed-code snippets;
15. catalog descriptions and unfetched content are never used as guidance;
16. Compliance status follows evidence and coverage, while Document Quality
    remains explicitly `not-assessed`.

## 11. HTML requirements

The report must show:

1. report identity, overall code quality (`passed|failed|not-assessed`), and a
   header link to the detailed source/artifact comparison in the appendix;
2. scope notice and assessed/not-assessed dimension coverage;
3. REST breaking findings with actual/expected behavior;
4. one numbered downstream breaking list containing linked, tagged REST
   findings, direct SDK method cards, and one SDK type card per distinct
   affected cross-language type;
5. all SDK method and SDK contract cards collapsed by default; SDK contract
   summaries show severity, short type name, and an `SDK type` tag. Expanded
   details use a structured before/after contract table, highlighted breaking
   rationale, and the affected REST operation list.
   SDK method cards use the same structure with an `SDK method` tag, method
   identity, HTTP method/path, structured changed-member rows, and highlighted
   rationale.
   The affected REST operation list is also collapsed by default, while
   expanded details retain changed TypeSpec and Semantic intent links. Mapped
   SDK method data and root-cause provenance remain available in JSON rather
   than the main report;
6. a numbered `Azure Guidelines (N)` section before Semantic intents,
   containing only findings, without
   a separate status or coverage summary card, plus an explicit not-assessed
   Document Quality dimension;
   per-intent assessments, ranked search documents, and retrieval details
   appear only in the appendix;
7. source-first Semantic intents;
8. expandable REST operations with before/after impact;
9. Compliance finding cards without a separate TypeSpec source-link list;
   under **Actual**, show at most two changed-code snippets ranked by relevance
   to the actual behavior, while retaining complete evidence in JSON and the
   appendix;
10. blockers;
11. appendix with files, projects, compiler artifacts, timings, model input
    accounting, ranked Compliance documents, retrieval attempts, and
    provenance.

### REST contract cards

REST breaking findings use the same visual hierarchy as downstream SDK type
cards. The renderer groups confirmed REST findings by stable wire-contract
identity, such as a model, enum, request parameter type, or response header
type. One contract produces one default-collapsed card and retains every
deterministically affected REST operation.

Collapsed:

```text
┌ high  NfsFileType  [REST contract]
│       3 contract changes · 6 affected REST operations
└
```

Expanded:

```text
┌ high  NfsFileType  [REST contract]
│
│ REST contract  Storage.File.NfsFileType
│
│ Breaking changes
│ ┌ Contract area                         Before              After
│ ├ NfsFileType.SymLink                   "SymLink"            removed
│ ├ NfsFileType.BlockDevice               "BlockDevice"        removed
│ └ NfsFileType.CharacterDevice           "CharacterDevice"    removed
│
│ Why this is breaking
│ Removing serialized values narrows the wire contract.
│
│ Affected REST operations (6)
│ Directory_ListFilesAndDirectoriesSegment  GET   ?restype=directory&comp=list
│ File_GetProperties                        HEAD  /{shareName}/{filePath}
│ ...
│
│ Changed TypeSpec  models.tsp:412
│ Related intent    Remove NFS file-type variants
└
```

Presentation rules:

1. The summary shows severity, stable REST contract identity, a `REST
contract` tag, distinct contract-delta count, and distinct affected REST
   operation count.
2. The card is collapsed by default. Hash navigation opens the selected card
   and its folded ancestors.
3. The expanded body starts with contract identity, followed by one compact
   `Contract member | Before | After` table.
4. Multiple findings for the same contract become rows in that table; do not
   repeat full cards or unchanged request/response content.
5. Contract identities come from exact normalized AutoRest schema references
   or enum metadata. A removed property's own type is preferred; otherwise use
   the nearest containing named contract. Source-file or display-name
   coincidence is insufficient.
6. Human-readable contract areas replace internal rule IDs, for example
   `response 200.segment.fileItems[].fileType`, `include`, or
   `x-ms-file-file-type`.
7. Removed values use removal styling and added/replacement values use
   addition styling, matching downstream method cards.
8. One concise rationale follows the table. Per-row rationales are shown only
   when they materially differ.
9. The complete affected-operation list follows the diff. Each row shows
   operation ID, selected API version, HTTP method, and path.
10. REST-derived downstream entries link back to the contract card rather than
    duplicate its REST details.
11. Changed TypeSpec sources and related Semantic intents appear at the bottom.
12. A REST finding without a proven contract identity remains visible as a
    default-collapsed `Unmapped REST contract change` card. Do not infer a
    contract from source-file coincidence.
13. Stable finding anchors remain inside the aggregate card so existing deep
    links continue to work. JSON retains every underlying finding once.
14. The visible REST count is the number of distinct contract cards plus
    unmapped cards, not the raw finding-row count.

Compliance rendering is source-first:

- show status, assessed-intent coverage, and finding count;
- under each Semantic intent, show the four ranked documents, score
  components, fetched section, and canonical source link;
- show one fetched-guidance synthesis beside the intent's representative
  changed TypeSpec evidence;
- expand `applicable-fail` intent assessments by default;
- collapse `applicable-pass` intent assessments by default;
- render retrieval failures, catalog exhaustion, and `not-assessed` intents
  explicitly rather than presenting zero findings as a pass.

Semantic operation rendering is bounded:

- render a concise aggregate impact sentence and no more than three
  representative expandable operation cards for every intent;
- choose representatives deterministically in stable operation-ID order;
- state how many operations are omitted from HTML;
- retain the complete affected-operation inventory in `assessment.json`.

Each operation card shows the operation ID, selected API version, HTTP
method/path, concise mapping reason, REST before/after delta or explicit
unchanged statement, and downstream outcome. It is deterministic supporting
evidence, not a separate Semantic or Compliance assessment, and it does not
repeat TypeSpec code.

Each Semantic intent is collapsed by default and shows exactly one escaped
TypeSpec code example. The example is nested in a second disclosure that is
also collapsed by default. It is presentation-only and does not narrow the
intent's evidence:

- prefer hunks retained by operation-specific compiler evidence;
- then prefer hunks attached to a changed declaration;
- then prefer a hunk with a substantive changed line rather than only an
  import, using, or blank-line change;
- use source path, current/base start line, and hunk ID as deterministic final
  tie-breakers.

Label the code as a representative example and link readers to the Appendix
for complete evidence. If the intent has no provable hunk, state
`No representative TypeSpec example available` rather than showing unrelated
code. The complete source and hunk inventory remains in `assessment.json` and
in a collapsed Appendix section.

Operation code evidence is scoped to that operation. Primitive source mapping
records source-change, hunk, and declaration IDs on every operation mapping.
When coherent units merge, duplicate mappings for one operation retain only
the highest-authority mapping evidence:

- API-version governance for publication intents;
- exact operation identity for operation changes;
- compiled contract containment;
- compiler-reference fallback.

The renderer must never pass intent-level source hunks to operation cards.
Operation-specific source evidence remains in `assessment.json` for
traceability and representative-example selection, but code is rendered once
at the Semantic-intent level.

For previously generated publication units that predate operation-level
source IDs, assembly may recover only the unit's compiler-indexed `Versions`
declaration hunks. This fallback is limited to API-version publication units;
it is further restricted to the operation fact's project root. It must not
attach another subservice's version declaration or other intent-wide feature
or compatibility hunks to every published operation.

The header's `TypeSpec source diff` line shows only the Git source commits:

```text
TypeSpec source diff: <base-commit> → <head-commit>
```

The appendix's `Projects and compiler status` table records the exact API
version passed to each baseline and target emitter invocation:

```text
baseline <commit>@<api-version> → target <commit>@<api-version>
```

The header follows a summary-dashboard hierarchy:

1. uppercase `TypeSpec Assessment` eyebrow;
2. prominent assessment title;
3. the single source/artifact pair or multi-project appendix link on one
   metadata line;
4. six summary cards for overall code quality, Semantic intents, REST breaking
   changes, downstream breaking changes, Azure Guidelines, and Document
   Quality.

Overall code quality combines REST/downstream safety with Azure Guidelines. It
is `Failed` when either assessed result fails, `Passed` when both pass, and
`Not assessed` when neither fails but either result is unavailable. Document
Quality remains a separate dimension. The Semantic card includes distinct
affected-operation count and
add/modify/remove intent counts. The downstream card distinguishes affected
SDK methods from underlying finding count. The Compliance card shows
`Passed`, `Failed`, or `Not assessed` plus finding and covered-declaration
counts. A zero finding count must not imply a pass when evidence retrieval or
declaration coverage is incomplete.

Each summary card is a full-card link to its report section:

- Semantic intents → `#semantic-intents`;
- REST breaking changes → `#rest-breaking`;
- downstream breaking changes → `#downstream-breaking`;
- Azure compliance → `#azure-compliance`;
- Document Quality → `#document-quality`;
- overall code quality → the failed or incomplete code-quality dimension.

Cards expose visible hover and keyboard-focus states without changing their
status colors.

The report does not render a separate `Assessment comparison` section. Each
project records the complete artifact comparison once in the appendix with:

- comparison mode;
- baseline role, source revision, full commit SHA, and API version;
- target role, source revision, full commit SHA, and API version;
- selection reason for each role;
- AutoRest and TCGC status for each role.

For an existing-version change, the appendix comparison is:

```text
source:    <base-commit> → <head-commit>
artifacts: <base-commit>@<affected-version> →
           <head-commit>@<affected-version>
```

For a newly added version such as PR 44988, the appendix comparison is:

```text
source:    9f0ad696... → 780a61ace...
artifacts: 780a61ace...@<previous-latest-stable> →
           780a61ace...@2025-09-01
```

The report explicitly states when the Git base commit is used only for the
TypeSpec source diff and is not used to produce the baseline emitter artifact.
Never label a head-source artifact as a base-commit artifact.

## 12. Files

```text
.github/skills/azure-typespec-assessment/
  SKILL.md
  references/
    workflow.md
    classification.md
    output-contract.md
    downstream-breaking-cases.md
    agentic-search.md
    reference-document-links.md
  scripts/
    cli.mjs
    stable-id.mjs
    git-evidence.mjs
    source-index.mjs
    api-version-selection.mjs
    compiler-runner.mjs
    autorest-contract.mjs
    tcgc-contract.mjs
    prepare-assessment.mjs
    analyze-semantic-intents.mjs
    analyze-rest-breaking.mjs
    analyze-downstream-breaking.mjs
    run-assessment-analysis.mjs
    compliance-search-request.mjs
    compliance-search-evidence.schema.json
    inference.schema.json
    assessment-judgment.schema.json
    assessment.schema.json
    assemble-assessment.mjs
    validate-assessment.mjs
    assessment-display.mjs
    render-assessment-html.mjs
    *.test.mjs
  evals/
    assessment.eval.yaml
```

Preserve accepted assessments, `evals/cases.json`, and user-owned eval changes.

## 13. Completion criteria

- One command prepares deterministic evidence and bounded Agent input.
- AutoRest and TCGC use the same source revision and API version within each
  artifact-comparison role.
- Semantic analysis covers changed TypeSpec source before calculating REST
  impact.
- REST and downstream analyzers follow emitter boundaries.
- Every Semantic intent produces one bounded Compliance query profile from its
  changed constructs, representative source evidence, and aggregate operation
  counts.
- Catalog scoring deterministically selects the four highest-ranked
  retrievable official documents per intent, with failed attempts and
  replacements preserved.
- Fetched guidance has canonical URL, content hash, section, excerpt, and
  query-term provenance.
- Every Semantic review unit records deterministic hunk coverage directly in
  `model-input.json`.
- AI inference is skipped when all hunks are deterministically classified and
  runs only for explicit `unknown` hunk requests.
- When inference runs, `inference.json` has exact request coverage and only
  bounded, source-linked inferred candidates.
- Agent judgment has one concise Semantic result and one Compliance decision
  per intent, plus exact deterministic and inferred REST/downstream candidate
  coverage.
- Final JSON rejects unsupported or incomplete results.
- HTML presents ranked documentation and intent-level Compliance results
  without conflating them with scoped REST/downstream code safety.
- Focused tests, 11 retained report replays, strict skill lint, and real PR
  43308, 44882, and 44988 smoke tests pass.
- PR 44988 produces 11 coherent Semantic intents, no REST breaking finding for
  the new-version transition, and two grouped Service Gateway downstream SDK
  method breaks, and ranks the expected four documents for the AddressPrefixSet
  intent.

## 14. Technical challenges

The primary technical challenge is constructing and reconciling several
incomplete graphs of the same API change:

| Graph          | Source                         | Represents                                                                 |
| -------------- | ------------------------------ | -------------------------------------------------------------------------- |
| Source graph   | TypeSpec compiler and Git diff | Hunks, declarations, decorators, references, and versions                  |
| REST graph     | AutoRest output                | Operations, routes, parameters, schemas, headers, paging, and LRO behavior |
| SDK graph      | TCGC output                    | Clients, methods, parameters, return types, models, enums, and unions      |
| Guidance graph | Official documents             | Azure requirements applicable to each Semantic intent                      |

The assessment must connect these graphs without losing provenance:

```text
TypeSpec hunk
  -> declaration
  -> Semantic intent
  -> REST operation and schema
  -> SDK client, method, and type
  -> applicable Azure guidance
```

The main challenges are:

1. **Stable identity across revisions.** Names, operation IDs, paths, generated
   symbols, and compiler identities may change between baseline and target.
   Fallback matching must not pair unrelated entities.
2. **Transitive type reachability.** A nested model or enum can affect
   operations and SDK methods through inheritance, spreads, aliases, unions,
   collections, response wrappers, and LRO results. Traversal must be
   cycle-safe and deduplicated.
3. **Version projection.** The comparison may select different API versions on
   the two sides. Analysis must distinguish a PR change from a difference
   caused only by version selection.
4. **Language-specific SDK behavior.** Language-neutral TCGC may not expose
   Go-, Python-, Java-, or C#-specific customization effects such as a scoped
   `@@clientLocation` change.
5. **Source-to-artifact provenance.** Compiler artifacts describe generated
   contracts but may not identify the exact changed hunk that caused a delta.
   Every finding still requires an auditable link to changed TypeSpec.
6. **Coverage accounting.** Exact candidate coverage proves that every
   generated candidate was judged, but does not prove that every changed hunk
   produced a candidate or an explicit no-impact result.
7. **Deterministic aggregation.** Fine-grained deltas must become readable
   method, client, and type findings without hiding distinct breaks or
   duplicating one root cause across many operations.
8. **Bounded Agent validation.** Agent output must use only supplied evidence
   and identifiers. Assembly rejects invented symbols, unknown sources,
   duplicate coverage, unsupported findings, and success-shaped fallbacks.
9. **Compliance retrieval quality.** Search must identify governing official
   guidance for each narrow intent. A completed search with only generic or
   irrelevant guidance produces `no-applicable-guidance`; incomplete execution
   produces `not-assessed`.
10. **Artifact size and performance.** Large services can produce very large
    evidence graphs. Input compaction must preserve all transitively required
    evidence without overwhelming the bounded Agent context.

To make deterministic blind spots observable, each Semantic review unit
includes a coverage ledger:

```json
{
  "reviewUnitId": "semantic-...",
  "deterministicCoverage": {
    "restCandidateIds": [],
    "downstreamCandidateIds": [],
    "complianceSearchRequestIds": ["compliance-search-..."],
    "relatedOperationIds": [],
    "coveredHunkIds": [],
    "uncoveredHunkIds": ["hunk-..."],
    "gaps": ["no-language-specific-tcgc-delta"]
  },
  "inferenceRequired": true
}
```

This changes the completeness invariant from:

> Every generated candidate was judged.

to:

> Every changed piece of evidence was deterministically analyzed, explicitly
> classified as no impact, sent to bounded AI inference, or reported as
> blocked.
