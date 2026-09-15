import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { stringify } from "yaml";
import { analyzeDownstreamBreaking } from "./analyze-downstream-breaking.mjs";

function packageShape(current) {
  const stringType = { kind: "string" };
  const widgetType = {
    kind: "model",
    name: "ScenarioRun",
    crossLanguageDefinitionId: "Microsoft.Chaos.ScenarioRun",
  };
  return {
    crossLanguagePackageId: "Microsoft.Chaos",
    crossLanguageVersion: "1",
    metadata: { apiVersions: ["v1"] },
    clients: [{
      kind: "client",
      name: "ScenarioRuns",
      crossLanguageDefinitionId: "Microsoft.Chaos.ScenarioRuns",
      children: [],
      methods: [{
        kind: current ? "lro" : "basic",
        name: "cancel",
        access: "public",
        crossLanguageDefinitionId: "Microsoft.Chaos.ScenarioRuns.cancel",
        parameters: [{
          kind: "method",
          name: "runId",
          optional: false,
          onClient: false,
          type: stringType,
        }],
        operation: {
          kind: "http",
          path: "/runs/{runId}/cancel",
          uriTemplate: "/runs/{runId}/cancel",
          verb: "post",
          parameters: [],
          responses: [{ statusCodes: 202 }],
          exceptions: [{ statusCodes: "*" }],
        },
        response: current ? { kind: "method", type: widgetType } : { kind: "method" },
        lroMetadata: current ? {
          finalStateVia: "location",
          pollingStep: { responseBody: widgetType },
          operation: {
            kind: "http",
            path: "/runs/{runId}/cancel",
            uriTemplate: "/runs/{runId}/cancel",
            verb: "post",
          },
          logicalResult: widgetType,
          pollingInfo: {
            kind: "pollingOperationStep",
            responseModel: widgetType,
            terminationStatus: { kind: "status-code" },
          },
          envelopeResult: widgetType,
        } : undefined,
      }],
    }],
    models: [{
      ...widgetType,
      access: "public",
      usage: 3,
      properties: current ? [] : [{
        kind: "property",
        name: "status",
        serializedName: "status",
        optional: false,
        discriminator: false,
        type: stringType,
      }],
    }],
    enums: [],
    unions: [],
    namespaces: [],
  };
}

function parameterOnlyLroShape(current, finalStateVia = "azure-async-operation") {
  const shape = packageShape(true);
  const method = shape.clients[0].methods[0];
  method.parameters = [
    ...method.parameters,
    ...(current ? [{
      kind: "method",
      name: "afcManagedSync",
      optional: true,
      onClient: false,
      type: { kind: "boolean" },
    }] : []),
  ];
  method.operation.uriTemplate = current
    ? "/runs/{runId}/cancel?api-version,afcManagedSync"
    : "/runs/{runId}/cancel?api-version";
  method.lroMetadata.finalStateVia = finalStateVia;
  method.lroMetadata.operation.uriTemplate = method.operation.uriTemplate;
  return shape;
}

function nestedResponseShape(current) {
  const shape = packageShape(false);
  const method = shape.clients[0].methods[0];
  method.name = "listFilesAndDirectoriesSegment";
  method.crossLanguageDefinitionId =
    "Storage.File.Directory.listFilesAndDirectoriesSegment";
  method.response = {
    kind: "method",
    type: {
      kind: "model",
      name: "ListFilesAndDirectoriesSegmentResponse",
      crossLanguageDefinitionId:
        "Storage.File.ListFilesAndDirectoriesSegmentResponse",
    },
  };
  const property = (name, type) => ({
    kind: "property",
    name,
    serializedName: name,
    optional: true,
    discriminator: false,
    type,
  });
  shape.models = [
    {
      kind: "model",
      name: "ListFilesAndDirectoriesSegmentResponse",
      crossLanguageDefinitionId:
        "Storage.File.ListFilesAndDirectoriesSegmentResponse",
      access: "public",
      usage: 2,
      properties: [
        property("segment", {
          kind: "model",
          name: "FilesAndDirectoriesListSegment",
          crossLanguageDefinitionId:
            "Storage.File.FilesAndDirectoriesListSegment",
        }),
      ],
    },
    {
      kind: "model",
      name: "FilesAndDirectoriesListSegment",
      crossLanguageDefinitionId:
        "Storage.File.FilesAndDirectoriesListSegment",
      access: "public",
      usage: 2,
      properties: current
        ? []
        : [
            property("blockDeviceItems", {
              kind: "array",
              valueType: {
                kind: "model",
                name: "BlockDeviceItem",
                crossLanguageDefinitionId: "Storage.File.BlockDeviceItem",
              },
            }),
          ],
    },
    {
      kind: "model",
      name: "BlockDeviceItem",
      crossLanguageDefinitionId: "Storage.File.BlockDeviceItem",
      access: "public",
      usage: 2,
      properties: [],
    },
  ];
  return shape;
}

function analyzeShapes(context, base, current) {
  const work = fs.mkdtempSync(path.join(process.cwd(), ".downstream-analyzer-test-"));
  context.after(() => fs.rmSync(work, { recursive: true, force: true }));
  fs.writeFileSync(path.join(work, "base.yaml"), stringify(base));
  fs.writeFileSync(path.join(work, "current.yaml"), stringify(current));
  const artifact = (file) => ({
    status: "succeeded",
    format: "tcgc-yaml",
    files: [{ path: file }],
  });
  return analyzeDownstreamBreaking({
    workRoot: work,
    manifest: {
      projects: [{
        id: "project-1",
        sourceChangeIds: ["source-supplied"],
        artifacts: {
          base: { tcgc: artifact("base.yaml") },
          current: { tcgc: artifact("current.yaml") },
        },
      }],
    },
    sourceIndex: {
      sourceChanges: [{
        id: "source-supplied",
        declarations: [{ id: "declaration-supplied", decorators: [] }],
      }],
    },
  });
}

test("detects PR 43308-style kind/response/LRO changes without inventing parameter changes", (context) => {
  const result = analyzeShapes(context, packageShape(false), packageShape(true));
  const rules = new Set(result.candidates.map((item) => item.rule));
  assert.ok(rules.has("method-kind-changed"));
  assert.ok(rules.has("method-response-changed"));
  assert.ok(rules.has("method-lro-changed"));
  assert.ok(rules.has("model-property-removed"));
  assert.ok(!rules.has("method-parameters-changed"));
  assert.ok(result.candidates.every((item) => item.sourceChangeIds[0] === "source-supplied"));
  assert.ok(result.candidates.every((item) =>
    item.crossLanguageDefinitionId.startsWith("Microsoft.Chaos."),
  ));
  assert.ok(Object.values(result.facts).some((item) =>
    item.factKind === "method" && item.kind === "lro",
  ));
});

test("retains response-header evidence without changing established candidate IDs", (context) => {
  const withoutHeaders = analyzeShapes(
    context,
    packageShape(false),
    packageShape(true),
  );
  const base = packageShape(false);
  const current = packageShape(true);
  for (const shape of [base, current]) {
    shape.clients[0].methods[0].operation.responses[0].headers = [{
      kind: "responseheader",
      name: "location",
      serializedName: "Location",
      optional: true,
      type: { kind: "string" },
    }];
    shape.clients[0].methods[0].operation.exceptions[0].headers = [];
  }
  const withHeaders = analyzeShapes(context, base, current);

  assert.deepEqual(
    withHeaders.candidates.map((item) => item.id),
    withoutHeaders.candidates.map((item) => item.id),
  );
  assert.ok(Object.values(withHeaders.facts).some((fact) =>
    fact.factKind === "method" &&
    fact.operation.responses[0].headers[0].serializedName === "Location",
  ));
});

test("does not emit an LRO finding when only a public parameter and nested URI template change", (context) => {
  const result = analyzeShapes(
    context,
    parameterOnlyLroShape(false),
    parameterOnlyLroShape(true),
  );
  const rules = result.candidates.map((item) => item.rule);

  assert.ok(rules.includes("method-parameters-changed"));
  assert.ok(!rules.includes("method-lro-changed"));
});

test("retains actual LRO behavior changes", (context) => {
  const result = analyzeShapes(
    context,
    parameterOnlyLroShape(false, "location"),
    parameterOnlyLroShape(false, "azure-async-operation"),
  );

  assert.ok(result.candidates.some((item) => item.rule === "method-lro-changed"));
  assert.ok(!result.candidates.some((item) => item.rule === "method-parameters-changed"));
});

test("links nested response type changes to unchanged public methods", (context) => {
  const result = analyzeShapes(
    context,
    nestedResponseShape(false),
    nestedResponseShape(true),
  );
  const propertyCandidate = result.candidates.find(
    (item) => item.rule === "model-property-removed",
  );

  assert.ok(propertyCandidate);
  assert.ok(
    !result.candidates.some((item) => item.rule === "method-response-changed"),
  );
  const root = result.rootCauses.find((item) =>
    item.directCandidateIds.includes(propertyCandidate.id),
  );
  assert.equal(root.kind, "type-contract-propagation");
  assert.equal(root.methodFactIds.length, 1);
  assert.ok(
    root.referenceEvidence.some(
      (edge) =>
        edge.kind === "response" && edge.location === "response-body",
    ),
  );
  assert.ok(
    root.referenceEvidence.some(
      (edge) =>
        edge.kind === "property" &&
        edge.memberName === "segment" &&
        edge.location === "response-body",
    ),
  );
  assert.ok(
    Object.values(result.facts).some(
      (fact) =>
        fact.factKind === "method" &&
        fact.crossLanguageDefinitionId ===
          "Storage.File.Directory.listFilesAndDirectoriesSegment",
    ),
  );
});
