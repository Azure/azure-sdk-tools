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
        { id: "source-1", path: "a.tsp", hunks: [], declarations: [] },
        { id: "source-unused", path: "b.tsp", hunks: [], declarations: [] },
      ],
    },
    semantic: {
      status: "ready",
      facts: {
        "operation-1": { operationId: "Widgets_Get" },
        "operation-unused": { operationId: "Unused" },
      },
      reviewUnits: [{
        id: "semantic-1",
        sourceChangeIds: ["source-1"],
        operationIds: ["operation-1"],
        beforeFactIds: [],
        afterFactIds: ["operation-1"],
      }],
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
  assert.deepEqual(input.semanticReviewUnits[0].representativeOperationIds, ["operation-1"]);
  assert.equal(input.complianceSearchRequests.length, 1);
  assert.equal(input.complianceSearchRequests[0].reviewUnitId, "semantic-1");
  assert.deepEqual(input.deferredDimensions, { documentQuality: "not-assessed" });
  assert.equal(input.inputAccounting.budgetTier, "small");
  assert.equal(input.inputAccounting.omittedRedundant.rawEmitterArtifacts, true);
});
