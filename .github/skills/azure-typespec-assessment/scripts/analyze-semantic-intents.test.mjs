import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import {
  analyzeSemanticIntents,
  dedupePublicationHunks,
} from "./analyze-semantic-intents.mjs";

function writeJson(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(value));
}

function artifact(file) {
  return {
    format: "swagger-2.0",
    status: "succeeded",
    files: [{ path: file, documentRole: "primary" }],
  };
}

test("keeps shared hunks only in their specific semantic unit", () => {
  const units = [{
    id: "semantic-publication",
    sourceChangeIds: ["source-version", "source-client"],
    hunkIds: ["hunk-version", "hunk-client"],
    declarationIds: ["declaration-version", "declaration-client"],
    operations: [],
    groupingEvidence: {
      reasons: ["cross-project:api-version-publication"],
      memberHunkIds: ["hunk-version", "hunk-client"],
    },
  }, {
    id: "semantic-client",
    sourceChangeIds: ["source-client"],
    hunkIds: ["hunk-client"],
    declarationIds: ["declaration-client"],
    operations: [],
    groupingEvidence: {
      reasons: ["sdk-compatibility"],
      memberHunkIds: ["hunk-client"],
    },
  }];
  const sources = [{
    id: "source-version",
    hunks: [{ id: "hunk-version" }],
    declarations: [{
      id: "declaration-version",
      hunkIds: ["hunk-version"],
    }],
  }, {
    id: "source-client",
    hunks: [{ id: "hunk-client" }],
    declarations: [{
      id: "declaration-client",
      hunkIds: ["hunk-client"],
    }],
  }];

  const result = dedupePublicationHunks(units, sources);

  assert.deepEqual(result[0].hunkIds, ["hunk-version"]);
  assert.deepEqual(result[0].sourceChangeIds, ["source-version"]);
  assert.deepEqual(result[0].declarationIds, ["declaration-version"]);
  assert.deepEqual(result[0].groupingEvidence.memberHunkIds, ["hunk-version"]);
  assert.deepEqual(result[1], units[1]);
});

test("creates source-first semantic units and retains unchanged REST operations", (context) => {
  const work = fs.mkdtempSync(path.join(process.cwd(), ".semantic-analyzer-test-"));
  context.after(() => fs.rmSync(work, { recursive: true, force: true }));
  const document = {
    swagger: "2.0",
    info: { title: "Widgets", version: "v1" },
    paths: {
      "/widgets/{id}/cancel": {
        post: {
          operationId: "Widgets_Cancel",
          parameters: [],
          responses: { 202: { description: "accepted" } },
        },
      },
    },
  };
  writeJson(path.join(work, "base.json"), document);
  writeJson(path.join(work, "current.json"), document);
  const result = analyzeSemanticIntents({
    workRoot: work,
    manifest: {
      projects: [{
        id: "project-kept",
        sourceChangeIds: ["source-kept"],
        artifacts: {
          base: { autorest: artifact("base.json") },
          current: { autorest: artifact("current.json") },
        },
      }],
    },
    sourceIndex: {
      sourceChanges: [{
        id: "source-kept",
        status: "modified",
        hunks: [{ id: "hunk-kept" }],
        declarations: [
          {
            id: "declaration-base",
            kind: "operation",
            qualifiedName: "Widgets.cancel",
            hunkIds: ["hunk-kept"],
            source: { revision: "base" },
          },
          {
            id: "declaration-current",
            kind: "operation",
            qualifiedName: "Widgets.cancel",
            hunkIds: ["hunk-kept"],
            source: { revision: "current" },
          },
        ],
      }],
    },
  });

  assert.equal(result.status, "ready");
  assert.equal(result.reviewUnits.length, 1);
  assert.equal(result.reviewUnits[0].action, "modify");
  assert.equal(result.reviewUnits[0].operations[0].operationId, "Widgets_Cancel");
  assert.equal(result.reviewUnits[0].operations[0].restChanged, false);
  assert.deepEqual(result.reviewUnits[0].operations[0].sourceChangeIds, ["source-kept"]);
  assert.deepEqual(result.reviewUnits[0].operations[0].hunkIds, ["hunk-kept"]);
  assert.deepEqual(result.reviewUnits[0].hunkIds, ["hunk-kept"]);
});

test("retains a semantic unit when changed TypeSpec has no REST operation", (context) => {
  const work = fs.mkdtempSync(path.join(process.cwd(), ".semantic-no-rest-test-"));
  context.after(() => fs.rmSync(work, { recursive: true, force: true }));
  const document = {
    swagger: "2.0",
    info: { title: "Widgets", version: "v1" },
    paths: {},
  };
  writeJson(path.join(work, "base.json"), document);
  writeJson(path.join(work, "current.json"), document);
  const result = analyzeSemanticIntents({
    workRoot: work,
    manifest: {
      projects: [{
        id: "project-kept",
        sourceChangeIds: ["source-kept"],
        artifacts: {
          base: { autorest: artifact("base.json") },
          current: { autorest: artifact("current.json") },
        },
      }],
    },
    sourceIndex: {
      sourceChanges: [{
        id: "source-kept",
        status: "added",
        hunks: [{ id: "hunk-kept" }],
        declarations: [{
          id: "declaration-current",
          kind: "alias",
          qualifiedName: "InternalName",
          hunkIds: ["hunk-kept"],
          source: { revision: "current" },
        }],
      }],
    },
  });

  assert.equal(result.reviewUnits.length, 1);
  assert.equal(result.reviewUnits[0].action, "add");
  assert.deepEqual(result.reviewUnits[0].operations, []);
});

test("maps a changed model to an operation through changed TypeSpec references", (context) => {
  const work = fs.mkdtempSync(path.join(process.cwd(), ".semantic-source-reference-test-"));
  context.after(() => fs.rmSync(work, { recursive: true, force: true }));
  const document = {
    swagger: "2.0",
    info: { title: "Widgets", version: "v1" },
    paths: {
      "/widgets/{id}": {
        get: {
          operationId: "Widgets_Get",
          responses: { 200: { description: "ok", schema: { type: "object" } } },
        },
      },
    },
  };
  writeJson(path.join(work, "base.json"), document);
  writeJson(path.join(work, "current.json"), document);
  const result = analyzeSemanticIntents({
    workRoot: work,
    manifest: {
      projects: [{
        id: "project-kept",
        sourceChangeIds: ["model-source", "operation-source"],
        artifacts: {
          base: { autorest: artifact("base.json") },
          current: { autorest: artifact("current.json") },
        },
      }],
    },
    sourceIndex: {
      sourceChanges: [{
        id: "model-source",
        status: "added",
        hunks: [{ id: "model-hunk", lines: ["+model PollingResponse {}"] }],
        declarations: [{
          id: "model",
          kind: "model",
          qualifiedName: "PollingResponse",
          hunkIds: ["model-hunk"],
          source: { revision: "current" },
        }],
      }, {
        id: "operation-source",
        status: "modified",
        hunks: [{
          id: "operation-hunk",
          lines: ["+  get is ArmResourceRead<Widget, Response = PollingResponse>;"],
        }],
        declarations: [{
          id: "operation",
          kind: "operation",
          qualifiedName: "Widgets.get",
          hunkIds: ["operation-hunk"],
          compilerEvidence: { referencedNames: ["PollingResponse"] },
          source: { revision: "current" },
        }],
      }],
    },
  });
  const modelUnit = result.reviewUnits.find((item) => item.sourceChangeIds.includes("model-source"));
  assert.ok(modelUnit, JSON.stringify(result));
  assert.equal(modelUnit.operations[0].operationId, "Widgets_Get");
  assert.equal(modelUnit.operations[0].matchBasis, "operation-identity");
});

test("classifies a new API surface as add despite modified registration code", (context) => {
  const work = fs.mkdtempSync(path.join(process.cwd(), ".semantic-added-feature-test-"));
  context.after(() => fs.rmSync(work, { recursive: true, force: true }));
  writeJson(path.join(work, "base.json"), {
    swagger: "2.0",
    info: { title: "Widgets", version: "v1" },
    paths: {},
  });
  writeJson(path.join(work, "current.json"), {
    swagger: "2.0",
    info: { title: "Widgets", version: "v2" },
    paths: {
      "/widgets": {
        put: {
          operationId: "Widgets_Create",
          responses: { 200: { description: "ok" } },
        },
      },
    },
  });
  const result = analyzeSemanticIntents({
    workRoot: work,
    manifest: {
      projects: [{
        id: "project-widgets",
        sourceChangeIds: ["feature-source", "main-source"],
        artifactComparison: { mode: "new-api-version" },
        artifacts: {
          base: { autorest: artifact("base.json") },
          current: { autorest: artifact("current.json") },
        },
      }],
    },
    sourceIndex: {
      sourceChanges: [{
        id: "feature-source",
        path: "specification/widgets/Widget.tsp",
        status: "added",
        hunks: [{ id: "feature-hunk", lines: ["+op create(): void;"] }],
        declarations: [{
          id: "feature-operation",
          kind: "operation",
          qualifiedName: "Widgets.create",
          hunkIds: ["feature-hunk"],
          source: { revision: "current" },
        }],
      }, {
        id: "main-source",
        path: "specification/widgets/main.tsp",
        status: "modified",
        hunks: [{ id: "main-hunk", lines: ['+import "./Widget.tsp";'] }],
        declarations: [{
          id: "main-base",
          kind: "namespace",
          qualifiedName: "Widgets",
          hunkIds: ["main-hunk"],
          source: { revision: "base" },
        }, {
          id: "main-current",
          kind: "namespace",
          qualifiedName: "Widgets",
          hunkIds: ["main-hunk"],
          source: { revision: "current" },
        }],
      }],
    },
  });

  assert.equal(result.reviewUnits.length, 1);
  assert.equal(result.reviewUnits[0].action, "add");
  assert.deepEqual(
    result.reviewUnits[0].operations.map((operation) => operation.operationId),
    ["Widgets_Create"],
  );
});

test("keeps mixed added and changed operations classified as modify", (context) => {
  const work = fs.mkdtempSync(path.join(process.cwd(), ".semantic-mixed-action-test-"));
  context.after(() => fs.rmSync(work, { recursive: true, force: true }));
  writeJson(path.join(work, "base.json"), {
    swagger: "2.0",
    info: { title: "Widgets", version: "v1" },
    paths: {
      "/widgets/{id}": {
        get: {
          operationId: "Widgets_Get",
          responses: { 200: { description: "ok" } },
        },
      },
    },
  });
  writeJson(path.join(work, "current.json"), {
    swagger: "2.0",
    info: { title: "Widgets", version: "v2" },
    paths: {
      "/widgets": {
        put: {
          operationId: "Widgets_Create",
          responses: { 200: { description: "ok" } },
        },
      },
      "/widgets/{id}": {
        get: {
          operationId: "Widgets_Get",
          responses: { 201: { description: "created" } },
        },
      },
    },
  });
  const result = analyzeSemanticIntents({
    workRoot: work,
    manifest: {
      projects: [{
        id: "project-widgets",
        sourceChangeIds: ["feature-source", "main-source"],
        artifactComparison: { mode: "new-api-version" },
        artifacts: {
          base: { autorest: artifact("base.json") },
          current: { autorest: artifact("current.json") },
        },
      }],
    },
    sourceIndex: {
      sourceChanges: [{
        id: "feature-source",
        path: "specification/widgets/Widget.tsp",
        status: "added",
        hunks: [{ id: "feature-hunk", lines: ["+op create(): void;"] }],
        declarations: [{
          id: "feature-operation",
          kind: "operation",
          qualifiedName: "Widgets.create",
          hunkIds: ["feature-hunk"],
          source: { revision: "current" },
        }],
      }, {
        id: "main-source",
        path: "specification/widgets/main.tsp",
        status: "added",
        hunks: [{ id: "main-hunk", lines: ["-op get(): Widget;", "+op get(): CreatedWidget;"] }],
        declarations: [{
          id: "get-base",
          kind: "operation",
          qualifiedName: "Widgets.get",
          hunkIds: ["main-hunk"],
          source: { revision: "base" },
        }, {
          id: "get-current",
          kind: "operation",
          qualifiedName: "Widgets.get",
          hunkIds: ["main-hunk"],
          source: { revision: "current" },
        }],
      }],
    },
  });

  assert.equal(result.reviewUnits.length, 1);
  assert.equal(result.reviewUnits[0].action, "modify");
  assert.deepEqual(
    result.reviewUnits[0].operations.map((operation) => operation.operationId),
    ["Widgets_Create", "Widgets_Get"],
  );
});
