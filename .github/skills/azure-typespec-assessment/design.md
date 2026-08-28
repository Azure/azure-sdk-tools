# Original Azure TypeSpec Assessment MVP Rewrite Plan

## Goal

Rebuild `.github/skills/azure-typespec-assessment` from the deleted
implementation, following
`tools/azsdk-cli/docs/specs/typespec-assessment.spec.md` at commit
`0c73c7462073e857b490d76e27a05a02eed4a1ed`.

The MVP is a read-only assessment of the current TypeSpec Git diff. It compares
the merge base of `HEAD` and a selected branch with `HEAD` plus staged,
unstaged, and relevant untracked changes.

Included:

- semantic understanding from changed TypeSpec and AutoRest;
- REST breaking candidates from AutoRest;
- downstream SDK breaking candidates from TCGC;
- one bounded Agent judgment;
- validated `assessment.json`;
- readable `assessment.html`.

Compliance and Document Quality are visibly deferred as `planned`.

## End-to-end flow

```text
prepare-assessment.mjs
  Git diff + source index + isolated comparison-role compile
  AutoRest artifacts + TCGC artifacts
        |
        +----------------------+----------------------+
        v                      v                      v
analyze-semantic-       analyze-rest-         analyze-downstream-
intents.mjs             breaking.mjs          breaking.mjs
        \                      |                      /
         +---------- run-assessment-analysis.mjs ---+
                         model-input.json
                                  |
                                  v
                       one Agent judgment
                                  |
                                  v
                  assemble -> validate -> render HTML
```

No Node.js script calls an LLM API. Preparation and dimension analysis are
deterministic. The Agent reads only bounded `model-input.json` and writes
`assessment-judgment.json`.

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

For each operation, the Semantic card shows LRO/202/Location behavior in the
baseline version and synchronous/200/`ServiceGatewayActionOkResponseBody`
behavior in `2025-09-01`. This is version-scoped REST evolution, not a REST
breaking finding.

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
          "declarationOccurrenceIds": [
            "declaration-occurrence-<hash>"
          ],
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

For a changed template, trait, or helper, list **every proven consumer** as an
affected REST operation. A consumer remains semantically affected even when
its normalized AutoRest and TCGC outputs are both unchanged, because its
compiled behavior depends directly on the changed TypeSpec abstraction. Its
operation card must separately state:

- whether the REST contract changed;
- whether a downstream SDK projection changed;
- or that both normalized outputs are unchanged.

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

### Downstream shared-type aggregation

REST-compatible model, enum, union, and other shared SDK type findings are
grouped by **root cause**, not rendered as one flat card per propagated type.
Each root cause produces one default-collapsed `Shared SDK type impact` card.

A root cause is the nearest deterministic SDK contract change that caused the
types to acquire a breaking public-surface difference. Examples include:

- one or more SDK methods beginning to return a previously input-only model;
- a directly changed public model propagating through methods that expose it;
- an enum or union contract change propagating through containing public
  models.

Root-cause relationships must come from TCGC type and method reference
provenance. Matching only by project, source file, name, or simultaneous usage
changes is insufficient.

Each shared-type card contains:

1. a concise root-cause summary;
2. the number of distinct affected SDK types;
3. the number of distinct affected REST operations;
4. the number of distinct affected SDK methods;
5. three deterministic representative operation/method examples;
6. an expandable complete affected type, operation, and method list;
7. links to the underlying approved findings and related Semantic intents.

Counts are based on distinct stable identities, not finding count. The same
underlying downstream finding must appear in exactly one aggregate. If several
method deltas are parts of one method contract change, such as method kind,
response type, and LRO metadata, they count as one affected SDK method and one
affected REST operation.

When one propagated type is affected by multiple independent root causes, keep
the type in each applicable root-cause card but assign its underlying finding
to one canonical card. Other cards reference that type as shared evidence
rather than duplicating the finding. The JSON records the canonical aggregate
ID so validation can enforce complete, non-overlapping finding coverage.

If deterministic TCGC provenance cannot establish a root cause, place the
finding in a separate `Unresolved shared SDK type impact` group. Do not attach
operations or methods based on guesses; counts for unproven operation/method
relationships remain zero.

For PR 43308, the ten public-type usage findings are one root-cause group:

- root cause: `ScenarioConfigurations.execute` and `ScenarioRuns.cancel`
  change from `basic`/void to `lro`/`ScenarioRun`;
- affected SDK types: 10;
- affected REST operations: 2;
- affected SDK methods: 2;
- the two direct methods remain separate operation cards containing their
  merged kind, response, and LRO deltas.

### Semantic and finding relationships

Relationships between Semantic intents and approved findings are
**bidirectional** and deterministic:

- each REST finding records its related Semantic intent IDs;
- each downstream operation/shared-type group records its related Semantic
  intent IDs;
- each Semantic intent records its related REST finding, downstream operation
  group, and shared-type group IDs.

The primary affected list inside a Semantic intent contains REST operations
only. SDK methods, models, enums, unions, and other generated symbols appear
only in a separate `Related findings` area.

Allowed relationship evidence:

1. REST finding to Semantic intent: exact project, API version, and REST
   operation identity.
2. Downstream method group to Semantic intent: exact project and compiled HTTP
   method/path mapped to an affected REST operation in that intent.
3. Shared-type group to Semantic intent: deterministic TCGC root-cause
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

## 6. Bounded Agent input

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
  "semanticReviewUnits": [],
  "restCandidates": [],
  "downstreamRootCauses": [],
  "downstreamCandidates": [],
  "deferredDimensions": {
    "compliance": "planned",
    "documentQuality": "planned"
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
      "downstreamCandidates": 0
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

## 7. Agent judgment

File: `assessment-judgment.json`

```json
{
  "schemaVersion": 1,
  "semanticIntents": [
    {
      "reviewUnitId": "semantic-<hash>",
      "title": "Return scenario-run resources from run actions",
      "summary": "The TypeSpec change modifies the operation behavior.",
      "sourceChangeIds": ["source-<hash>"],
      "operationIds": ["operation-<hash>"]
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
  "overallConfidence": "high|medium|low",
  "blockers": []
}
```

Exactly one semantic result is required per review unit and one decision per
REST candidate. Direct downstream method candidates require one decision per
candidate. Propagated shared-type candidates require one decision per
deterministic root cause instead of repetitive per-type decisions.

An approved root-cause decision approves every `propagatedCandidateId` in that
root cause except IDs explicitly listed in `excludedCandidateIds`. Exclusions
must be members of that root cause and are treated as rejected. A rejected
root-cause decision rejects every propagated candidate and omits severity.
Every downstream candidate must be covered exactly once by either a direct
candidate decision or one canonical root-cause decision.

## 8. Final assessment

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
      "status": "planned",
      "summary": "Deferred from the MVP."
    },
    "documentQuality": {
      "status": "planned",
      "summary": "Planned by the design document."
    }
  },
  "changedFiles": [],
  "projects": [],
  "blockers": [],
  "provenance": {
    "modelInput": "model-input.json",
    "judgment": "assessment-judgment.json",
    "preparationManifest": "preparation-manifest.json"
  },
  "inputAccounting": {},
  "timings": {}
}
```

Every downstream operation group merges all approved deltas for one project,
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

Each suppressed delta records the approved REST finding IDs and deterministic
causal match basis in `impliedByRest`. HTML shows one concise notice with the
suppressed operation/method/delta counts and links to the REST findings. If an
operation group contains both implied and independent deltas, render the group
with only its independent deltas and mention the suppressed count.

Do not suppress an entire operation group based only on matching operation ID,
HTTP method/path, project, or API version. Deduplication requires a supported
rule-to-rule causal relationship and matching deterministic evidence.

## 9. Validation

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
10. Compliance and Document Quality remain `planned`.

## 10. HTML requirements

The report must show:

1. report identity, confidence, scoped safety, and a header link to the
   detailed source/artifact comparison in the appendix;
2. Preview Notice and MVP coverage;
3. REST breaking findings with actual/expected behavior;
4. REST-compatible downstream SDK breaks grouped by REST operation/SDK method;
5. shared type impact collapsed by root cause;
6. source-first Semantic intents;
7. expandable REST operations with before/after impact;
8. planned Compliance and Document Quality;
9. blockers;
10. appendix with files, projects, compiler artifacts, timings, model input
    accounting, and provenance.

Semantic operation rendering is bounded:

- for 15 or fewer affected REST operations, render every operation as an
  expandable card;
- for more than 15, render a concise total-impact sentence and three
  representative expandable operation cards;
- choose representatives deterministically in stable operation-ID order;
- state how many operations are omitted from HTML;
- retain the complete affected-operation inventory in `assessment.json`.

Each operation card shows the operation ID, selected API version, HTTP
method/path, concise mapping reason, REST before/after delta or explicit
unchanged statement, and downstream outcome. It does not repeat TypeSpec code.

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

For one assessed subservice/project, the header's `TypeSpec source diff` line
shows the exact artifact pair:

```text
TypeSpec source diff:
<baseline-commit>@<baseline-api-version> →
<target-commit>@<target-api-version>
```

For multiple assessed subservices/projects, one pair would be ambiguous, so
the header links directly to the appendix's `Projects and compiler status`
table instead:

```text
TypeSpec source diff: See Projects and compiler status in Appendix
```

The header follows a summary-dashboard hierarchy:

1. uppercase `TypeSpec Assessment` eyebrow;
2. prominent assessment title;
3. overall confidence and either the single source/artifact pair or the
   multi-project appendix link on one metadata line;
4. five summary cards for overall code safety, Semantic intents, REST breaking
   changes, downstream breaking changes, and Azure compliance.

The Semantic card includes distinct affected-operation count and
add/modify/remove intent counts. The downstream card distinguishes affected
SDK methods from underlying finding count. Planned compliance is labeled
`Planned / Not assessed`; a zero must not imply that compliance passed.

Each summary card is a full-card link to its report section:

- Semantic intents → `#semantic-intents`;
- REST breaking changes → `#rest-breaking`;
- downstream breaking changes → `#downstream-breaking`;
- Azure compliance → `#azure-compliance`;
- overall code safety → the failing REST section when REST findings exist,
  otherwise the failing downstream section, with REST as the passed-state
  overview target.

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

## 11. Files

```text
.github/skills/azure-typespec-assessment/
  SKILL.md
  references/
    workflow.md
    classification.md
    output-contract.md
    downstream-breaking-cases.md
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

## 12. Completion criteria

- One command prepares deterministic evidence and bounded Agent input.
- AutoRest and TCGC use the same source revision and API version within each
  artifact-comparison role.
- Semantic analysis covers changed TypeSpec source before calculating REST
  impact.
- REST and downstream analyzers follow emitter boundaries.
- Agent judgment has exact bounded coverage.
- Final JSON rejects unsupported or incomplete results.
- HTML contains the required major information points.
- Focused tests, 11 retained report replays, strict skill lint, and a real
  PR 43308 smoke test pass.
- PR 44988 produces 11 coherent Semantic intents, no REST breaking finding for
  the new-version transition, and two grouped Service Gateway downstream SDK
  method breaks.
