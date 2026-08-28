import fs from "node:fs";
import path from "node:path";
import { parseArgs, isMain, readJson, runMain, writeJson } from "./cli.mjs";
import { prepareAssessment } from "./prepare-assessment.mjs";
import { analyzeSemanticIntents } from "./analyze-semantic-intents.mjs";
import { analyzeRestBreaking } from "./analyze-rest-breaking.mjs";
import { analyzeDownstreamBreaking } from "./analyze-downstream-breaking.mjs";
import { validateAssessment } from "./validate-assessment.mjs";
import { renderAssessmentHtml } from "./render-assessment-html.mjs";

const BUDGET_TIERS = [
  ["small", 128 * 1024],
  ["medium", 256 * 1024],
  ["large", 512 * 1024],
  ["maximum", 1024 * 1024],
];

function typeSummary(value, depth = 0) {
  if (value === undefined || value === null) return value;
  if (typeof value !== "object") return value;
  if (depth >= 3) {
    return value.id ?? value.crossLanguageDefinitionId ?? value.name ?? value.kind ?? "nested";
  }
  if (Array.isArray(value)) return value.map((item) => typeSummary(item, depth + 1));
  const result = {};
  for (const key of [
    "kind",
    "id",
    "identity",
    "crossLanguageDefinitionId",
    "name",
    "type",
    "format",
    "nullable",
    "optional",
    "required",
    "serializedName",
    "value",
    "isFixed",
    "isUnionAsEnum",
  ]) {
    if (value[key] !== undefined) {
      result[key] = typeof value[key] === "object"
        ? typeSummary(value[key], depth + 1)
        : value[key];
    }
  }
  if (value.items) result.items = typeSummary(value.items, depth + 1);
  if (value.valueType) result.valueType = typeSummary(value.valueType, depth + 1);
  if (value.keyType) result.keyType = typeSummary(value.keyType, depth + 1);
  if (value.variantTypes) result.variantTypes = value.variantTypes.map((item) => typeSummary(item, depth + 1));
  if (value.properties) {
    result.properties = value.properties.map((property) => ({
      name: property.name,
      serializedName: property.serializedName,
      required: property.required,
      optional: property.optional,
      type: typeSummary(property.schema ?? property.type, depth + 1),
    }));
  }
  return result;
}

function metadataSummary(value) {
  if (!value || typeof value !== "object") return value;
  const result = {};
  for (const key of [
    "isLongRunning",
    "finalStateVia",
    "nextLinkName",
    "itemName",
    "operationName",
    "nextLinkVerb",
  ]) {
    if (value[key] !== undefined) result[key] = value[key];
  }
  for (const key of [
    "logicalResult",
    "envelopeResult",
    "finalEnvelopeResult",
    "responseType",
  ]) {
    if (value[key] !== undefined) result[key] = typeSummary(value[key]);
  }
  for (const key of [
    "pageItemsSegments",
    "nextLinkSegments",
    "pageSizeParameterSegments",
    "continuationTokenParameterSegments",
    "continuationTokenResponseSegments",
    "nextLinkReInjectedParametersSegments",
  ]) {
    if (value[key]) {
      result[key] = value[key].map((item) =>
        typeof item === "object"
          ? item.crossLanguageDefinitionId ?? item.name ?? item.serializedName ?? item.kind
          : item,
      );
    }
  }
  for (const key of ["pollingStep", "statusMonitorStep", "finalStep"]) {
    if (value[key]) result[key] = { kind: value[key].kind, target: value[key].target?.kind };
  }
  return result;
}

function compactOperationFact(fact) {
  return {
    id: fact.id,
    projectId: fact.projectId,
    revision: fact.revision,
    apiVersion: fact.apiVersion,
    operationId: fact.operationId,
    method: fact.method,
    path: fact.path,
    routeSource: fact.routeSource,
    parameters: (fact.parameters ?? []).map((parameter) => ({
      name: parameter.name,
      in: parameter.in,
      required: parameter.required,
      collectionFormat: parameter.collectionFormat,
      schema: typeSummary(parameter.schema),
    })),
    request: fact.request
      ? {
          kind: fact.request.kind,
          required: fact.request.required,
          schema: typeSummary(fact.request.schema),
          members: fact.request.members?.map((member) => ({
            name: member.name,
            required: member.required,
            schema: typeSummary(member.schema),
          })),
        }
      : undefined,
    responses: (fact.responses ?? []).map((response) => ({
      status: response.status,
      statusKind: response.statusKind,
      schema: typeSummary(response.schema),
      headers: response.headers?.map((header) => ({
        name: header.name,
        schema: typeSummary(header.schema),
      })),
    })),
    paging: metadataSummary(fact.paging),
    lro: metadataSummary(fact.lro),
  };
}

function compactSdkFact(fact) {
  return {
    id: fact.id,
    projectId: fact.projectId,
    revision: fact.revision,
    factKind: fact.factKind,
    kind: fact.kind,
    identity: fact.identity,
    crossLanguageDefinitionId: fact.crossLanguageDefinitionId,
    client: fact.client,
    owner: fact.owner,
    parent: fact.parent,
    name: fact.name,
    access: fact.access,
    usage: fact.usage,
    reachable: fact.reachable,
    parameters: fact.parameters?.map((parameter) => ({
      position: parameter.position,
      name: parameter.name,
      optional: parameter.optional,
      onClient: parameter.onClient,
      type: typeSummary(parameter.type),
    })),
    responseType: typeSummary(fact.responseType),
    properties: fact.properties?.map((property) => ({
      name: property.name,
      serializedName: property.serializedName,
      optional: property.optional,
      access: property.access,
      flatten: property.flatten,
      type: typeSummary(property.type),
    })),
    values: fact.values?.map((item) => ({ name: item.name, value: item.value })),
    isFixed: fact.isFixed,
    isUnionAsEnum: fact.isUnionAsEnum,
    paging: metadataSummary(fact.paging),
    lro: metadataSummary(fact.lro),
    decorators: fact.decorators,
  };
}

function compactFact(id, fact) {
  if (id.startsWith("sdk-fact-")) return compactSdkFact(fact);
  if (id.startsWith("operation-") || id.startsWith("rest-fact-")) {
    return compactOperationFact(fact);
  }
  return { id, summary: typeSummary(fact) };
}

function referencedFacts(semantic, rest, downstream) {
  const ids = new Set();
  for (const unit of semantic.reviewUnits) {
    for (const id of [
      ...unit.operationIds,
      ...unit.beforeFactIds,
      ...unit.afterFactIds,
    ]) ids.add(id);
  }
  for (const candidate of [...rest.candidates, ...downstream.candidates]) {
    for (const id of candidate.evidenceFactIds) ids.add(id);
  }
  const available = { ...semantic.facts, ...rest.facts, ...downstream.facts };
  return Object.fromEntries(
    [...ids]
      .filter((id) => available[id] !== undefined)
      .sort()
      .map((id) => [id, compactFact(id, available[id])]),
  );
}

function compactSources(sourceIndex, semantic, rest, downstream) {
  const ids = new Set();
  for (const item of [
    ...semantic.reviewUnits,
    ...rest.candidates,
    ...downstream.candidates,
  ]) {
    for (const id of item.sourceChangeIds) ids.add(id);
  }
  return Object.fromEntries(
    sourceIndex.sourceChanges
      .filter((source) => ids.has(source.id))
      .sort((left, right) => left.id.localeCompare(right.id))
      .map((source) => [
        source.id,
        {
          id: source.id,
          path: source.path,
          status: source.status,
          origins: source.origins,
          hunks: source.hunks,
          declarations: source.declarations,
        },
      ]),
  );
}

function accountInput(input, maximumBytes) {
  const tiers = maximumBytes
    ? [
        ...BUDGET_TIERS.filter(([, limit]) => limit < maximumBytes),
        ["configured-maximum", maximumBytes],
      ]
    : BUDGET_TIERS;
  let bytes = 0;
  let tier;
  for (let attempt = 0; attempt < 4; attempt += 1) {
    input.inputAccounting = {
      budgetTier: tier?.[0],
      budgetBytes: tier?.[1],
      bytes,
      estimatedTokens: Math.ceil(bytes / 4),
      retained: {
        sourceChanges: Object.keys(input.sourceChanges).length,
        facts: Object.keys(input.facts).length,
        semanticReviewUnits: input.semanticReviewUnits.length,
        restCandidates: input.restCandidates.length,
        downstreamCandidates: input.downstreamCandidates.length,
      },
      omittedRedundant: {
        rawEmitterArtifacts: true,
        compilerLogs: true,
        unchangedInventories: true,
        unreferencedFacts: true,
      },
    };
    bytes = Buffer.byteLength(JSON.stringify(input));
    tier = tiers.find(([, limit]) => bytes <= limit);
  }
  if (!tier) {
    throw new Error(
      `Required model input is ${bytes} bytes, above the ${tiers.at(-1)[1]} byte maximum.`,
    );
  }
  input.inputAccounting.budgetTier = tier[0];
  input.inputAccounting.budgetBytes = tier[1];
  input.inputAccounting.bytes = Buffer.byteLength(JSON.stringify(input));
  input.inputAccounting.estimatedTokens = Math.ceil(input.inputAccounting.bytes / 4);
  return input;
}

export function buildModelInput({
  manifest,
  sourceIndex,
  semantic,
  rest,
  downstream,
  maximumBytes,
}) {
  const input = {
    schemaVersion: 1,
    context: {
      sourceComparison: {
        baseCommit: manifest.comparison.mergeBaseCommit,
        headCommit: manifest.comparison.headCommit,
        baseRef: manifest.comparison.baseRef,
        workingTree: manifest.comparison.workingTree,
      },
      projects: manifest.projects.map(({ id, path: projectPath, artifactComparison, apiVersions }) => ({
        id,
        path: projectPath,
        artifactComparison: artifactComparison ?? {
          mode: "legacy",
          baseline: {
            sourceRevision: "base",
            commit: manifest.comparison.mergeBaseCommit,
            apiVersion: apiVersions?.base,
            reason: apiVersions?.baseReason,
          },
          target: {
            sourceRevision: "current",
            commit: manifest.comparison.headCommit,
            apiVersion: apiVersions?.current,
            reason: apiVersions?.currentReason,
          },
        },
      })),
    },
    sourceChanges: compactSources(sourceIndex, semantic, rest, downstream),
    facts: referencedFacts(semantic, rest, downstream),
    semanticReviewUnits: semantic.status === "ready" ? semantic.reviewUnits : [],
    restCandidates: rest.status === "ready" ? rest.candidates : [],
    downstreamCandidates: downstream.status === "ready" ? downstream.candidates : [],
    downstreamRootCauses: downstream.status === "ready" ? downstream.rootCauses ?? [] : [],
    deferredDimensions: {
      compliance: "planned",
      documentQuality: "planned",
    },
    blockers: [...manifest.blockers, ...semantic.blockers, ...rest.blockers, ...downstream.blockers],
    inputAccounting: {},
  };
  return accountInput(input, maximumBytes);
}

function blockedAssessment(manifest, semantic, rest, downstream) {
  return {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    title: `TypeSpec assessment: ${manifest.projects.map((project) => project.path).join(", ")}`,
    repository: manifest.repository,
    comparison: {
      baseRef: manifest.comparison.baseRef,
      baseCommit: manifest.comparison.mergeBaseCommit,
      headCommit: manifest.comparison.headCommit,
      workingTree: manifest.comparison.workingTree,
    },
    artifactComparisons: manifest.projects.map((project) => ({
      projectId: project.id,
      ...(project.artifactComparison ?? {
        mode: "legacy",
        baseline: {
          sourceRevision: "base",
          commit: manifest.comparison.mergeBaseCommit,
          apiVersion: project.apiVersions?.base,
          reason: project.apiVersions?.baseReason ?? "legacy-selection",
        },
        target: {
          sourceRevision: "current",
          commit: manifest.comparison.headCommit,
          apiVersion: project.apiVersions?.current,
          reason: project.apiVersions?.currentReason ?? "legacy-selection",
        },
      }),
    })),
    confidence: "low",
    safety: { scope: "rest-and-downstream-only", status: "not-assessed" },
    dimensions: {
      semantic: { status: "not-assessed", items: [], blockers: semantic.blockers },
      rest: { status: "not-assessed", findings: [], blockers: rest.blockers },
      downstream: { status: "not-assessed", findings: [], blockers: downstream.blockers },
      compliance: { status: "planned", summary: "Deferred from the MVP." },
      documentQuality: { status: "planned", summary: "Planned by the design document." },
    },
    changedFiles: manifest.changedFiles,
    projects: manifest.projects,
    blockers: [...manifest.blockers, ...semantic.blockers, ...rest.blockers, ...downstream.blockers],
    provenance: { preparationManifest: "preparation-manifest.json" },
    timings: manifest.timings,
  };
}

export async function runAssessmentAnalysis(options) {
  const output = path.resolve(options.output);
  const manifest = await prepareAssessment({ ...options, output });
  if (manifest.status === "no-changes") {
    const result = {
      schemaVersion: 1,
      status: "no-changes",
      message: "No changed TypeSpec was found in the selected specification.",
      comparison: manifest.comparison,
    };
    writeJson(path.join(output, "model-input.json"), result);
    return result;
  }
  const sourceIndex = readJson(path.join(output, "source", "source-index.json"));
  fs.mkdirSync(path.join(output, "dimensions"), { recursive: true });
  const runDimension = (name, action) => {
    const started = performance.now();
    const result = action();
    manifest.timings[name] = Math.round(performance.now() - started);
    return result;
  };
  const semantic = runDimension("semanticAnalysisMs", () =>
    analyzeSemanticIntents({
      manifest,
      sourceIndex,
      workRoot: output,
      output: path.join(output, "dimensions", "semantic-intents-input.json"),
    }),
  );
  const rest = runDimension("restAnalysisMs", () =>
    analyzeRestBreaking({
      manifest,
      sourceIndex,
      workRoot: output,
      output: path.join(output, "dimensions", "rest-breaking-input.json"),
    }),
  );
  const downstream = runDimension("downstreamAnalysisMs", () =>
    analyzeDownstreamBreaking({
      manifest,
      sourceIndex,
      workRoot: output,
      output: path.join(output, "dimensions", "downstream-breaking-input.json"),
    }),
  );
  writeJson(path.join(output, "preparation-manifest.json"), manifest);
  const allBlocked = [semantic, rest, downstream].every((item) => item.status === "blocked");
  if (allBlocked) {
    const assessment = blockedAssessment(manifest, semantic, rest, downstream);
    const errors = validateAssessment(assessment);
    if (errors.length) throw new Error(errors.join("\n"));
    writeJson(path.join(output, "assessment.json"), assessment);
    fs.writeFileSync(path.join(output, "assessment.html"), renderAssessmentHtml(assessment));
    return { status: "blocked", assessment };
  }
  const configuredMaximum = options.model_input_budget_bytes === undefined
    ? undefined
    : Number(options.model_input_budget_bytes);
  if (
    configuredMaximum !== undefined &&
    (!Number.isInteger(configuredMaximum) || configuredMaximum <= 0)
  ) {
    throw new Error("--model-input-budget-bytes requires a positive integer.");
  }
  const modelInput = buildModelInput({
    manifest,
    sourceIndex,
    semantic,
    rest,
    downstream,
    maximumBytes: configuredMaximum,
  });
  writeJson(path.join(output, "model-input.json"), modelInput);
  return { status: "awaiting-agent-judgment", modelInput };
}

if (isMain(import.meta.url)) {
  runMain(async () => {
    const args = parseArgs(process.argv.slice(2), {
      required: ["specification", "output"],
      defaults: { repo: process.cwd(), base: "origin/main" },
    });
    const result = await runAssessmentAnalysis(args);
    console.log(`${result.status}: ${path.join(path.resolve(args.output), "model-input.json")}`);
    if (result.status === "blocked") process.exitCode = 1;
  });
}
