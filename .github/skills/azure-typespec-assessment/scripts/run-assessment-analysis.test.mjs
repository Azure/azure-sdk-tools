import assert from "node:assert/strict";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  applyModelInputBudget,
  buildAssessmentDraft,
  buildFastAssessmentDraft,
  buildSemanticReviewUnits,
  compactAnalysisProject,
  mergeHistoricalComplianceDocuments,
} from "./run-assessment-analysis.mjs";

test("assessment drafts isolate deterministic evidence from model tasks", () => {
  const draft = buildAssessmentDraft({
    evidence: {
      baseline: { commit: "base" },
      head: { commit: "head" },
      changedFiles: ["spec/main.tsp"],
      sourceReferences: [{ path: "spec/main.tsp" }],
      typeSpecDiffs: [],
      errors: [],
      durationMs: 2000,
      phaseDurations: { deterministicAnalysisMs: 25 },
    },
    analysis: {
      durationMs: 25,
      sourceIndex: [],
      projects: [
        {
          path: "spec",
          sourceReferences: [{ path: "spec/main.tsp" }],
          rest: {
            baseline: [{ key: "v1:GET:/widgets" }],
            head: [{ key: "v1:GET:/widgets" }],
            changes: [],
            restBreakingCandidates: [],
          },
          downstream: { candidates: [], changes: [] },
        },
      ],
    },
    complianceEvidence: { durationMs: 300, documents: [] },
    totalMs: 2400,
  });
  assert.equal(draft.projects[0].path, "spec");
  assert.equal(draft.projects[0].rest.baseline, undefined);
  assert.equal(draft.projects[0].rest.head, undefined);
  assert.equal(draft.projects[0].rest.counts.baselineOperations, 1);
  assert.equal(draft.comparison.kind, "committed-range");
  assert.equal(draft.assessmentDuration.preparationMs, 2000);
  assert.equal(draft.assessmentDuration.deterministicAnalysisMs, 25);
  assert.equal(draft.assessmentDuration.documentationEvidenceMs, 300);
  assert.ok(draft.modelTasks.every((task) => typeof task === "string"));
});

test("fast assessment drafts retain only impact evidence", () => {
  const full = buildAssessmentDraft({
    evidence: {
      baseline: { commit: "base" },
      head: { commit: "head" },
      changedFiles: ["spec/main.tsp"],
      typeSpecDiffs: [],
      errors: [],
      durationMs: 10,
      phaseDurations: { deterministicAnalysisMs: 2 },
    },
    analysis: {
      durationMs: 2,
      sourceIndex: [],
      projects: [
        {
          path: "spec",
          rest: {
            baseline: [],
            head: [],
            changes: [],
            restBreakingCandidates: [],
          },
          downstream: { candidates: [], changes: [] },
        },
      ],
    },
    complianceEvidence: { durationMs: 1, documents: [] },
    totalMs: 12,
  });
  const fast = buildFastAssessmentDraft(full);
  assert.equal(fast.mode, "impact-only");
  assert.deepEqual(
    fast.projects[0].rest.operationGroups,
    full.projects[0].rest.operationGroups,
  );
  assert.match(fast.modelTasks.join(" "), /Do not generate semantic intents/);
});

test("assessment drafts identify local pre-PR working-tree scope", () => {
  const draft = buildAssessmentDraft({
    evidence: {
      baseline: { ref: "origin/main", commit: "base" },
      head: {
        commit: "head",
        hasWorkingTreeChanges: true,
        changeScope: { staged: true, unstaged: true, untracked: true },
      },
      changedFiles: ["spec/main.tsp", "spec/new.tsp"],
      sourceReferences: [],
      typeSpecDiffs: [],
      errors: [],
      durationMs: 10,
      phaseDurations: { deterministicAnalysisMs: 1 },
    },
    analysis: { durationMs: 1, sourceIndex: [], projects: [] },
    complianceEvidence: { durationMs: 1, documents: [] },
    totalMs: 12,
  });
  assert.equal(draft.comparison.kind, "local-working-tree");
  assert.deepEqual(draft.comparison.includedChanges, {
    staged: true,
    unstaged: true,
    untracked: true,
  });
});

test("compact drafts omit unchanged operation inventories and raw TCGC leaves", () => {
  const unchanged = Array.from({ length: 100 }, (_, index) => ({
    key: `v1:GET:/widgets/${index}`,
  }));
  const project = compactAnalysisProject(
    {
      path: "spec",
      sourceReferences: [{ path: "spec/main.tsp" }],
      rest: {
        baseline: unchanged,
        head: unchanged,
        changes: [],
        restBreakingCandidates: [],
      },
      downstream: {
        changes: Array.from({ length: 100 }, (_, index) => ({
          path: `$.raw[${index}]`,
        })),
        candidates: [],
      },
    },
    [],
  );
  assert.equal(project.rest.counts.baselineOperations, 100);
  assert.equal(project.rest.counts.modelRelevantOperations, 0);
  assert.equal(project.rest.operationChanges.length, 0);
  assert.equal(project.rest.operationGroups.length, 0);
  assert.equal(project.downstream.changes, undefined);
  assert.ok(JSON.stringify(project).length < 500);
});

test("compact drafts use a manifest for added operations", () => {
  const largeContract = {
    key: "v1:POST:/widgets",
    operationId: "Widgets_Create",
    apiVersion: "v1",
    method: "POST",
    path: "/widgets",
    parameters: [
      {
        in: "query",
        name: "mode",
        required: false,
        type: "string",
        contract: {
          enum: Array.from({ length: 100 }, (_, index) => `v${index}`),
        },
      },
    ],
    request: {
      required: true,
      content: [
        {
          mediaType: "application/json",
          schema: "Widget",
          contract: { properties: { payload: "x".repeat(10_000) } },
        },
      ],
    },
    responses: [
      {
        status: "200",
        headers: [],
        content: [{ mediaType: "application/json", schema: "Widget" }],
      },
    ],
    lro: { isLongRunning: false },
    paging: { isPaged: false },
    sourceArtifact: "widget.json",
  };
  const project = compactAnalysisProject(
    {
      path: "spec",
      sourceReferences: [],
      rest: {
        baseline: [],
        head: [largeContract],
        changes: [{ kind: "added", after: largeContract, aspects: [] }],
        restBreakingCandidates: [],
      },
      downstream: { changes: [], candidates: [] },
    },
    [],
  );
  assert.equal(project.rest.operationChanges.length, 0);
  assert.deepEqual(project.rest.operationGroups, [
    {
      id: "added-v1-widgets-added",
      kind: "added",
      behavior: "added",
      apiVersion: "v1",
      family: "Widgets",
      operationIds: ["Widgets_Create"],
      changedAspects: undefined,
      changes: undefined,
    },
  ]);
  assert.ok(JSON.stringify(project.rest.operationGroups).length < 200);
});

test("compact drafts summarize modified contracts without copying schemas", () => {
  const before = {
    key: "v1:POST:/widgets",
    operationId: "Widgets_Create",
    apiVersion: "v1",
    method: "POST",
    path: "/widgets",
    request: {
      required: true,
      content: [
        {
          schema: "Widget",
          contract: { payload: "x".repeat(20_000) },
        },
      ],
    },
    responses: [],
  };
  const after = {
    ...before,
    request: {
      required: true,
      content: [
        {
          schema: "NewWidget",
          contract: { payload: "y".repeat(20_000) },
        },
      ],
    },
  };
  const project = compactAnalysisProject({
    path: "spec",
    rest: {
      baseline: [before],
      head: [after],
      changes: [
        {
          kind: "modified",
          before,
          after,
          aspects: [{ field: "request" }],
        },
      ],
      restBreakingCandidates: [
        {
          rule: "request-contract-changed",
          severity: "high",
          summary: "Request changed.",
          evidence: {
            operation: after.key,
            before: before.request,
            after: after.request,
          },
          reviewRequired: true,
        },
      ],
    },
    downstream: { changes: [], candidates: [] },
  });
  assert.deepEqual(project.rest.operationChanges[0].aspectChanges, {
    request: {
      before: { required: true, schemas: ["Widget"] },
      after: { required: true, schemas: ["NewWidget"] },
    },
  });
  assert.ok(JSON.stringify(project.rest.operationChanges).length < 500);
  assert.deepEqual(project.rest.breakingCandidates[0], {
    id: "rest-1-request-contract-changed-v1-post-widgets",
    rule: "request-contract-changed",
    severity: "high",
    summary: "Request changed.",
    evidence: {
      operation: "v1:POST:/widgets",
      before: { required: true, schemas: ["Widget"] },
      after: { required: true, schemas: ["NewWidget"] },
    },
    reviewRequired: true,
  });
  assert.deepEqual(project.downstream.candidates, [
    {
      id: "derived-rest-contract-sdk-impact",
      rule: "rest-contract-sdk-impact",
      severity: "high",
      summary:
        "Confirmed REST contract changes can require generated SDK signature, serialization, or response-shape changes.",
      evidence: [
        {
          candidateId: "rest-1-request-contract-changed-v1-post-widgets",
          operation: "v1:POST:/widgets",
        },
      ],
      reviewRequired: true,
    },
  ]);
});

test("compact drafts ignore artifact paths while retaining schema changes", () => {
  const before = {
    key: "v1:GET:/widgets",
    operationId: "Widgets_Get",
    apiVersion: "v1",
    method: "GET",
    path: "/widgets",
    parameters: [],
    request: null,
    responses: [
      {
        status: "200",
        schema: {
          reference: "common/v3/types.json#/definitions/Widget",
          properties: { name: { type: "string" } },
        },
      },
    ],
    sourceArtifact: "baseline/widgets.json",
  };
  const artifactOnly = {
    ...before,
    responses: [
      {
        status: "200",
        schema: {
          reference: "common/v5/types.json#/definitions/Widget",
          properties: { name: { type: "string" } },
        },
      },
    ],
    sourceArtifact: "head/widgets.json",
  };
  const schemaChanged = {
    ...artifactOnly,
    responses: [
      {
        status: "200",
        schema: {
          reference: "common/v5/types.json#/definitions/Widget",
          properties: {
            name: { type: "string" },
            size: { type: "integer" },
          },
        },
      },
    ],
  };
  const makeProject = (after) => ({
    path: "spec",
    rest: {
      baseline: [before],
      head: [after],
      changes: [
        {
          kind: "modified",
          before,
          after,
          aspects: [{ field: "responses" }],
        },
      ],
      restBreakingCandidates: [
        {
          rule: "response-contract-changed",
          evidence: { operation: after.key },
        },
      ],
    },
    downstream: { changes: [], candidates: [] },
  });

  const artifactOnlyProject = compactAnalysisProject(
    makeProject(artifactOnly),
  );
  assert.equal(artifactOnlyProject.rest.operationChanges.length, 0);
  assert.equal(artifactOnlyProject.rest.breakingCandidates.length, 0);

  const schemaChangedProject = compactAnalysisProject(
    makeProject(schemaChanged),
  );
  assert.deepEqual(
    schemaChangedProject.rest.operationChanges[0].changedAspects,
    ["responses"],
  );
  assert.equal(schemaChangedProject.rest.breakingCandidates.length, 1);
});

test("compact drafts omit unchanged operations inherited by a new API version", () => {
  const baseline = {
    key: "2025-01-01:GET:/widgets",
    operationId: "Widgets_Get",
    apiVersion: "2025-01-01",
    method: "GET",
    path: "/widgets",
    parameters: [
      {
        in: "query",
        name: "api-version",
        required: true,
        type: "string",
        default: "2025-01-01",
        contract: { enum: ["2025-01-01"] },
      },
    ],
    request: null,
    responses: [{ status: "200" }],
    lro: { isLongRunning: false },
    paging: { isPaged: false },
  };
  const unchanged = {
    ...baseline,
    key: "2025-02-01:GET:/widgets",
    apiVersion: "2025-02-01",
    parameters: [
      {
        ...baseline.parameters[0],
        default: "2025-02-01",
        contract: { enum: ["2025-02-01"] },
      },
    ],
  };
  const modified = {
    ...unchanged,
    key: "2025-02-01:POST:/widgets",
    operationId: "Widgets_Create",
    method: "POST",
    request: { required: true },
  };
  const project = compactAnalysisProject({
    path: "spec",
    sourceReferences: [],
    rest: {
      baseline: [
        baseline,
        {
          ...baseline,
          key: "2025-01-01:POST:/widgets",
          operationId: "Widgets_Create",
          method: "POST",
          request: null,
        },
      ],
      head: [unchanged, modified],
      changes: [
        { kind: "added", after: unchanged, aspects: [] },
        { kind: "added", after: modified, aspects: [] },
      ],
      restBreakingCandidates: [],
    },
    downstream: { changes: [], candidates: [] },
  });
  assert.deepEqual(project.rest.operationGroups, [
    {
      id: "version-modified-2025-02-01-widgets-material-change",
      kind: "version-modified",
      behavior: "material-change",
      apiVersion: "2025-02-01",
      family: "Widgets",
      operationIds: ["Widgets_Create"],
      changedAspects: ["request"],
      changes: [
        {
          operationId: "Widgets_Create",
          aspects: {
            request: {
              before: null,
              after: { required: true, schemas: undefined },
            },
          },
        },
      ],
    },
  ]);
  assert.equal(project.rest.counts.changedOperations, 2);
  assert.equal(project.rest.counts.modelRelevantOperations, 1);
});

test("operation groups link to versioned model owners and operation files", () => {
  const operation = {
    key: "2025-02-01:GET:/widgets",
    operationId: "Widgets_Get",
    apiVersion: "2025-02-01",
    method: "GET",
    path: "/widgets",
  };
  const draft = buildAssessmentDraft({
    evidence: {
      baseline: { commit: "base" },
      head: { commit: "head" },
      changedFiles: [
        "spec/Widget.tsp",
        "spec/WidgetSelector.tsp",
        "spec/models.tsp",
      ],
      typeSpecDiffs: [
        {
          path: "spec/Widget.tsp",
          context: "interface Widgets {",
          oldStart: 1,
          newStart: 1,
          lines: ["+op get(): Widget;"],
        },
        {
          path: "spec/models.tsp",
          context: "model WidgetProperties {",
          oldStart: 1,
          newStart: 1,
          lines: ["+@added(Versions.v2025_02_01)", "+displayName?: string;"],
        },
        {
          path: "spec/WidgetSelector.tsp",
          context: "interface WidgetSelectors {",
          oldStart: 1,
          newStart: 1,
          lines: ["+op list(): WidgetSelector[];"],
        },
      ],
      errors: [],
      durationMs: 1,
      phaseDurations: { deterministicAnalysisMs: 1 },
    },
    analysis: {
      durationMs: 1,
      sourceIndex: [],
      projects: [
        {
          path: "spec",
          rest: {
            baseline: [],
            head: [operation],
            changes: [{ kind: "added", after: operation, aspects: [] }],
            restBreakingCandidates: [],
          },
          downstream: { changes: [], candidates: [] },
        },
      ],
    },
    complianceEvidence: { durationMs: 1, documents: [] },
    totalMs: 2,
  });

  assert.deepEqual(draft.projects[0].rest.operationGroups[0].sourceLinks, [
    {
      path: "spec/models.tsp",
      owners: ["WidgetProperties"],
      sourceChangeIds: ["spec/models.tsp:1:1"],
    },
    {
      path: "spec/Widget.tsp",
      owners: undefined,
      sourceChangeIds: ["spec/Widget.tsp:1:1"],
    },
  ]);
});

test("semantic review units split material behavior and added families", () => {
  const units = buildSemanticReviewUnits([
    {
      path: "spec",
      rest: {
        operationChanges: [],
        operationGroups: [
          {
            id: "version-widgets",
            kind: "version-modified",
            behavior: "version-propagation",
            apiVersion: "2025-09-01",
            family: "Widgets",
            operationIds: ["Widgets_Get"],
            sourceLinks: [
              {
                path: "spec/Widget.tsp",
                sourceChangeIds: ["source-version"],
              },
            ],
          },
          {
            id: "material-firewall",
            kind: "version-modified",
            behavior: "material-change",
            apiVersion: "2025-09-01",
            family: "FirewallPolicies",
            operationIds: ["FirewallPolicies_CreateOrUpdate"],
            changedAspects: ["parameters"],
            sourceLinks: [
              {
                path: "spec/FirewallPolicy.tsp",
                sourceChangeIds: ["source-afc"],
              },
            ],
          },
          {
            id: "added-prefix-sets",
            kind: "added",
            behavior: "added",
            apiVersion: "2025-09-01",
            family: "AddressPrefixSets",
            operationIds: ["AddressPrefixSets_Get"],
            sourceLinks: [
              {
                path: "spec/AddressPrefixSet.tsp",
                sourceChangeIds: ["source-prefix-set"],
              },
            ],
          },
          {
            id: "added-lags",
            kind: "added",
            behavior: "added",
            apiVersion: "2025-09-01",
            family: "ExpressRouteLags",
            operationIds: ["ExpressRouteLags_Get"],
            sourceLinks: [
              {
                path: "spec/ExpressRouteLag.tsp",
                sourceChangeIds: ["source-lag"],
              },
            ],
          },
        ],
      },
    },
  ]);

  assert.equal(units.length, 4);
  assert.ok(
    units.some(
      (unit) =>
        unit.family === "version-lineage" &&
        unit.kind === "version-propagation",
    ),
  );
  assert.ok(
    units.some(
      (unit) =>
        unit.family === "AddressPrefixSets" && unit.behavior === "added",
    ),
  );
  assert.ok(
    units.some(
      (unit) => unit.family === "ExpressRouteLags" && unit.behavior === "added",
    ),
  );
  const firewallUnit = units.find(
    (unit) => unit.family === "FirewallPolicies",
  );
  assert.equal(firewallUnit.behavior, "parameters");
  assert.deepEqual(firewallUnit.sourceChangeIds, ["source-afc"]);
});

test("material semantic review units require exact source evidence", () => {
  assert.throws(
    () =>
      buildSemanticReviewUnits([
        {
          path: "spec",
          rest: {
            operationChanges: [],
            operationGroups: [
              {
                id: "material-firewall",
                kind: "version-modified",
                behavior: "material-change",
                apiVersion: "2025-09-01",
                family: "FirewallPolicies",
                operationIds: ["FirewallPolicies_CreateOrUpdate"],
                changedAspects: ["parameters"],
                sourceLinks: [],
              },
            ],
          },
        },
      ]),
    /have no changed TypeSpec source evidence/,
  );
});

test("related material operations share one family and behavior unit", () => {
  const units = buildSemanticReviewUnits(
    [
      {
        path: "spec",
        rest: {
          operationChanges: [
            {
              id: "get-widget",
              apiVersion: "v1",
              operationId: "Widgets_Get",
              changedAspects: ["responses"],
            },
            {
              id: "list-widgets",
              apiVersion: "v1",
              operationId: "Widgets_List",
              changedAspects: ["responses"],
            },
          ],
          operationGroups: [],
        },
        downstream: { candidates: [] },
      },
    ],
    [
      {
        path: "spec/Widget.tsp",
        versionedMembers: [],
        changes: [{ id: "source-widget" }],
      },
    ],
  );

  assert.equal(units.length, 1);
  assert.deepEqual(units[0].operationChangeIds, [
    "get-widget",
    "list-widgets",
  ]);
  assert.equal(units[0].family, "Widgets");
  assert.equal(units[0].behavior, "responses");
});

test("source-only model and enum behaviors remain separate", () => {
  const units = buildSemanticReviewUnits(
    [
      {
        path: "spec",
        rest: { operationChanges: [], operationGroups: [] },
        downstream: { candidates: [] },
      },
    ],
    [
      {
        path: "spec/models.tsp",
        versionedMembers: [],
        changes: [
          {
            id: "source-enum",
            diffLines: [{ kind: "remove", text: 'Fifo: "Fifo",' }],
          },
          {
            id: "source-model",
            diffLines: [
              { kind: "remove", text: "model FifoItem {" },
              { kind: "remove", text: "name: string;" },
            ],
          },
          {
            id: "source-version",
            diffLines: [
              {
                kind: "remove",
                text: "@added(Versions.v2026_12_06)",
              },
            ],
          },
        ],
      },
    ],
  );

  assert.deepEqual(
    units.map((unit) => [unit.behavior, unit.sourceChangeIds]),
    [
      ["enum-values", ["source-enum"]],
      ["model-shape", ["source-model", "source-version"]],
    ],
  );
});

test("fast source changes retain commit-pinned TypeSpec links", () => {
  const draft = buildAssessmentDraft({
    evidence: {
      baseline: { commit: "base" },
      head: { commit: "head" },
      changedFiles: ["spec/main.tsp"],
      sourceReferences: [
        {
          path: "spec/main.tsp",
          revision: "head",
          startLine: 11,
          endLine: 12,
          link: "https://github.com/Azure/example/blob/head/spec/main.tsp#L11-L12",
        },
      ],
      typeSpecDiffs: [
        {
          path: "spec/main.tsp",
          context: "model Widget",
          oldStart: 10,
          oldCount: 2,
          newStart: 10,
          newCount: 3,
          lines: [" model Widget {", "+  name?: string;", " }"],
        },
      ],
      errors: [],
      durationMs: 1,
      phaseDurations: { deterministicAnalysisMs: 1 },
    },
    analysis: {
      durationMs: 1,
      sourceIndex: [],
      projects: [],
    },
    complianceEvidence: { durationMs: 1, documents: [] },
    totalMs: 2,
  });
  assert.equal(
    draft.sourceFiles[0].changes[0].sourceLink,
    "https://github.com/Azure/example/blob/head/spec/main.tsp#L10-L12",
  );
});

test("added paging decorators become downstream review candidates", () => {
  const draft = buildAssessmentDraft({
    evidence: {
      baseline: { commit: "base" },
      head: { commit: "head" },
      changedFiles: ["spec/main.tsp"],
      typeSpecDiffs: [
        {
          path: "spec/main.tsp",
          oldStart: 1,
          newStart: 1,
          lines: ["+@list", "+@pageItems", "+@nextLink"],
        },
      ],
      errors: [],
      durationMs: 1,
      phaseDurations: { deterministicAnalysisMs: 1 },
    },
    analysis: {
      durationMs: 1,
      sourceIndex: [
        {
          symbol: "@list",
          kind: "decorator",
          change: "added",
          path: "spec/main.tsp",
        },
        {
          symbol: "@pageItems",
          kind: "decorator",
          change: "added",
          path: "spec/main.tsp",
        },
        {
          symbol: "@nextLink",
          kind: "decorator",
          change: "added",
          path: "spec/main.tsp",
        },
      ],
      projects: [
        {
          path: "spec",
          rest: {
            baseline: [],
            head: [],
            changes: [],
            restBreakingCandidates: [],
          },
          downstream: { changes: [], candidates: [] },
        },
      ],
    },
    complianceEvidence: { durationMs: 1, documents: [] },
    totalMs: 2,
  });
  assert.deepEqual(draft.projects[0].downstream.candidates, [
    {
      id: "source-paging-metadata-added",
      rule: "paging-metadata-added",
      severity: "medium",
      summary:
        "Added paging metadata can change generated SDK return and iteration shapes while preserving the REST wire contract.",
      evidence: [
        { path: "spec/main.tsp", symbol: "@list", count: 1 },
        { path: "spec/main.tsp", symbol: "@nextLink", count: 1 },
        { path: "spec/main.tsp", symbol: "@pageItems", count: 1 },
      ],
      reviewRequired: true,
    },
  ]);
});

test("assessment drafts identify ARM actions changed from async to sync", () => {
  const path = "specification/network/resource-manager/Microsoft.Network/Network";
  const sourcePath = `${path}/ServiceGateway.tsp`;
  const draft = buildAssessmentDraft({
    evidence: {
      baseline: { commit: "base" },
      head: { commit: "head", hasWorkingTreeChanges: false, changeScope: [] },
      changedFiles: [sourcePath],
      typeSpecDiffs: [
        {
          path: sourcePath,
          oldStart: 1,
          newStart: 1,
          lines: [
            "-  updateServices is ArmResourceActionAsync<ServiceGateway, void, void>;",
            "+  updateServices is ArmResourceActionSync<ServiceGateway, void, void>;",
          ],
        },
      ],
      errors: [],
      durationMs: 1,
      phaseDurations: {},
    },
    analysis: {
      durationMs: 1,
      sourceIndex: [],
      projects: [
        {
          path,
          rest: {
            baseline: [],
            head: [],
            changes: [],
            restBreakingCandidates: [],
          },
          downstream: { candidates: [], changes: [] },
        },
      ],
    },
    complianceEvidence: { durationMs: 1, documents: [] },
    totalMs: 2,
  });
  const candidate = draft.projects[0].downstream.candidates.find(
    ({ id }) => id === "source-arm-action-changed-from-async-to-sync",
  );
  assert.equal(candidate.rule, "sdk-lro-to-synchronous");
  assert.deepEqual(candidate.evidence, [
    {
      path: sourcePath,
      symbols: ["updateServices"],
    },
  ]);
  assert.match(
    draft.sourceFiles[0].changes[0].lines.join("\n"),
    /ArmResourceActionSync/,
  );
});

test("PR 43308-style LRO cleanup does not synthesize downstream impact", () => {
  const sourcePath = "spec/lro-helpers.tsp";
  const draft = buildAssessmentDraft({
    evidence: {
      baseline: { commit: "base" },
      head: { commit: "head", hasWorkingTreeChanges: false, changeScope: [] },
      changedFiles: [sourcePath],
      typeSpecDiffs: [
        {
          path: sourcePath,
          oldStart: 1,
          newStart: 1,
          lines: [
            '-@extension("x-ms-long-running-operation", true)',
            '-@extension("x-ms-long-running-operation-options", #{ `final-state-via`: "location" })',
            "-@pollingOperation(ScenarioRuns.get)",
            '+@Azure.Core.useFinalStateVia("location")',
          ],
        },
      ],
      errors: [],
      durationMs: 1,
      phaseDurations: {},
    },
    analysis: {
      durationMs: 1,
      sourceIndex: [],
      projects: [
        {
          path: "spec",
          rest: {
            baseline: [],
            head: [],
            changes: [],
            restBreakingCandidates: [],
          },
          downstream: { candidates: [], changes: [] },
        },
      ],
    },
    complianceEvidence: { durationMs: 1, documents: [] },
    totalMs: 2,
  });
  assert.equal(draft.projects[0].downstream.candidates.length, 0);
});

test("assessment drafts collapse repeated decorators and bound source lines", () => {
  const draft = buildAssessmentDraft({
    evidence: {
      baseline: { commit: "base" },
      head: { commit: "head" },
      changedFiles: ["spec/models.tsp"],
      typeSpecDiffs: [
        {
          path: "spec/models.tsp",
          oldStart: 1,
          newStart: 1,
          lines: [
            " model Widget {",
            "+@added(Versions.v2)",
            "+newProperty?: string;",
            ...Array.from(
              { length: 498 },
              (_, index) => `+property${index}?: string;`,
            ),
          ],
        },
      ],
      errors: [],
      durationMs: 1,
      phaseDurations: { deterministicAnalysisMs: 1 },
    },
    analysis: {
      durationMs: 1,
      sourceIndex: [
        ...Array.from({ length: 500 }, (_, index) => ({
          symbol: "@added",
          kind: "decorator",
          change: "added",
          path: "spec/models.tsp",
          revision: "head",
          line: index + 1,
        })),
        {
          symbol: "Widget",
          kind: "model",
          change: "added",
          path: "spec/models.tsp",
          revision: "head",
          line: 1,
        },
      ],
      projects: [],
    },
    complianceEvidence: { durationMs: 1, documents: [] },
    totalMs: 2,
  });
  assert.deepEqual(draft.sourceFiles[0].decorators, [
    { symbol: "@added", change: "added", count: 500 },
  ]);
  assert.equal(draft.sourceFiles[0].declarations.length, 1);
  assert.deepEqual(draft.sourceFiles[0].versionedMembers, [
    {
      owner: "Widget",
      symbol: "newProperty",
      version: "v2",
      sourceChangeId: "spec/models.tsp:1:1",
    },
  ]);
  assert.equal(draft.sourceFiles[0].changes[0].lines.length, 200);
  assert.equal(draft.sourceFiles[0].changes[0].omittedLineCount, 300);
});

test("model input budgets report size and reject oversized drafts", () => {
  const measured = applyModelInputBudget({ payload: "x".repeat(100) }, 1000);
  assert.equal(measured.modelInput.serialization, "minified-json");
  assert.equal(
    measured.modelInput.bytes,
    Buffer.byteLength(JSON.stringify(measured)),
  );
  assert.equal(
    measured.modelInput.estimatedTokens,
    Math.ceil(measured.modelInput.bytes / 4),
  );
  assert.throws(
    () => applyModelInputBudget({ payload: "x".repeat(1000) }, 100),
    /exceeding the 100-byte budget/,
  );
});

test("offline analysis retains authoritative document excerpts without findings", () => {
  const root = mkdtempSync(join(tmpdir(), "assessment-compliance-"));
  try {
    const assessmentPath = join(root, "assessment.json");
    writeFileSync(
      assessmentPath,
      JSON.stringify({
        dimensions: {
          azureCompliance: {
            documents: [
              {
                title: "Stable after preview",
                url: "https://example.test/versioning",
                section: "Version enum",
                guidanceExcerpt: "Remove the replaced preview version.",
                expectedCodeSnippets: [
                  { lines: ["enum Versions {", "  v2: \"v2\",", "}"] },
                ],
              },
            ],
            findings: [{ id: "must-not-be-copied" }],
          },
        },
      }),
    );
    const merged = mergeHistoricalComplianceDocuments(
      { documents: [] },
      assessmentPath,
    );
    assert.equal(merged.documents[0].cache, "retained-assessment");
    assert.equal(
      merged.documents[0].matchingExcerpt,
      "Remove the replaced preview version.",
    );
    assert.deepEqual(merged.documents[0].candidateCodeBlocks, [
      'enum Versions {\n  v2: "v2",\n}',
    ]);
    assert.equal(JSON.stringify(merged).includes("must-not-be-copied"), false);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
