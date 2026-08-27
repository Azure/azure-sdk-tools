#!/usr/bin/env node

import { createHash } from "node:crypto";
import { existsSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, extname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parse as parseYaml } from "yaml";

import { buildSourceIndex } from "./source-index.mjs";

const HTTP_METHODS = new Set([
  "delete",
  "get",
  "head",
  "options",
  "patch",
  "post",
  "put",
  "trace",
]);

function uniqueSorted(values) {
  return [...new Set(values)].sort();
}

function stable(value) {
  if (Array.isArray(value)) return value.map(stable);
  if (!value || typeof value !== "object") return value;
  return Object.fromEntries(
    Object.entries(value)
      .filter(([, child]) => child !== undefined)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, child]) => [key, stable(child)]),
  );
}

function stableJson(value) {
  return JSON.stringify(stable(value));
}

function pointerValue(document, pointer) {
  if (!pointer.startsWith("#/")) return undefined;
  return pointer
    .slice(2)
    .split("/")
    .map((part) => part.replaceAll("~1", "/").replaceAll("~0", "~"))
    .reduce((value, part) => value?.[part], document);
}

function dereference(document, value) {
  if (!value?.$ref) return value;
  return pointerValue(document, value.$ref) ?? value;
}

function schemaName(schema) {
  if (!schema) return "none";
  if (schema.$ref) return schema.$ref.split("/").at(-1);
  if (schema.type === "array") return `array<${schemaName(schema.items)}>`;
  if (schema.oneOf) {
    return schema.oneOf.map(schemaName).sort().join(" | ");
  }
  if (schema.allOf) {
    return schema.allOf.map(schemaName).sort().join(" & ");
  }
  if (schema.type) {
    return schema.format ? `${schema.type}(${schema.format})` : schema.type;
  }
  return "object";
}

function schemaContract(document, schema, seen = new Set()) {
  if (!schema) return null;
  const reference = schema.$ref;
  if (reference) {
    if (seen.has(reference)) return { reference, recursive: true };
    const target = pointerValue(document, reference);
    if (!target) return { reference };
    return {
      reference,
      value: schemaContract(document, target, new Set([...seen, reference])),
    };
  }
  const properties = Object.fromEntries(
    Object.entries(schema.properties ?? {})
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([name, property]) => [
        name,
        schemaContract(document, property, seen),
      ]),
  );
  return stable({
    type: schema.type,
    format: schema.format,
    nullable: schema.nullable,
    readOnly: schema.readOnly,
    enum: schema.enum ? [...schema.enum].sort() : undefined,
    required: Array.isArray(schema.required)
      ? [...schema.required].sort()
      : undefined,
    discriminator: schema.discriminator,
    properties: Object.keys(properties).length > 0 ? properties : undefined,
    items: schema.items
      ? schemaContract(document, schema.items, seen)
      : undefined,
    additionalProperties:
      typeof schema.additionalProperties === "object"
        ? schemaContract(document, schema.additionalProperties, seen)
        : schema.additionalProperties,
    allOf: schema.allOf?.map((value) => schemaContract(document, value, seen)),
    oneOf: schema.oneOf?.map((value) => schemaContract(document, value, seen)),
    anyOf: schema.anyOf?.map((value) => schemaContract(document, value, seen)),
  });
}

function mediaSchema(document, content) {
  if (!content || typeof content !== "object") return undefined;
  const mediaTypes = Object.keys(content).sort();
  if (mediaTypes.length === 0) return undefined;
  return mediaTypes.map((mediaType) => ({
    mediaType,
    schema: schemaName(content[mediaType]?.schema),
    contract: schemaContract(document, content[mediaType]?.schema),
  }));
}

function parameterContract(document, rawParameter) {
  const parameter = dereference(document, rawParameter);
  const schema = parameter.schema ?? parameter;
  return {
    in: parameter.in ?? "unknown",
    name: parameter.name ?? "unknown",
    required: Boolean(parameter.required),
    type: schemaName(schema),
    contract: schemaContract(document, schema),
    default: schema.default,
  };
}

function requestContract(document, operation) {
  if (operation.requestBody) {
    const requestBody = dereference(document, operation.requestBody);
    return {
      required: Boolean(requestBody.required),
      content: mediaSchema(document, requestBody.content) ?? [],
    };
  }
  const body = (operation.parameters ?? [])
    .map((parameter) => dereference(document, parameter))
    .find((parameter) => parameter.in === "body");
  if (body) {
    return {
      required: Boolean(body.required),
      content: [
        {
          mediaType: "application/json",
          schema: schemaName(body.schema),
          contract: schemaContract(document, body.schema),
        },
      ],
    };
  }
  return { required: false, content: [] };
}

function responseContract(document, responses) {
  return Object.entries(responses ?? {})
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([status, rawResponse]) => {
      const response = dereference(document, rawResponse);
      const headers = Object.entries(response.headers ?? {})
        .map(([name, header]) => ({
          name,
          required: Boolean(header.required),
          type: schemaName(header.schema ?? header),
        }))
        .sort((left, right) => left.name.localeCompare(right.name));
      const content =
        mediaSchema(document, response.content) ??
        (response.schema
          ? [
              {
                mediaType: "application/json",
                schema: schemaName(response.schema),
                contract: schemaContract(document, response.schema),
              },
            ]
          : []);
      return { status, headers, content };
    });
}

function apiVersion(document, filePath) {
  const declared = document.info?.version;
  if (declared) return String(declared);
  const match = filePath
    .replaceAll("\\", "/")
    .match(/(?:^|\/)(\d{4}-\d{2}-\d{2}(?:-preview)?)(?:\/|$)/);
  return match?.[1] ?? "unknown";
}

function pagingContract(operation) {
  const pageable = operation["x-ms-pageable"];
  if (!pageable) return { isPaged: false };
  return {
    isPaged: true,
    itemName: pageable.itemName ?? "value",
    nextLinkName: pageable.nextLinkName ?? "nextLink",
    continuation:
      pageable.operationName ??
      "Issue a GET request to the opaque continuation URL until it is absent.",
  };
}

function lroContract(operation) {
  if (!operation["x-ms-long-running-operation"]) {
    return { isLongRunning: false };
  }
  const options = operation["x-ms-long-running-operation-options"] ?? {};
  return {
    isLongRunning: true,
    finalStateVia: options["final-state-via"] ?? "unknown",
    finalResult: options["final-state-schema"] ?? "operation response",
  };
}

function operationContract(
  document,
  path,
  method,
  operation,
  pathItem,
  filePath,
) {
  const parameters = [
    ...(pathItem.parameters ?? []),
    ...(operation.parameters ?? []),
  ]
    .map((parameter) => parameterContract(document, parameter))
    .sort((left, right) =>
      `${left.in}:${left.name}`.localeCompare(`${right.in}:${right.name}`),
    );
  const version = apiVersion(document, filePath);
  return {
    key: `${version}:${method.toUpperCase()}:${path}`,
    operationId: operation.operationId ?? `${method}_${path}`,
    apiVersion: version,
    method: method.toUpperCase(),
    path,
    parameters,
    request: requestContract(document, operation),
    responses: responseContract(document, operation.responses),
    lro: lroContract(operation),
    paging: pagingContract(operation),
    sourceArtifact: filePath.replaceAll("\\", "/"),
  };
}

export function extractOpenApiOperations(document, filePath = "openapi.json") {
  const pathCollections = [document.paths ?? {}, document["x-ms-paths"] ?? {}];
  const hostParameters =
    document["x-ms-parameterized-host"]?.parameters ?? [];
  const parameterizedHost = hostParameters.length > 0;
  const operations = [];
  for (const paths of pathCollections) {
    for (const [path, pathItem] of Object.entries(paths)) {
      let resolvedPath = path.startsWith("/")
        ? path
        : `${document.basePath ?? "/"}${path}`;
      if (parameterizedHost && resolvedPath.startsWith("/")) {
        const hostPath = hostParameters
          .map((parameter) => `{${parameter.name}}`)
          .join("");
        resolvedPath = `/${hostPath}${resolvedPath === "/" ? "" : resolvedPath}`;
      }
      const resolvedPathItem = {
        ...pathItem,
        parameters: [
          ...hostParameters,
          ...(pathItem.parameters ?? []),
        ],
      };
      for (const [method, operation] of Object.entries(pathItem)) {
        if (!HTTP_METHODS.has(method.toLowerCase())) continue;
        operations.push(
          operationContract(
            document,
            resolvedPath,
            method,
            operation,
            resolvedPathItem,
            filePath,
          ),
        );
      }
    }
  }
  return operations.sort((left, right) => left.key.localeCompare(right.key));
}

const CONTRACT_FIELDS = [
  "method",
  "path",
  "parameters",
  "request",
  "responses",
  "lro",
  "paging",
];

function changedAspects(before, after) {
  return CONTRACT_FIELDS.filter(
    (field) => stableJson(before[field]) !== stableJson(after[field]),
  ).map((field) => ({
    field,
    before: before[field],
    after: after[field],
  }));
}

function removedParameters(before, after) {
  const afterKeys = new Set(
    after.parameters.map((parameter) => `${parameter.in}:${parameter.name}`),
  );
  return before.parameters.filter(
    (parameter) => !afterKeys.has(`${parameter.in}:${parameter.name}`),
  );
}

function newlyRequiredParameters(before, after) {
  const beforeByKey = new Map(
    before.parameters.map((parameter) => [
      `${parameter.in}:${parameter.name}`,
      parameter,
    ]),
  );
  return after.parameters.filter((parameter) => {
    const previous = beforeByKey.get(`${parameter.in}:${parameter.name}`);
    return parameter.required && (!previous || !previous.required);
  });
}

function changedParameterContracts(before, after) {
  const beforeByKey = new Map(
    before.parameters.map((parameter) => [
      `${parameter.in}:${parameter.name}`,
      parameter,
    ]),
  );
  return after.parameters
    .map((parameter) => ({
      before: beforeByKey.get(`${parameter.in}:${parameter.name}`),
      after: parameter,
    }))
    .filter(
      ({ before: previous, after: current }) =>
        previous &&
        stableJson({
          type: previous.type,
          contract: previous.contract,
          default: previous.default,
        }) !==
          stableJson({
            type: current.type,
            contract: current.contract,
            default: current.default,
          }),
    );
}

function removedResponseStatuses(before, after) {
  const afterStatuses = new Set(
    after.responses.map((response) => response.status),
  );
  return before.responses
    .map((response) => response.status)
    .filter((status) => !afterStatuses.has(status));
}

function restCandidates(change) {
  if (change.kind === "removed") {
    return [
      {
        rule: "operation-removed",
        severity: "high",
        summary: `${change.before.operationId} was removed from API version ${change.before.apiVersion}.`,
        evidence: { operation: change.before.key },
        reviewRequired: true,
      },
    ];
  }
  if (change.kind !== "modified") return [];

  const candidates = [];
  if (
    change.aspects.some((aspect) => ["method", "path"].includes(aspect.field))
  ) {
    candidates.push({
      rule: "route-changed",
      severity: "high",
      summary: `${change.after.operationId} changed its HTTP method or path.`,
      evidence: { operation: change.after.key },
      reviewRequired: true,
    });
  }
  const removed = removedParameters(change.before, change.after);
  if (removed.length > 0) {
    candidates.push({
      rule: "parameter-removed",
      severity: "high",
      summary: `${change.after.operationId} removed ${removed.length} parameter(s).`,
      evidence: { operation: change.after.key, parameters: removed },
      reviewRequired: true,
    });
  }
  const required = newlyRequiredParameters(change.before, change.after);
  if (required.length > 0) {
    candidates.push({
      rule: "parameter-became-required",
      severity: "high",
      summary: `${change.after.operationId} made ${required.length} parameter(s) required.`,
      evidence: { operation: change.after.key, parameters: required },
      reviewRequired: true,
    });
  }
  const changedParameters = changedParameterContracts(
    change.before,
    change.after,
  );
  if (changedParameters.length > 0) {
    candidates.push({
      rule: "parameter-contract-changed",
      severity: "high",
      summary: `${change.after.operationId} changed ${changedParameters.length} parameter contract(s).`,
      evidence: {
        operation: change.after.key,
        parameters: changedParameters,
      },
      reviewRequired: true,
    });
  }
  if (change.aspects.some((aspect) => aspect.field === "request")) {
    candidates.push({
      rule: "request-contract-changed",
      severity: "high",
      summary: `${change.after.operationId} changed its request contract.`,
      evidence: {
        operation: change.after.key,
        before: change.before.request,
        after: change.after.request,
      },
      reviewRequired: true,
    });
  }
  const removedStatuses = removedResponseStatuses(change.before, change.after);
  if (removedStatuses.length > 0) {
    candidates.push({
      rule: "response-status-removed",
      severity: "high",
      summary: `${change.after.operationId} removed response status ${removedStatuses.join(", ")}.`,
      evidence: {
        operation: change.after.key,
        statuses: removedStatuses,
      },
      reviewRequired: true,
    });
  } else if (change.aspects.some((aspect) => aspect.field === "responses")) {
    candidates.push({
      rule: "response-contract-changed",
      severity: "high",
      summary: `${change.after.operationId} changed an existing response contract.`,
      evidence: {
        operation: change.after.key,
        before: change.before.responses,
        after: change.after.responses,
      },
      reviewRequired: true,
    });
  }
  return candidates;
}

export function compareOperationSets(baselineOperations, headOperations) {
  const baseline = new Map(
    baselineOperations.map((operation) => [operation.key, operation]),
  );
  const head = new Map(
    headOperations.map((operation) => [operation.key, operation]),
  );
  const keys = uniqueSorted([...baseline.keys(), ...head.keys()]);
  const changes = [];
  for (const key of keys) {
    const before = baseline.get(key);
    const after = head.get(key);
    if (!before) {
      changes.push({ kind: "added", key, before: null, after, aspects: [] });
      continue;
    }
    if (!after) {
      changes.push({ kind: "removed", key, before, after: null, aspects: [] });
      continue;
    }
    const aspects = changedAspects(before, after);
    if (aspects.length > 0) {
      changes.push({ kind: "modified", key, before, after, aspects });
    }
  }
  return {
    changes,
    restBreakingCandidates: changes.flatMap(restCandidates),
  };
}

function listJsonFiles(root) {
  if (!existsSync(root)) return [];
  return readdirSync(root, { withFileTypes: true }).flatMap((entry) => {
    const path = join(root, entry.name);
    if (entry.isDirectory()) return listJsonFiles(path);
    return extname(entry.name).toLowerCase() === ".json" ||
      entry.name.toLowerCase() === "json"
      ? [path]
      : [];
  });
}

function listStructuredFiles(root) {
  if (!existsSync(root)) return [];
  return readdirSync(root, { withFileTypes: true }).flatMap((entry) => {
    const path = join(root, entry.name);
    if (entry.isDirectory()) return listStructuredFiles(path);
    return [".json", ".yaml", ".yml"].includes(
      extname(entry.name).toLowerCase(),
    ) || entry.name.toLowerCase() === "json"
      ? [path]
      : [];
  });
}

function parseStructuredFile(path) {
  const content = readFileSync(path, "utf8");
  return [".yaml", ".yml"].includes(extname(path).toLowerCase())
    ? parseYaml(content, { maxAliasCount: -1 })
    : JSON.parse(content);
}

function tcgcVersions(values) {
  return (values ?? []).map((value) =>
    typeof value === "object" ? (value.value ?? value.name ?? value.kind) : value,
  );
}

function tcgcTypeReference(value) {
  if (value === undefined || value === null || typeof value !== "object") {
    return value;
  }
  return stable({
    kind: value.kind,
    name: value.name,
    namespace: value.namespace,
    crossLanguageDefinitionId: value.crossLanguageDefinitionId,
    value:
      typeof value.value === "object"
        ? (value.value.value ?? value.value.name ?? value.value.kind)
        : value.value,
    encode: value.encode,
    format: value.format,
    nullable: value.nullable,
    elementType: tcgcTypeReference(value.elementType),
    valueType: tcgcTypeReference(value.valueType),
    keyType: tcgcTypeReference(value.keyType),
  });
}

function projectTcgcParameter(parameter) {
  return stable({
    kind: parameter.kind,
    name: parameter.name,
    serializedName: parameter.serializedName,
    crossLanguageDefinitionId: parameter.crossLanguageDefinitionId,
    type: tcgcTypeReference(parameter.type),
    optional: parameter.optional,
    onClient: parameter.onClient,
    isApiVersionParam: parameter.isApiVersionParam,
    flatten: parameter.flatten,
    access: parameter.access,
    visibility: parameter.visibility,
    apiVersions: tcgcVersions(parameter.apiVersions),
  });
}

function projectTcgcLro(metadata) {
  if (!metadata) return undefined;
  return stable({
    finalStateVia: metadata.finalStateVia,
    finalStepKind: metadata.finalStep?.kind,
    pollingStepKind: metadata.pollingStep?.kind,
    statusMonitorStepKind: metadata.statusMonitorStep?.kind,
    pollingInfoKind: metadata.pollingInfo?.kind,
    pollingResponse: tcgcTypeReference(metadata.pollingInfo?.responseModel),
    logicalResult: tcgcTypeReference(metadata.logicalResult),
    envelopeResult: tcgcTypeReference(metadata.envelopeResult),
    finalEnvelopeResult: tcgcTypeReference(metadata.finalEnvelopeResult),
  });
}

function projectTcgcMethod(method) {
  return stable({
    kind: method.kind,
    name: method.name,
    crossLanguageDefinitionId: method.crossLanguageDefinitionId,
    access: method.access,
    parameters: (method.parameters ?? []).map(projectTcgcParameter),
    response: method.response
      ? {
          kind: method.response.kind,
          optional: method.response.optional,
          type: tcgcTypeReference(method.response.type),
        }
      : undefined,
    apiVersions: tcgcVersions(method.apiVersions),
    generateConvenient: method.generateConvenient,
    generateProtocol: method.generateProtocol,
    isOverride: method.isOverride,
    lroMetadata: projectTcgcLro(method.lroMetadata),
  });
}

function projectTcgcClient(client) {
  return stable({
    kind: client.kind,
    name: client.name,
    namespace: client.namespace,
    crossLanguageDefinitionId: client.crossLanguageDefinitionId,
    apiVersions: tcgcVersions(client.apiVersions),
    clientInitialization: (client.clientInitialization?.parameters ?? []).map(
      projectTcgcParameter,
    ),
    methods: (client.methods ?? []).map(projectTcgcMethod),
    children: (client.children ?? []).map(projectTcgcClient),
  });
}

function projectTcgcModel(model) {
  return stable({
    kind: model.kind,
    name: model.name,
    namespace: model.namespace,
    crossLanguageDefinitionId: model.crossLanguageDefinitionId,
    access: model.access,
    usage: model.usage,
    apiVersions: tcgcVersions(model.apiVersions),
    baseModel: tcgcTypeReference(model.baseModel),
    additionalProperties: tcgcTypeReference(model.additionalProperties),
    properties: (model.properties ?? []).map(projectTcgcParameter),
  });
}

function projectTcgcEnum(enumeration) {
  return stable({
    kind: enumeration.kind,
    name: enumeration.name,
    namespace: enumeration.namespace,
    crossLanguageDefinitionId: enumeration.crossLanguageDefinitionId,
    access: enumeration.access,
    usage: enumeration.usage,
    isFixed: enumeration.isFixed,
    isFlags: enumeration.isFlags,
    apiVersions: tcgcVersions(enumeration.apiVersions),
    valueType: tcgcTypeReference(enumeration.valueType),
    values: (enumeration.values ?? []).map((value) =>
      stable({
        name: value.name,
        value: value.value,
        apiVersions: tcgcVersions(value.apiVersions),
      }),
    ),
  });
}

function projectTcgcDocument(document) {
  return stable({
    crossLanguagePackageId: document.crossLanguagePackageId,
    crossLanguageVersion: document.crossLanguageVersion,
    clients: (document.clients ?? []).map(projectTcgcClient),
    models: (document.models ?? []).map(projectTcgcModel),
    enums: (document.enums ?? []).map(projectTcgcEnum),
    unions: (document.unions ?? []).map((union) =>
      stable({
        kind: union.kind,
        name: union.name,
        namespace: union.namespace,
        crossLanguageDefinitionId: union.crossLanguageDefinitionId,
        access: union.access,
        usage: union.usage,
        type: tcgcTypeReference(union.type),
      }),
    ),
  });
}

function operationsFromDirectory(root, artifactRoot) {
  const operations = listJsonFiles(root).flatMap((path) => {
    const document = JSON.parse(readFileSync(path, "utf8"));
    if (!document.paths && !document["x-ms-paths"]) return [];
    return extractOpenApiOperations(document, relative(artifactRoot, path));
  });
  const byKey = new Map();
  for (const operation of operations) {
    const existing = byKey.get(operation.key);
    if (
      existing &&
      stableJson({ ...existing, sourceArtifact: undefined }) !==
        stableJson({ ...operation, sourceArtifact: undefined })
    ) {
      throw new Error(
        `Conflicting AutoRest operation ${operation.key} in ${existing.sourceArtifact} and ${operation.sourceArtifact}.`,
      );
    }
    if (!existing) byKey.set(operation.key, operation);
  }
  return [...byKey.values()].sort((left, right) =>
    left.key.localeCompare(right.key),
  );
}

const DOWNSTREAM_PATH_RULES = [
  ["naming", /(?:^|[.[\]/])name(?:$|[.[\]/])/i],
  ["parameter-surface", /parameter/i],
  ["flattening", /flatten/i],
  ["access-or-usage", /(?:access|usage|visibility|reachable)/i],
  ["lro-metadata", /(?:long.?running|\blro\b|polling|final.?state)/i],
  ["client-location", /(?:client|operation.?group|location)/i],
  ["type-shape", /(?:type|base|extends|discriminator|hierarchy)/i],
];

function flattenJson(
  value,
  path = "$",
  entries = new Map(),
  seen = new WeakMap(),
) {
  if (Array.isArray(value)) {
    if (seen.has(value)) {
      entries.set(path, `$ref:${seen.get(value)}`);
      return entries;
    }
    seen.set(value, path);
    if (value.length === 0) entries.set(path, []);
    let identities;
    for (const key of [
      "crossLanguageDefinitionId",
      "operationId",
      "name",
      "serializedName",
    ]) {
      const candidates = value.map((child) =>
        child &&
        typeof child === "object" &&
        typeof child[key] === "string" &&
        child[key]
          ? `${key}=${child[key]}`
          : undefined,
      );
      if (
        candidates.every(Boolean) &&
        new Set(candidates).size === candidates.length
      ) {
        identities = candidates;
        break;
      }
    }
    value.forEach((child, index) =>
      flattenJson(
        child,
        `${path}[${identities ? identities[index] : index}]`,
        entries,
        seen,
      ),
    );
    return entries;
  }
  if (!value || typeof value !== "object") {
    entries.set(path, value);
    return entries;
  }
  if (seen.has(value)) {
    entries.set(path, `$ref:${seen.get(value)}`);
    return entries;
  }
  seen.set(value, path);
  const children = Object.entries(value)
    .filter(
      ([key]) =>
        !/^(?:description|doc|summary|crossLanguageDefinitionId)$/i.test(key),
    )
    .sort(([left], [right]) => left.localeCompare(right));
  if (children.length === 0) entries.set(path, {});
  for (const [key, child] of children) {
    flattenJson(child, `${path}.${key}`, entries, seen);
  }
  return entries;
}

function structuredDocuments(root, artifactRoot) {
  return Object.fromEntries(
    listStructuredFiles(root)
      .sort()
      .map((path) => [
        relative(artifactRoot, path).replaceAll("\\", "/"),
        projectTcgcDocument(parseStructuredFile(path)),
      ]),
  );
}

export function compareJsonArtifacts(baselineDocuments, headDocuments) {
  const baseline = flattenJson(baselineDocuments);
  const head = flattenJson(headDocuments);
  const paths = uniqueSorted([...baseline.keys(), ...head.keys()]);
  const changes = paths
    .filter(
      (path) => stableJson(baseline.get(path)) !== stableJson(head.get(path)),
    )
    .map((path) => ({
      path,
      kind: !baseline.has(path)
        ? "added"
        : !head.has(path)
          ? "removed"
          : "modified",
      before: baseline.get(path),
      after: head.get(path),
    }));
  const classifiedChanges = changes.map((change) => {
    const rule =
      DOWNSTREAM_PATH_RULES.find(
        ([name, pattern]) =>
          pattern.test(change.path) ||
          (name === "lro-metadata" &&
            pattern.test(
              `${stableJson(change.before)} ${stableJson(change.after)}`,
            )),
      )?.[0] ?? "unclassified-client-surface";
    const declaration =
      change.path.match(
        /^(.*\.(?:children|clients|clientInitialization|methods|parameters|models|properties|enums|values|unions)\[[^\]]+\])/,
      )?.[1] ?? change.path;
    return { rule, declaration, change };
  });
  const candidateGroups = new Map();
  for (const item of classifiedChanges) {
    const group = candidateGroups.get(item.declaration) ?? {
      declaration: item.declaration,
      changes: [],
      rules: new Set(),
    };
    group.changes.push(item.change);
    group.rules.add(item.rule);
    candidateGroups.set(item.declaration, group);
  }
  const rulePriority = [
    "lro-metadata",
    "naming",
    "parameter-surface",
    "flattening",
    "access-or-usage",
    "client-location",
    "type-shape",
    "unclassified-client-surface",
  ];
  const candidates = [...candidateGroups.values()]
    .filter((group) =>
      group.changes.some(
        (change) =>
          !change.path.endsWith(".crossLanguageVersion") &&
          !/\.apiVersions(?:\[|$)/.test(change.path) &&
          ["modified", "removed"].includes(change.kind),
      ),
    )
    .map((group) => ({
      id: `tcgc-${createHash("sha256")
        .update(group.declaration)
        .digest("hex")
        .slice(0, 12)}`,
      rule:
        rulePriority.find((rule) => group.rules.has(rule)) ??
        "unclassified-client-surface",
      summary: `TCGC changed ${group.declaration}.`,
      evidence: group.changes,
      reviewRequired: true,
    }));
  return { changes, candidates };
}

function projectSourceReferences(evidence, projectPath) {
  if (projectPath === ".") return evidence.sourceReferences ?? [];
  return (evidence.sourceReferences ?? []).filter(
    (reference) =>
      reference.path === projectPath ||
      reference.path.startsWith(`${projectPath.replaceAll("\\", "/")}/`),
  );
}

export function analyzeArtifacts(evidence, evidenceDirectory) {
  const startedAt = process.hrtime.bigint();
  const projects = (evidence.projects ?? []).map((project) => {
    const projectId =
      project.path === "."
        ? "repository-root"
        : project.path.replace(/[\\/]/g, "__");
    const artifactRoot = join(evidenceDirectory, "artifacts", projectId);
    const baseline = operationsFromDirectory(
      join(artifactRoot, "base", "autorest"),
      evidenceDirectory,
    );
    const head = operationsFromDirectory(
      join(artifactRoot, "head", "autorest"),
      evidenceDirectory,
    );
    const baselineTcgcRoot = join(artifactRoot, "base", "tcgc");
    const headTcgcRoot = join(artifactRoot, "head", "tcgc");
    const downstream = compareJsonArtifacts(
      structuredDocuments(baselineTcgcRoot, baselineTcgcRoot),
      structuredDocuments(headTcgcRoot, headTcgcRoot),
    );
    return {
      path: project.path,
      sourceReferences: projectSourceReferences(evidence, project.path),
      rest: {
        baseline,
        head,
        ...compareOperationSets(baseline, head),
      },
      downstream: { status: "requires-review", ...downstream },
    };
  });
  return {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    baseline: evidence.baseline,
    head: evidence.head,
    durationMs: Math.round(
      Number(process.hrtime.bigint() - startedAt) / 1_000_000,
    ),
    sourceIndex: buildSourceIndex(
      evidence.typeSpecDiffs ?? [],
      evidence.sourceReferences ?? [],
    ),
    projects,
    reviewRequired: {
      semanticIntent: true,
      restBreakingCandidates: projects.some(
        (project) => project.rest.restBreakingCandidates.length > 0,
      ),
      downstream: true,
      compliance: true,
    },
  };
}

function main() {
  const [evidencePathValue, analysisPathValue] = process.argv.slice(2);
  if (!evidencePathValue || !analysisPathValue) {
    throw new Error(
      "Usage: analyze-artifacts.mjs <evidence.json> <analysis.json>",
    );
  }
  const evidencePath = resolve(evidencePathValue);
  const evidence = JSON.parse(readFileSync(evidencePath, "utf8"));
  const analysis = analyzeArtifacts(evidence, dirname(evidencePath));
  writeFileSync(resolve(analysisPathValue), `${JSON.stringify(analysis)}\n`);
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  try {
    main();
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
