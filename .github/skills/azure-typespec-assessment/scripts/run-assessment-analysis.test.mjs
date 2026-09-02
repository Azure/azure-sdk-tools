import assert from "node:assert/strict";
import test from "node:test";
import { buildModelInput } from "./run-assessment-analysis.mjs";

test("model input keeps only transitively referenced facts and sources", () => {
  const input = buildModelInput({
    manifest: {
      comparison: {
        mergeBaseCommit: "base",
        headCommit: "head",
        baseRef: "origin/main",
        workingTree: {},
      },
      projects: [{ id: "p", path: "specification/widget" }],
      blockers: [],
    },
    sourceIndex: {
      sourceChanges: [
        {
          id: "source-1",
          path: "a.tsp",
          hunks: [{ id: "hunk-1", lines: ["+model Widget {}"] }],
          declarations: [
            {
              id: "declaration-1",
              kind: "model",
              qualifiedName: "Widget",
              hunkIds: ["hunk-1"],
            },
          ],
        },
        { id: "source-unused", path: "b.tsp", hunks: [], declarations: [] },
      ],
    },
    semantic: {
      status: "ready",
      facts: {
        "operation-1": { operationId: "Widgets_Get" },
        "operation-unused": { operationId: "Unused" },
      },
      reviewUnits: [
        {
          id: "semantic-1",
          sourceChangeIds: ["source-1"],
          hunkIds: ["hunk-1"],
          declarationIds: ["declaration-1"],
          operationIds: ["operation-1"],
          beforeFactIds: [],
          afterFactIds: ["operation-1"],
        },
      ],
      blockers: [],
    },
    rest: {
      status: "ready",
      facts: {},
      candidates: [],
      blockers: [],
    },
    downstream: {
      status: "ready",
      facts: {},
      candidates: [],
      blockers: [],
    },
  });
  assert.deepEqual(Object.keys(input.sourceChanges), ["source-1"]);
  assert.deepEqual(Object.keys(input.facts), []);
  assert.equal(input.semanticReviewUnits[0].affectedOperationCount, 1);
  assert.deepEqual(input.semanticReviewUnits[0].representativeOperationIds, [
    "operation-1",
  ]);
  assert.equal(input.complianceSearchRequests.length, 1);
  assert.equal(input.complianceSearchRequests[0].reviewUnitId, "semantic-1");
  assert.match(
    input.complianceSearchRequests[0].requestId,
    /^compliance-search-/,
  );
  assert.deepEqual(
    input.semanticReviewUnits[0].deterministicCoverage.coveredHunkIds,
    ["hunk-1"],
  );
  assert.deepEqual(
    input.semanticReviewUnits[0].deterministicCoverage.uncoveredHunkIds,
    [],
  );
  assert.equal(input.semanticReviewUnits[0].inferenceRequired, false);
  assert.deepEqual(input.inferenceRequests, []);
  assert.deepEqual(input.deferredDimensions, {
    documentQuality: "not-assessed",
  });
  assert.equal(input.inputAccounting.budgetTier, "small");
  assert.equal(
    input.inputAccounting.omittedRedundant.rawEmitterArtifacts,
    true,
  );
});

test("model input requests inference only for unknown hunks", () => {
  const input = buildModelInput({
    manifest: {
      comparison: {
        mergeBaseCommit: "base",
        headCommit: "head",
        baseRef: "origin/main",
        workingTree: {},
      },
      projects: [{ id: "p", path: "specification/widget" }],
      blockers: [],
    },
    sourceIndex: {
      sourceChanges: [
        {
          id: "source-1",
          path: "back-compatible.tsp",
          hunks: [
            {
              id: "hunk-1",
              lines: [
                "@@clientLocation(Widgets.get, Contoso,",
                '-  "!csharp"',
                '+  "!csharp,!go"',
                ");",
              ],
            },
          ],
          declarations: [
            {
              id: "declaration-1",
              hunkIds: ["hunk-1"],
              kind: "model",
              qualifiedName: "Widget",
            },
          ],
        },
      ],
    },
    semantic: {
      status: "ready",
      facts: {
        "operation-1": {
          operationId: "Widgets_Get",
          method: "get",
          path: "/widgets",
        },
      },
      reviewUnits: [
        {
          id: "semantic-1",
          sourceChangeIds: ["source-1"],
          hunkIds: ["hunk-1"],
          declarationIds: ["declaration-1"],
          operations: [
            {
              operationId: "Widgets_Get",
              hunkIds: ["hunk-1"],
              afterFactId: "operation-1",
            },
          ],
        },
      ],
      blockers: [],
    },
    rest: {
      status: "ready",
      facts: {},
      candidates: [
        {
          id: "rest-1",
          operationIds: ["Widgets_Get"],
          sourceChangeIds: ["source-1"],
          evidenceFactIds: [],
        },
      ],
      blockers: [],
    },
    downstream: {
      status: "ready",
      facts: {},
      candidates: [],
      blockers: [],
    },
  });

  assert.equal(input.semanticReviewUnits[0].inferenceRequired, true);
  assert.deepEqual(
    input.semanticReviewUnits[0].deterministicCoverage.uncoveredHunkIds,
    ["hunk-1"],
  );
  assert.equal(input.inferenceRequests.length, 1);
  assert.match(input.inferenceRequests[0].requestId, /^inference-request-/);
  assert.equal(input.inferenceRequests[0].hunkId, "hunk-1");
  assert.deepEqual(input.inferenceRequests[0].allowedDimensions, [
    "rest",
    "downstream",
  ]);
});

test("model input keeps unsupported unmapped decorators unknown", () => {
  const input = buildModelInput({
    manifest: {
      comparison: {
        mergeBaseCommit: "base",
        headCommit: "head",
        baseRef: "origin/main",
        workingTree: {},
      },
      projects: [{ id: "p", path: "specification/widget" }],
      blockers: [],
    },
    sourceIndex: {
      sourceChanges: [
        {
          id: "source-1",
          path: "client.tsp",
          hunks: [
            {
              id: "hunk-1",
              lines: [
                "-@@access(Widget, Access.public);",
                "+@@access(Widget, Access.internal);",
              ],
            },
          ],
          declarations: [],
        },
      ],
    },
    semantic: {
      status: "ready",
      facts: {},
      reviewUnits: [
        {
          id: "semantic-1",
          sourceChangeIds: ["source-1"],
          hunkIds: ["hunk-1"],
          declarationIds: [],
          operations: [],
        },
      ],
      blockers: [],
    },
    rest: { status: "ready", facts: {}, candidates: [], blockers: [] },
    downstream: { status: "ready", facts: {}, candidates: [], blockers: [] },
  });

  assert.equal(input.semanticReviewUnits[0].inferenceRequired, true);
  assert.equal(
    input.semanticReviewUnits[0].deterministicCoverage.classifications[0]
      .reason,
    "unsupported-customization-not-represented",
  );
});

test("known decorators do not mask unsupported changes in the same hunk", () => {
  for (const lines of [
    [
      '+@@operationId(Widgets.get, "Widgets_Get");',
      "+@@access(Widget, Access.internal);",
    ],
    [
      ' @@operationId(Widgets.get, "Widgets_Get");',
      "+@@access(Widget, Access.internal);",
    ],
    [
      '+@@clientName(Widget, "RenamedWidget");',
      "+@@access(Widget, Access.internal);",
    ],
  ]) {
    const input = buildModelInput({
      manifest: {
        comparison: {
          mergeBaseCommit: "base",
          headCommit: "head",
          baseRef: "origin/main",
          workingTree: {},
        },
        projects: [{ id: "p", path: "specification/widget" }],
        blockers: [],
      },
      sourceIndex: {
        sourceChanges: [
          {
            id: "source-1",
            path: "client.tsp",
            hunks: [{ id: "hunk-1", lines }],
            declarations: [],
          },
        ],
      },
      semantic: {
        status: "ready",
        facts: {},
        reviewUnits: [
          {
            id: "semantic-1",
            sourceChangeIds: ["source-1"],
            hunkIds: ["hunk-1"],
            declarationIds: [],
            operations: [],
          },
        ],
        blockers: [],
      },
      rest: { status: "ready", facts: {}, candidates: [], blockers: [] },
      downstream: { status: "ready", facts: {}, candidates: [], blockers: [] },
    });

    assert.equal(input.semanticReviewUnits[0].inferenceRequired, true);
    assert.equal(input.inferenceRequests.length, 1);
    assert.equal(
      input.semanticReviewUnits[0].deterministicCoverage.classifications[0]
        .reason,
      "unsupported-customization-not-represented",
    );
  }
});

test("multiline decorator bodies remain inference-visible", () => {
  for (const lines of [
    [
      " @@clientName(Widget,",
      '-  "GoWidget",',
      '+  "RenamedWidget",',
      '   "go")',
    ],
    [" @pattern(", '-  "old"', '+  "new"', " )"],
  ]) {
    const input = buildModelInput({
      manifest: {
        comparison: {
          mergeBaseCommit: "base",
          headCommit: "head",
          baseRef: "origin/main",
          workingTree: {},
        },
        projects: [{ id: "p", path: "specification/widget" }],
        blockers: [],
      },
      sourceIndex: {
        sourceChanges: [
          {
            id: "source-1",
            path: "client.tsp",
            hunks: [{ id: "hunk-1", lines }],
            declarations: [],
          },
        ],
      },
      semantic: {
        status: "ready",
        facts: {},
        reviewUnits: [
          {
            id: "semantic-1",
            projectId: "p",
            projectIds: ["p"],
            sourceChangeIds: ["source-1"],
            hunkIds: ["hunk-1"],
            declarationIds: [],
            operations: [],
          },
        ],
        blockers: [],
      },
      rest: { status: "ready", facts: {}, candidates: [], blockers: [] },
      downstream: { status: "ready", facts: {}, candidates: [], blockers: [] },
    });

    assert.equal(input.semanticReviewUnits[0].inferenceRequired, true);
    assert.equal(input.inferenceRequests.length, 1);
  }
});

test("context-only scoped decorators do not force inference", () => {
  const input = buildModelInput({
    manifest: {
      comparison: {
        mergeBaseCommit: "base",
        headCommit: "head",
        baseRef: "origin/main",
        workingTree: {},
      },
      projects: [{ id: "p", path: "specification/widget" }],
      blockers: [],
    },
    sourceIndex: {
      sourceChanges: [
        {
          id: "source-1",
          path: "main.tsp",
          hunks: [
            {
              id: "hunk-1",
              lines: [
                ' @@clientLocation(Widgets.get, Contoso, "go");',
                "-model Widget {}",
                "+model Widget { name?: string; }",
              ],
            },
          ],
          declarations: [
            {
              id: "declaration-1",
              hunkIds: ["hunk-1"],
              kind: "model",
              qualifiedName: "Widget",
            },
          ],
        },
      ],
    },
    semantic: {
      status: "ready",
      facts: {},
      reviewUnits: [
        {
          id: "semantic-1",
          sourceChangeIds: ["source-1"],
          hunkIds: ["hunk-1"],
          declarationIds: ["declaration-1"],
          operations: [],
        },
      ],
      blockers: [],
    },
    rest: { status: "ready", facts: {}, candidates: [], blockers: [] },
    downstream: { status: "ready", facts: {}, candidates: [], blockers: [] },
  });

  assert.equal(input.semanticReviewUnits[0].inferenceRequired, false);
});

test("parentheses in context decorator strings do not leak decorator scope", () => {
  const input = buildModelInput({
    manifest: {
      comparison: {
        mergeBaseCommit: "base",
        headCommit: "head",
        baseRef: "origin/main",
        workingTree: {},
      },
      projects: [{ id: "p", path: "specification/widget" }],
      blockers: [],
    },
    sourceIndex: {
      sourceChanges: [
        {
          id: "source-1",
          path: "main.tsp",
          hunks: [
            {
              id: "hunk-1",
              lines: [
                ' @@clientLocation(Widgets.get, Contoso, "go(");',
                "-model Widget {}",
                "+model Widget { name?: string; }",
              ],
            },
          ],
          declarations: [
            {
              id: "declaration-1",
              hunkIds: ["hunk-1"],
              kind: "model",
              qualifiedName: "Widget",
            },
          ],
        },
      ],
    },
    semantic: {
      status: "ready",
      facts: {},
      reviewUnits: [
        {
          id: "semantic-1",
          sourceChangeIds: ["source-1"],
          hunkIds: ["hunk-1"],
          declarationIds: ["declaration-1"],
          operations: [],
        },
      ],
      blockers: [],
    },
    rest: { status: "ready", facts: {}, candidates: [], blockers: [] },
    downstream: { status: "ready", facts: {}, candidates: [], blockers: [] },
  });

  assert.equal(input.semanticReviewUnits[0].inferenceRequired, false);
});

test("direct doc decorator changes remain semantic-only", () => {
  const input = buildModelInput({
    manifest: {
      comparison: {
        mergeBaseCommit: "base",
        headCommit: "head",
        baseRef: "origin/main",
        workingTree: {},
      },
      projects: [{ id: "p", path: "specification/widget" }],
      blockers: [],
    },
    sourceIndex: {
      sourceChanges: [
        {
          id: "source-1",
          path: "main.tsp",
          hunks: [
            {
              id: "hunk-1",
              lines: ['-@doc("old")', '+@doc("new")'],
            },
          ],
          declarations: [],
        },
      ],
    },
    semantic: {
      status: "ready",
      facts: {},
      reviewUnits: [
        {
          id: "semantic-1",
          sourceChangeIds: ["source-1"],
          hunkIds: ["hunk-1"],
          declarationIds: [],
          operations: [],
        },
      ],
      blockers: [],
    },
    rest: { status: "ready", facts: {}, candidates: [], blockers: [] },
    downstream: { status: "ready", facts: {}, candidates: [], blockers: [] },
  });

  assert.equal(input.semanticReviewUnits[0].inferenceRequired, false);
  assert.equal(
    input.semanticReviewUnits[0].deterministicCoverage.classifications[0]
      .status,
    "semantic-only",
  );
});

test("direct unsupported decorators remain inference-visible", () => {
  const input = buildModelInput({
    manifest: {
      comparison: {
        mergeBaseCommit: "base",
        headCommit: "head",
        baseRef: "origin/main",
        workingTree: {},
      },
      projects: [{ id: "p", path: "specification/widget" }],
      blockers: [],
    },
    sourceIndex: {
      sourceChanges: [
        {
          id: "source-1",
          path: "main.tsp",
          hunks: [
            {
              id: "hunk-1",
              lines: [
                '-@myCustom("old")',
                '+@myCustom("new")',
                " model Widget {}",
              ],
            },
          ],
          declarations: [
            {
              id: "declaration-1",
              hunkIds: ["hunk-1"],
              kind: "model",
              qualifiedName: "Widget",
            },
          ],
        },
      ],
    },
    semantic: {
      status: "ready",
      facts: {},
      reviewUnits: [
        {
          id: "semantic-1",
          sourceChangeIds: ["source-1"],
          hunkIds: ["hunk-1"],
          declarationIds: ["declaration-1"],
          operations: [],
        },
      ],
      blockers: [],
    },
    rest: { status: "ready", facts: {}, candidates: [], blockers: [] },
    downstream: { status: "ready", facts: {}, candidates: [], blockers: [] },
  });

  assert.equal(input.semanticReviewUnits[0].inferenceRequired, true);
  assert.equal(
    input.semanticReviewUnits[0].deterministicCoverage.classifications[0]
      .reason,
    "unsupported-customization-not-represented",
  );
});

test("mapped hunks remain blocked when deterministic analysis is blocked", () => {
  const input = buildModelInput({
    manifest: {
      comparison: {
        mergeBaseCommit: "base",
        headCommit: "head",
        baseRef: "origin/main",
        workingTree: {},
      },
      projects: [{ id: "p", path: "specification/widget" }],
      blockers: [],
    },
    sourceIndex: {
      sourceChanges: [
        {
          id: "source-1",
          path: "main.tsp",
          hunks: [{ id: "hunk-1", lines: ["+op getWidget(): Widget;"] }],
          declarations: [],
        },
      ],
    },
    semantic: {
      status: "ready",
      facts: {},
      reviewUnits: [
        {
          id: "semantic-1",
          sourceChangeIds: ["source-1"],
          hunkIds: ["hunk-1"],
          declarationIds: [],
          operations: [{ operationId: "Widgets_Get", hunkIds: ["hunk-1"] }],
        },
      ],
      blockers: [],
    },
    rest: {
      status: "blocked",
      facts: {},
      candidates: [],
      blockers: [{ code: "rest-blocked", message: "REST unavailable." }],
    },
    downstream: { status: "ready", facts: {}, candidates: [], blockers: [] },
  });

  assert.equal(
    input.semanticReviewUnits[0].deterministicCoverage.classifications[0]
      .status,
    "blocked",
  );
  assert.equal(input.semanticReviewUnits[0].inferenceRequired, false);
});

test("model input retains facts relevant to inference hunks", () => {
  const input = buildModelInput({
    manifest: {
      comparison: {
        mergeBaseCommit: "base",
        headCommit: "head",
        baseRef: "origin/main",
        workingTree: {},
      },
      projects: [{ id: "p", path: "specification/widget" }],
      blockers: [],
    },
    sourceIndex: {
      sourceChanges: [
        {
          id: "source-1",
          path: "client.tsp",
          hunks: [
            {
              id: "hunk-1",
              lines: [
                "-@@access(Widget, Access.public);",
                "+@@access(Widget, Access.internal);",
              ],
            },
          ],
          declarations: [],
        },
      ],
    },
    semantic: {
      status: "ready",
      facts: {},
      reviewUnits: [
        {
          id: "semantic-1",
          projectId: "p",
          projectIds: ["p"],
          sourceChangeIds: ["source-1"],
          hunkIds: ["hunk-1"],
          declarationIds: [],
          operations: [],
        },
      ],
      blockers: [],
    },
    rest: { status: "ready", facts: {}, candidates: [], blockers: [] },
    downstream: {
      status: "ready",
      facts: {
        "sdk-fact-widget": {
          id: "sdk-fact-widget",
          projectId: "p",
          comparisonRole: "target",
          sourceRevision: "current",
          sourceCommit: "head",
          apiVersions: ["2025-01-01"],
          factKind: "model",
          identity: "Contoso.Widget",
          crossLanguageDefinitionId: "Contoso.Widget",
          name: "Widget",
          operation: {
            operationId: "Widgets_Get",
            method: "get",
            path: "/widgets",
          },
          access: "public",
          usage: 1,
          properties: [],
          reachable: true,
        },
      },
      candidates: [],
      blockers: [],
    },
  });

  assert.deepEqual(Object.keys(input.facts), ["sdk-fact-widget"]);
  assert.equal(input.facts["sdk-fact-widget"].comparisonRole, "target");
  assert.equal(input.facts["sdk-fact-widget"].sourceRevision, "current");
  assert.equal(input.facts["sdk-fact-widget"].sourceCommit, "head");
  assert.deepEqual(input.facts["sdk-fact-widget"].apiVersions, ["2025-01-01"]);
  assert.deepEqual(input.facts["sdk-fact-widget"].operation, {
    operationId: "Widgets_Get",
    method: "get",
    path: "/widgets",
  });
});
