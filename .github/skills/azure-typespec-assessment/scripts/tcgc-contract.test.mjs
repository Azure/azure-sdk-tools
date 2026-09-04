import assert from "node:assert/strict";
import test from "node:test";
import { normalizeTcgcPackage, parseTcgcYaml } from "./tcgc-contract.mjs";

test("normalizes the TCGC graph with aliases, cycles, all method kinds, and separate bodyParam", () => {
  const root = parseTcgcYaml(`
crossLanguagePackageId: Contoso
crossLanguageVersion: "1.0"
metadata:
  apiVersions:
    Contoso: v2
  apiVersion: v1
clients:
  - &client
    kind: client
    name: Widgets
    crossLanguageDefinitionId: Contoso.Widgets
    methods:
      - &method
        kind: basic
        name: get
        access: public
        crossLanguageDefinitionId: Contoso.Widgets.get
        parameters:
          - kind: method
            name: id
            optional: false
            onClient: false
            type: { kind: string }
        operation:
          kind: http
          path: /widgets/{id}
          uriTemplate: /widgets/{id}
          verb: post
          parameters:
            - kind: path
              name: id
              type: { kind: string }
              methodParameterSegments:
                - [{ kind: method, name: id }]
              correspondingMethodParams:
                - { kind: method, name: oldId }
          bodyParam:
            kind: body
            name: body
            type:
              kind: dictionary
              keyType: { kind: string }
              valueType:
                kind: nullable
                type:
                  kind: array
                  valueType:
                    kind: tuple
                    valueTypes: [{ kind: string }, { kind: int32 }]
          responses:
            - statusCodes: { start: 200, end: 299 }
          exceptions:
            - statusCodes: "*"
      - <<: *method
        kind: paging
        name: list
        crossLanguageDefinitionId: Contoso.Widgets.list
        pagingMetadata:
          pageItemsSegments: [items]
          nextLinkVerb: get
      - <<: *method
        kind: lro
        name: begin
        crossLanguageDefinitionId: Contoso.Widgets.begin
        response:
          kind: method
          type: { kind: model, name: Widget, crossLanguageDefinitionId: Contoso.Widget }
        lroMetadata:
          finalStateVia: location
          pollingStep: { responseBody: { kind: string } }
          operation:
            kind: http
            path: /widgets/{id}
            uriTemplate: /widgets/{id}
            verb: post
          logicalResult: { kind: model, name: Widget, crossLanguageDefinitionId: Contoso.Widget }
          pollingInfo:
            kind: pollingOperationStep
            responseModel: { kind: model, name: Widget, crossLanguageDefinitionId: Contoso.Widget }
            terminationStatus: { kind: status-code }
          envelopeResult: { kind: model, name: Widget, crossLanguageDefinitionId: Contoso.Widget }
      - <<: *method
        kind: lropaging
        name: beginList
        crossLanguageDefinitionId: Contoso.Widgets.beginList
        pagingMetadata: { nextLinkSegments: [nextLink] }
        lroMetadata:
          finalStateVia: azure-async-operation
          pollingStep: { responseBody: { kind: string } }
          operation:
            kind: http
            path: /widgets/{id}
            uriTemplate: /widgets/{id}
            verb: post
          logicalResult: { kind: model, name: Widget, crossLanguageDefinitionId: Contoso.Widget }
          pollingInfo:
            kind: pollingOperationStep
            responseModel: { kind: model, name: Widget, crossLanguageDefinitionId: Contoso.Widget }
            terminationStatus: { kind: status-code }
          envelopeResult: { kind: model, name: Widget, crossLanguageDefinitionId: Contoso.Widget }
    children: []
models:
  - &widget
    kind: model
    name: Widget
    access: public
    usage: 3
    crossLanguageDefinitionId: Contoso.Widget
    discriminatorProperty: &kindProp
      kind: property
      name: kind
      serializedName: kind
      optional: false
      discriminator: true
      type: { kind: string }
    discriminatorValue: widget
    discriminatedSubtypes: {}
    properties:
      - *kindProp
      - kind: property
        name: child
        optional: true
        discriminator: false
        type: *widget
enums:
  - kind: enum
    name: Mode
    access: public
    usage: 1
    crossLanguageDefinitionId: Contoso.Mode
    valueType: { kind: string }
    isFixed: false
    isUnionAsEnum: true
    values: [{ name: Fast, value: fast }]
unions:
  - kind: union
    name: Choice
    access: public
    usage: 1
    crossLanguageDefinitionId: Contoso.Choice
    variantTypes:
      - { kind: string }
      - kind: string
        external: { kind: externalTypeInfo, identity: ext.Ext, package: ext }
    discriminatedOptions:
      envelope: object
      discriminatorPropertyName: kind
      envelopePropertyName: value
namespaces:
  - name: Contoso
    clients: [*client]
`);
  const contract = normalizeTcgcPackage(root);
  assert.deepEqual(contract.methods.map((item) => item.kind), ["lro", "lropaging", "basic", "paging"]);
  const get = contract.methods.find((item) => item.name === "get");
  assert.equal(get.operation.parameters[0].kind, "path");
  assert.equal(get.operation.bodyParam.kind, "body");
  assert.equal(get.operation.bodyParam.type.kind, "dictionary");
  assert.deepEqual(get.operation.responses[0].statusCodes, { start: 200, end: 299 });
  assert.equal(get.operation.exceptions[0].statusCodes, "*");
  assert.equal(contract.clients.length, 1);
  assert.equal(contract.models[0].discriminatorProperty, "kind");
  assert.equal(contract.models[0].properties[1].type.id, "Contoso.Widget");
  assert.equal(contract.enums[0].isUnionAsEnum, true);
  assert.equal(contract.unions[0].variantTypes[1].external.identity, "ext.Ext");
  assert.equal(contract.unions[0].discriminatedOptions.envelope, "object");
  assert.deepEqual(contract.package.apiVersions, [{ service: "Contoso", version: "v2" }]);
  assert.deepEqual(contract.conflicts.map((item) => item.code), [
    "api-version-conflict",
    "method-parameter-segments-conflict",
  ]);
});

test("rejects invalid normal response status arrays", () => {
  assert.throws(
    () =>
      normalizeTcgcPackage({
        crossLanguagePackageId: "Contoso",
        crossLanguageVersion: "1",
        metadata: { apiVersions: ["v1"] },
        clients: [{
          kind: "client",
          name: "Client",
          methods: [{
            kind: "basic",
            name: "get",
            parameters: [],
            operation: {
              kind: "http",
              parameters: [],
              responses: [{ statusCodes: [200, 201] }],
              exceptions: [],
            },
          }],
          children: [],
        }],
        models: [],
        enums: [],
        unions: [],
        namespaces: [],
      }),
    /one exact status/,
  );
});
