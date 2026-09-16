import assert from "node:assert/strict";
import test from "node:test";
import { buildDocumentQualityInput } from "./document-quality-input.mjs";

function fixture(documentationPresent = true) {
  const declaration = {
    id: "declaration-1",
    kind: "model",
    qualifiedName: "Widget",
    documentationPresent,
    hunkIds: ["hunk-1"],
    source: { revision: "current", startLine: 1, endLine: 1 },
  };
  const source = {
    id: "source-1",
    path: "main.tsp",
    hunks: [{ id: "hunk-1" }],
    declarations: [declaration],
    documentEvidence: {
      schemaVersion: 4,
      status: "ready",
      blockers: [],
      declarations: [{
        declarationId: declaration.id,
        qualifiedName: declaration.qualifiedName,
        kind: declaration.kind,
        documentationPresent,
        source: declaration.source,
      }],
    },
  };
  const semantic = {
    status: "ready",
    reviewUnits: [{
      id: "semantic-1",
      sourceChangeIds: [source.id],
      hunkIds: ["hunk-1"],
      declarationIds: [declaration.id],
    }],
  };
  return { sourceIndex: { sourceChanges: [source] }, semantic };
}

test("compiler-resolved documentation presence is retained without document text", () => {
  const result = buildDocumentQualityInput(fixture(true));
  assert.equal(result.schemaVersion, 4);
  assert.equal(result.status, "ready");
  assert.equal(result.reviewUnits[0].status, "ready");
  assert.deepEqual(result.reviewUnits[0].declarations, [{
    declarationId: "declaration-1",
    qualifiedName: "Widget",
    kind: "model",
    documentationPresent: true,
    source: { revision: "current", startLine: 1, endLine: 1 },
    sourceChangeId: "source-1",
  }]);
  assert.doesNotMatch(JSON.stringify(result), /docQuote|declaration source|description text/i);
});

test("missing compiler documentation remains an assessable declaration", () => {
  const result = buildDocumentQualityInput(fixture(false));
  assert.equal(result.reviewUnits[0].status, "ready");
  assert.equal(result.reviewUnits[0].declarations[0].documentationPresent, false);
});

test("compiler evidence blockers remain not-assessed input", () => {
  const args = fixture();
  args.sourceIndex.sourceChanges[0].documentEvidence = {
    schemaVersion: 4,
    status: "blocked",
    blockers: [{ message: "Compiler failed." }],
    declarations: [],
  };
  const result = buildDocumentQualityInput(args);
  assert.equal(result.status, "blocked");
  assert.equal(result.reviewUnits[0].status, "blocked");
  assert.match(result.reviewUnits[0].reason, /Compiler failed/);
});

test("a semantic unit without changed compiler declarations is not applicable", () => {
  const args = fixture();
  args.sourceIndex.sourceChanges[0].documentEvidence.declarations = [];
  const result = buildDocumentQualityInput(args);
  assert.equal(result.status, "ready");
  assert.equal(result.reviewUnits[0].status, "not-applicable");
});

test("legacy documentation evidence must be recollected", () => {
  const args = fixture();
  args.sourceIndex.sourceChanges[0].documentEvidence.schemaVersion = 3;
  const result = buildDocumentQualityInput(args);
  assert.equal(result.status, "blocked");
  assert.match(result.reviewUnits[0].reason, /recollected/);
});
