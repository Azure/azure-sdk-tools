import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  analyzeArtifacts,
  compareJsonArtifacts,
  compareOperationSets,
  extractOpenApiOperations,
} from "./analyze-artifacts.mjs";

function document(operation) {
  return {
    swagger: "2.0",
    info: { version: "2026-01-01" },
    paths: {
      "/widgets/{widgetName}": {
        parameters: [
          {
            in: "path",
            name: "widgetName",
            required: true,
            type: "string",
          },
        ],
        get: {
          operationId: "Widgets_Get",
          parameters: [
            {
              in: "query",
              name: "api-version",
              required: true,
              type: "string",
            },
          ],
          responses: {
            200: {
              schema: { $ref: "#/definitions/Widget" },
            },
            default: {
              schema: { $ref: "#/definitions/ErrorResponse" },
            },
          },
          ...operation,
        },
      },
    },
    definitions: {
      Widget: { type: "object" },
      ErrorResponse: { type: "object" },
    },
  };
}

test("extractOpenApiOperations creates a stable REST contract", () => {
  const [operation] = extractOpenApiOperations(
    document({
      "x-ms-pageable": { itemName: "value", nextLinkName: "nextLink" },
    }),
    "stable/2026-01-01/widgets.json",
  );
  assert.equal(operation.key, "2026-01-01:GET:/widgets/{widgetName}");
  assert.equal(operation.method, "GET");
  assert.equal(operation.path, "/widgets/{widgetName}");
  assert.deepEqual(
    operation.parameters.map((parameter) => parameter.name),
    ["widgetName", "api-version"],
  );
  assert.deepEqual(operation.request, { required: false, content: [] });
  assert.equal(operation.responses[0].content[0].schema, "Widget");
  assert.deepEqual(operation.lro, { isLongRunning: false });
  assert.equal(operation.paging.isPaged, true);
});

test("extractOpenApiOperations resolves relative x-ms-paths against basePath", () => {
  const input = {
    swagger: "2.0",
    info: { version: "2026-12-06" },
    basePath: "/{filesystem}/{directoryPath}",
    "x-ms-paths": {
      "?restype=directory&comp=list": {
        get: {
          operationId: "Directory_ListFilesAndDirectoriesSegment",
          responses: { 200: { description: "Success" } },
        },
      },
    },
  };

  const [operation] = extractOpenApiOperations(input);
  assert.equal(
    operation.path,
    "/{filesystem}/{directoryPath}?restype=directory&comp=list",
  );
  assert.equal(
    operation.key,
    "2026-12-06:GET:/{filesystem}/{directoryPath}?restype=directory&comp=list",
  );
});

test("extractOpenApiOperations includes parameterized host targets", () => {
  const input = {
    swagger: "2.0",
    info: { version: "2026-12-06" },
    "x-ms-parameterized-host": {
      hostTemplate: "{url}",
      useSchemePrefix: false,
      parameters: [
        {
          name: "url",
          in: "path",
          required: true,
          type: "string",
          format: "uri",
        },
      ],
    },
    paths: {
      "/": {
        put: {
          operationId: "File_Create",
          responses: { 201: { description: "Created" } },
        },
      },
    },
  };

  const [operation] = extractOpenApiOperations(input);
  assert.equal(operation.path, "/{url}");
  assert.deepEqual(
    operation.parameters.map(({ name, in: location }) => [name, location]),
    [["url", "path"]],
  );
});

test("compareOperationSets emits field changes and breaking candidates", () => {
  const [before] = extractOpenApiOperations(document({}));
  const [after] = extractOpenApiOperations(
    document({
      parameters: [
        {
          in: "query",
          name: "api-version",
          required: true,
          type: "string",
        },
        {
          in: "query",
          name: "filter",
          required: true,
          type: "string",
        },
      ],
    }),
  );
  const comparison = compareOperationSets([before], [after]);
  assert.equal(comparison.changes.length, 1);
  assert.equal(comparison.changes[0].kind, "modified");
  assert.deepEqual(
    comparison.changes[0].aspects.map((aspect) => aspect.field),
    ["parameters"],
  );
  assert.equal(
    comparison.restBreakingCandidates[0].rule,
    "parameter-became-required",
  );
  assert.equal(comparison.restBreakingCandidates[0].reviewRequired, true);
});

test("removed operations produce reviewable breaking candidates", () => {
  const [before] = extractOpenApiOperations(document({}));
  const comparison = compareOperationSets([before], []);
  assert.equal(comparison.changes[0].kind, "removed");
  assert.equal(comparison.restBreakingCandidates[0].rule, "operation-removed");
});

test("removed response statuses produce precise REST candidates", () => {
  const [before] = extractOpenApiOperations(document({}));
  const changed = document({});
  delete changed.paths["/widgets/{widgetName}"].get.responses["200"];
  const [after] = extractOpenApiOperations(changed);
  const comparison = compareOperationSets([before], [after]);
  const candidate = comparison.restBreakingCandidates.find(
    (item) => item.rule === "response-status-removed",
  );
  assert.deepEqual(candidate.evidence.statuses, ["200"]);
});

test("TCGC JSON changes become classified downstream candidates", () => {
  const comparison = compareJsonArtifacts(
    {
      "tcgc.json": {
        clients: [{ name: "WidgetsClient", methods: [{ name: "get" }] }],
      },
    },
    {
      "tcgc.json": {
        clients: [{ name: "WidgetsClient", methods: [{ name: "getWidget" }] }],
      },
    },
  );
  assert.equal(comparison.changes.length, 2);
  assert.equal(comparison.candidates[0].rule, "naming");
  assert.match(comparison.candidates[0].id, /^tcgc-[0-9a-f]{12}$/);
  assert.equal(comparison.candidates[0].reviewRequired, true);
});

test("TCGC LRO candidates include only operations whose metadata changed", () => {
  const unchangedLro = (name) => ({
    name,
    kind: "lro",
    lroMetadata: { finalStateVia: "location" },
  });
  const newlyRecognizedLro = ["execute", "legacyCancel", "runCancel"];
  const unchanged = [
    "validate",
    "fixResourcePermissions",
    "refreshRecommendations",
  ];
  const comparison = compareJsonArtifacts(
    {
      "tcgc.json": {
        methods: [
          ...newlyRecognizedLro.map((name) => ({ name, kind: "basic" })),
          ...unchanged.map(unchangedLro),
        ],
      },
    },
    {
      "tcgc.json": {
        methods: [
          ...newlyRecognizedLro.map(unchangedLro),
          ...unchanged.map(unchangedLro),
        ],
      },
    },
  );
  assert.ok(
    comparison.candidates.every(
      (candidate) => candidate.rule === "lro-metadata",
    ),
  );
  const evidence = JSON.stringify(comparison.candidates);
  for (const name of newlyRecognizedLro) assert.match(evidence, RegExp(name));
  for (const name of unchanged) assert.doesNotMatch(evidence, RegExp(name));
});

test("TCGC API-version propagation does not create downstream candidates", () => {
  for (const [beforeVersions, afterVersions] of [
    [["2025-01-01"], ["2026-01-01"]],
    [[], ["2026-01-01"]],
    [["2025-01-01"], []],
  ]) {
    const comparison = compareJsonArtifacts(
      {
        "tcgc.json": {
          methods: [
            { name: "get", kind: "basic", apiVersions: beforeVersions },
          ],
          crossLanguageVersion: "old-hash",
        },
      },
      {
        "tcgc.json": {
          methods: [
            { name: "get", kind: "basic", apiVersions: afterVersions },
          ],
          crossLanguageVersion: "new-hash",
        },
      },
    );
    assert.ok(comparison.changes.length > 0);
    assert.deepEqual(comparison.candidates, []);
  }
});

test("artifact directories compare matching relative TCGC files", () => {
  const root = mkdtempSync(join(tmpdir(), "assessment-analysis-"));
  const projectRoot = join(root, "artifacts", "spec");
  try {
    for (const side of ["base", "head"]) {
      mkdirSync(join(projectRoot, side, "autorest"), { recursive: true });
      mkdirSync(join(projectRoot, side, "tcgc"), { recursive: true });
      writeFileSync(
        join(projectRoot, side, "autorest", "openapi.json"),
        JSON.stringify(document({})),
      );
      writeFileSync(
        join(projectRoot, side, "tcgc", "tcgc.json"),
        JSON.stringify({
          clients: [
            {
              name: side === "base" ? "WidgetsClient" : "WidgetClient",
              crossLanguageDefinitionId: "Test.WidgetsClient",
            },
          ],
        }),
      );
    }
    const analysis = analyzeArtifacts(
      {
        baseline: { commit: "base" },
        head: { commit: "head" },
        projects: [{ path: "spec" }],
        sourceReferences: [],
      },
      root,
    );
    assert.equal(analysis.projects[0].rest.changes.length, 0);
    assert.equal(analysis.projects[0].downstream.changes.length, 1);
    assert.equal(analysis.projects[0].downstream.candidates[0].rule, "naming");
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("artifact directories compare YAML TCGC output", () => {
  const root = mkdtempSync(join(tmpdir(), "assessment-analysis-yaml-"));
  const projectRoot = join(root, "artifacts", "spec");
  try {
    for (const side of ["base", "head"]) {
      mkdirSync(join(projectRoot, side, "autorest"), { recursive: true });
      mkdirSync(join(projectRoot, side, "tcgc"), { recursive: true });
      writeFileSync(
        join(projectRoot, side, "tcgc", "tcgc-output.yaml"),
        [
          "clients:",
          "  - &client",
          `    name: ${side === "base" ? "WidgetsClient" : "WidgetClient"}`,
          "    crossLanguageDefinitionId: Test.WidgetsClient",
          "    self: *client",
          "  - *client",
          "",
        ].join("\n"),
      );
    }
    const analysis = analyzeArtifacts(
      {
        baseline: { commit: "base" },
        head: { commit: "head" },
        projects: [{ path: "spec" }],
        sourceReferences: [],
      },
      root,
    );
    assert.equal(analysis.projects[0].downstream.changes.length, 2);
    assert.ok(
      analysis.projects[0].downstream.candidates.every(
        (candidate) => candidate.rule === "naming",
      ),
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
