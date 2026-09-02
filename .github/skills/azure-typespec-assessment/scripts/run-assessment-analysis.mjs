import fs from "node:fs";
import path from "node:path";
import { parseArgs, isMain, readJson, runMain, writeJson } from "./cli.mjs";
import { prepareAssessment } from "./prepare-assessment.mjs";
import { analyzeSemanticIntents } from "./analyze-semantic-intents.mjs";
import { analyzeRestBreaking } from "./analyze-rest-breaking.mjs";
import { analyzeDownstreamBreaking } from "./analyze-downstream-breaking.mjs";
import { validateAssessment } from "./validate-assessment.mjs";
import { renderAssessmentHtml } from "./render-assessment-html.mjs";
import { buildComplianceSearchRequests } from "./compliance-search-request.mjs";
import { stableId } from "./stable-id.mjs";

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
    return (
      value.id ??
      value.crossLanguageDefinitionId ??
      value.name ??
      value.kind ??
      "nested"
    );
  }
  if (Array.isArray(value))
    return value.map((item) => typeSummary(item, depth + 1));
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
      result[key] =
        typeof value[key] === "object"
          ? typeSummary(value[key], depth + 1)
          : value[key];
    }
  }
  if (value.items) result.items = typeSummary(value.items, depth + 1);
  if (value.valueType)
    result.valueType = typeSummary(value.valueType, depth + 1);
  if (value.keyType) result.keyType = typeSummary(value.keyType, depth + 1);
  if (value.variantTypes)
    result.variantTypes = value.variantTypes.map((item) =>
      typeSummary(item, depth + 1),
    );
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
          ? (item.crossLanguageDefinitionId ??
            item.name ??
            item.serializedName ??
            item.kind)
          : item,
      );
    }
  }
  for (const key of ["pollingStep", "statusMonitorStep", "finalStep"]) {
    if (value[key])
      result[key] = { kind: value[key].kind, target: value[key].target?.kind };
  }
  return result;
}

function compactOperationFact(fact) {
  return {
    id: fact.id,
    projectId: fact.projectId,
    revision: fact.revision,
    comparisonRole: fact.comparisonRole,
    sourceRevision: fact.sourceRevision,
    sourceCommit: fact.sourceCommit,
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
    comparisonRole: fact.comparisonRole,
    sourceRevision: fact.sourceRevision,
    sourceCommit: fact.sourceCommit,
    apiVersions: fact.apiVersions,
    factKind: fact.factKind,
    kind: fact.kind,
    identity: fact.identity,
    crossLanguageDefinitionId: fact.crossLanguageDefinitionId,
    client: fact.client,
    operation: fact.operation
      ? {
          operationId: fact.operation.operationId,
          method: fact.operation.method,
          path: fact.operation.path,
        }
      : undefined,
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
    values: fact.values?.map((item) => ({
      name: item.name,
      value: item.value,
    })),
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

function inferenceRelevantFactIds({
  semanticUnits,
  sourceChanges,
  semantic,
  rest,
  downstream,
  coverages,
}) {
  const ids = new Set();
  const available = { ...semantic.facts, ...rest.facts, ...downstream.facts };

  for (const unit of semanticUnits) {
    if (!(coverages.get(unit.id)?.uncoveredHunkIds.length > 0)) continue;
    for (const id of [
      ...(unit.beforeFactIds ?? []),
      ...(unit.afterFactIds ?? []),
      ...(unit.operations ?? []).flatMap((operation) => [
        operation.beforeFactId,
        operation.afterFactId,
      ]),
    ]) {
      if (id && available[id] !== undefined) ids.add(id);
    }

    const projectIds = new Set(
      unit.projectIds ?? (unit.projectId ? [unit.projectId] : []),
    );
    const sourceText = (unit.hunkIds ?? [])
      .map((hunkId) =>
        inferenceHunkText(sourceForHunk(unit, sourceChanges, hunkId), hunkId),
      )
      .join("\n");
    const operationIds = new Set(
      (unit.operations ?? []).map((operation) => operation.operationId),
    );
    for (const [id, fact] of Object.entries(available)) {
      if (fact.projectId && !projectIds.has(fact.projectId)) continue;
      const terms = [
        fact.operationId,
        fact.identity,
        fact.crossLanguageDefinitionId,
        fact.name,
      ]
        .filter((term) => typeof term === "string" && term.length >= 3)
        .flatMap((term) => [term, term.split(".").at(-1)]);
      if (
        operationIds.has(fact.operationId) ||
        terms.some((term) => sourceText.includes(term))
      ) {
        ids.add(id);
      }
    }
  }
  return ids;
}

function referencedFacts(semantic, rest, downstream, additionalIds) {
  const ids = new Set();
  for (const candidate of [...rest.candidates, ...downstream.candidates]) {
    for (const id of candidate.evidenceFactIds) ids.add(id);
  }
  for (const id of additionalIds) ids.add(id);
  const available = { ...semantic.facts, ...rest.facts, ...downstream.facts };
  return Object.fromEntries(
    [...ids]
      .filter((id) => available[id] !== undefined)
      .sort()
      .map((id) => [id, compactFact(id, available[id])]),
  );
}

function semanticSourceExcerpts(unit, sourceChanges) {
  const allowed = new Set(unit.hunkIds ?? []);
  return unit.sourceChangeIds
    .flatMap((sourceId) => {
      const source = sourceChanges[sourceId];
      return (source?.hunks ?? [])
        .filter((hunk) => allowed.has(hunk.id))
        .map((hunk) => ({
          sourceChangeId: sourceId,
          hunkId: hunk.id,
          path: source.path,
          text: (hunk.lines ?? [])
            .filter((line) => /^[+-](?![+-])/.test(line))
            .slice(0, 12)
            .join("\n"),
        }));
    })
    .filter((excerpt) => excerpt.text)
    .sort((left, right) => {
      const score = (excerpt) => {
        const compatibilityFile =
          /(?:^|\/)(?:client|back-compatible)\.tsp$/i.test(excerpt.path);
        const substantive =
          /\b(model|interface|op|enum|union|scalar|alias)\b/.test(excerpt.text);
        return (compatibilityFile ? 2 : 0) + (substantive ? 0 : 1);
      };
      return (
        score(left) - score(right) ||
        left.path.localeCompare(right.path) ||
        left.hunkId.localeCompare(right.hunkId)
      );
    })
    .slice(0, 3);
}

function compactSemanticReviewUnit(unit, sourceChanges, deterministicCoverage) {
  const declarations = unit.sourceChangeIds.flatMap((sourceId) => {
    const source = sourceChanges[sourceId];
    const allowed = new Set(unit.hunkIds ?? []);
    return (source?.declarations ?? []).filter((declaration) =>
      declaration.hunkIds?.some((hunkId) => allowed.has(hunkId)),
    );
  });
  const operations = [
    ...(unit.operations ??
      (unit.operationIds ?? []).map((id) => ({
        operationId: id,
      }))),
  ].sort((left, right) => left.operationId.localeCompare(right.operationId));
  return {
    reviewUnitId: unit.id,
    action: unit.action ?? unit.changeKind ?? "modify",
    declarationKinds: [
      ...new Set(declarations.map((item) => item.kind).filter(Boolean)),
    ].sort(),
    qualifiedNames: [
      ...new Set(
        declarations.map((item) => item.qualifiedName).filter(Boolean),
      ),
    ].sort(),
    changedConstructs: [
      ...new Set(
        declarations
          .flatMap((item) => [
            ...(item.decorators ?? []).map((decorator) =>
              typeof decorator === "string" ? decorator : decorator?.name,
            ),
            item.baseType,
            ...(item.compilerEvidence?.referencedNames ?? []),
          ])
          .filter(Boolean),
      ),
    ].sort(),
    representativeSourceExcerpts: semanticSourceExcerpts(unit, sourceChanges),
    affectedOperationCount: operations.length,
    representativeOperationIds: operations
      .slice(0, 3)
      .map((item) => item.operationId),
    restChangedOperationCount: operations.filter((item) => item.restChanged)
      .length,
    groupingSummaries: (unit.groupingEvidence?.edges ?? [])
      .map((item) => item.summary)
      .filter(Boolean)
      .slice(0, 3),
    deterministicCoverage,
    inferenceRequired: deterministicCoverage.uncoveredHunkIds.length > 0,
  };
}

function intersection(left, right) {
  const rightSet = right instanceof Set ? right : new Set(right);
  return left.filter((item) => rightSet.has(item));
}

function sourceForHunk(unit, sourceChanges, hunkId) {
  return unit.sourceChangeIds
    .map((sourceChangeId) => sourceChanges[sourceChangeId])
    .find((source) => source?.hunks?.some((hunk) => hunk.id === hunkId));
}

function changedHunkText(source, hunkId) {
  const hunk = source?.hunks?.find((item) => item.id === hunkId);
  return (hunk?.lines ?? [])
    .filter((line) => /^[+-](?![+-])/.test(line))
    .slice(0, 20)
    .join("\n");
}

function inferenceHunkText(source, hunkId) {
  const hunk = source?.hunks?.find((item) => item.id === hunkId);
  return (hunk?.lines ?? []).slice(0, 40).join("\n");
}

function documentationOnly(source, hunkId) {
  const changed = changedHunkText(source, hunkId)
    .split("\n")
    .map((line) => line.slice(1).trim())
    .filter(Boolean);
  return (
    changed.length > 0 &&
    changed.every(
      (line) =>
        line.startsWith("//") ||
        line.startsWith("/*") ||
        line.startsWith("*") ||
        line === "*/",
    )
  );
}

function contractNeutralSupportingChange(source, hunkId) {
  const changed = changedHunkText(source, hunkId)
    .split("\n")
    .map((line) => line.slice(1).trim())
    .filter(Boolean);
  return (
    changed.length > 0 &&
    changed.every(
      (line) =>
        /^import\s/.test(line) ||
        /^using\s/.test(line) ||
        /^#suppress\s/.test(line) ||
        line.startsWith("//") ||
        line.startsWith("/*") ||
        line.startsWith("*") ||
        line === "*/",
    )
  );
}

function docDecoratorOnly(source, hunkId) {
  const hunk = source?.hunks?.find((item) => item.id === hunkId);
  const changed = changedHunkText(source, hunkId);
  const decorators = [...changed.matchAll(/@@?[A-Za-z0-9_.]+/g)].map(
    (match) => match[0],
  );
  return (
    (hunk?.lines ?? []).some((line) => /@@?doc\s*\(/.test(line)) &&
    decorators.every(
      (decorator) => decorator === "@doc" || decorator === "@@doc",
    ) &&
    !/\b(model|interface|op|enum|union|scalar|alias)\b/.test(changed)
  );
}

function structuralParenthesisDelta(line) {
  let quote;
  let escaped = false;
  let delta = 0;
  for (let index = 0; index < line.length; index += 1) {
    const character = line[index];
    if (escaped) {
      escaped = false;
      continue;
    }
    if (quote) {
      if (character === "\\") escaped = true;
      else if (character === quote) quote = undefined;
      continue;
    }
    if (character === '"' || character === "'" || character === "`") {
      quote = character;
    } else if (character === "/" && line[index + 1] === "/") {
      break;
    } else if (character === "(") {
      delta += 1;
    } else if (character === ")") {
      delta -= 1;
    }
  }
  return delta;
}

function decoratorOnlyChange(
  source,
  hunkId,
  allowedDecorators,
  allowedStandalone = () => false,
) {
  const hunk = source?.hunks?.find((item) => item.id === hunkId);
  let activeDecorator;
  let depth = 0;
  let changedLineCount = 0;
  let sawAllowedDecorator = false;

  for (const rawLine of hunk?.lines ?? []) {
    const changed = rawLine.startsWith("+") || rawLine.startsWith("-");
    const line =
      changed || rawLine.startsWith(" ") ? rawLine.slice(1) : rawLine;
    const decorators = [...line.matchAll(/@@?[A-Za-z0-9_.]+/g)].map(
      (match) => match[0],
    );
    const standaloneAllowed = allowedStandalone(line.trim());
    const startsAllowedDecorator =
      decorators.length > 0 &&
      decorators.every((decorator) => allowedDecorators.has(decorator));

    if (changed) {
      changedLineCount += 1;
      if (
        (!standaloneAllowed &&
          decorators.some((decorator) => !allowedDecorators.has(decorator))) ||
        (!activeDecorator && !startsAllowedDecorator && !standaloneAllowed)
      ) {
        return false;
      }
    }

    if (startsAllowedDecorator) {
      activeDecorator = decorators[0];
      sawAllowedDecorator = true;
      depth = 0;
    }
    if (activeDecorator) {
      depth += structuralParenthesisDelta(line);
      if (depth <= 0) {
        activeDecorator = undefined;
        depth = 0;
      }
    }
  }

  return changedLineCount > 0 && sawAllowedDecorator;
}

function representedArtifactChange(source, hunkId, rest, downstream) {
  const changedText = changedHunkText(source, hunkId);
  if (
    rest.status === "ready" &&
    decoratorOnlyChange(
      source,
      hunkId,
      new Set([
        "@@operationId",
        "@@doc",
        "@@clientName",
        "@@Azure.ClientGenerator.Core.clientName",
        "@extension",
        "@Azure.Core.useFinalStateVia",
      ]),
      (line) => line.startsWith("#suppress "),
    ) &&
    !/\b(csharp|java|javascript|python|go)\b/i.test(changedText)
  ) {
    return "rest-artifact-represented-change-compared";
  }
  if (
    downstream.status === "ready" &&
    decoratorOnlyChange(
      source,
      hunkId,
      new Set(["@@clientName", "@@Azure.ClientGenerator.Core.clientName"]),
    ) &&
    !/\b(csharp|java|javascript|python|go)\b/i.test(changedText)
  ) {
    return "language-neutral-sdk-customization-compared";
  }
  return undefined;
}

function decoratorsAffectingChangedLines(source, hunkId) {
  const hunk = source?.hunks?.find((item) => item.id === hunkId);
  const decorators = new Set();
  let activeDecorator;
  let depth = 0;

  for (const rawLine of hunk?.lines ?? []) {
    const changed = rawLine.startsWith("+") || rawLine.startsWith("-");
    const line =
      changed || rawLine.startsWith(" ") ? rawLine.slice(1) : rawLine;
    const decorator = line.match(/^\s*(@@?[A-Za-z0-9_.]+)/)?.[1];
    if (decorator) {
      activeDecorator = decorator;
      depth = 0;
    }
    if (changed && activeDecorator) decorators.add(activeDecorator);
    if (activeDecorator) {
      depth += structuralParenthesisDelta(line);
      if (depth <= 0) {
        activeDecorator = undefined;
        depth = 0;
      }
    }
  }

  return decorators;
}

function inferenceGapReason(source, hunkId) {
  const hunk = source?.hunks?.find((item) => item.id === hunkId);
  const fullText = (hunk?.lines ?? []).join("\n");
  const changedDecorators = decoratorsAffectingChangedLines(source, hunkId);
  const hasLanguageScope = /\b(csharp|java|javascript|python|go)\b/i.test(
    fullText,
  );
  if (changedDecorators.has("@@clientLocation") && hasLanguageScope) {
    return "language-specific-client-location-not-represented";
  }
  if (
    [...changedDecorators].some((decorator) =>
      /^(?:@@?(?:clientName|alternateType)|@Azure\.ClientGenerator\.Core\.Legacy\.flattenProperty)$/.test(
        decorator,
      ),
    ) &&
    hasLanguageScope
  ) {
    return "language-specific-sdk-customization-not-represented";
  }
  const representedDecorators = new Set([
    "@action",
    "@added",
    "@armProviderNamespace",
    "@armResourceAction",
    "@armResourceCollectionAction",
    "@armResourceCreateOrUpdate",
    "@armResourceDelete",
    "@armResourceInternal",
    "@armResourceList",
    "@armResourceOperations",
    "@armResourceRead",
    "@armResourceUpdate",
    "@armVirtualResource",
    "@autoRoute",
    "@body",
    "@bodyRoot",
    "@delete",
    "@discriminator",
    "@doc",
    "@encode",
    "@error",
    "@errorsDoc",
    "@example",
    "@extension",
    "@format",
    "@get",
    "@header",
    "@identifiers",
    "@key",
    "@list",
    "@maxItems",
    "@maxLength",
    "@maxValue",
    "@maxValueExclusive",
    "@minItems",
    "@minLength",
    "@minValue",
    "@minValueExclusive",
    "@pageItems",
    "@parentResource",
    "@patch",
    "@path",
    "@pattern",
    "@pollingOperation",
    "@post",
    "@put",
    "@query",
    "@removed",
    "@returnsDoc",
    "@route",
    "@secret",
    "@segment",
    "@server",
    "@service",
    "@sharedRoute",
    "@statusCode",
    "@summary",
    "@tag",
    "@typeChangedFrom",
    "@uniqueItems",
    "@useDependency",
    "@visibility",
    "@Azure.ClientGenerator.Core.Legacy.flattenProperty",
    "@Azure.Core.useFinalStateVia",
    "@Azure.ResourceManager.Legacy.customAzureResource",
    "@Azure.ResourceManager.Legacy.feature",
    "@Http.Private.includeInapplicableMetadataInPayload",
    "@Xml.name",
    "@Xml.unwrapped",
    "@armCommonTypesVersion",
    "@nextLink",
    "@@doc",
    "@@extension",
    "@@identifiers",
    "@@operationId",
    "@@override",
    "@@clientName",
    "@@Azure.ClientGenerator.Core.clientName",
    "@@visibility",
  ]);
  if (
    [...changedDecorators].some(
      (decorator) => !representedDecorators.has(decorator),
    )
  ) {
    return "unsupported-customization-not-represented";
  }
  return undefined;
}

function declarationHunkIds(unit, sourceChanges) {
  const ids = new Set(unit.hunkIds ?? []);
  const result = new Map();
  for (const sourceChangeId of unit.sourceChangeIds) {
    for (const declaration of sourceChanges[sourceChangeId]?.declarations ??
      []) {
      for (const hunkId of declaration.hunkIds ?? []) {
        if (!ids.has(hunkId)) continue;
        const values = result.get(hunkId) ?? [];
        values.push(declaration);
        result.set(hunkId, values);
      }
    }
  }
  return result;
}

function operationHunkIds(unit) {
  const result = new Map();
  for (const operation of unit.operations ?? []) {
    for (const hunkId of operation.hunkIds ?? []) {
      const values = result.get(hunkId) ?? [];
      values.push(operation);
      result.set(hunkId, values);
    }
  }
  return result;
}

function normalizedSymbolNames(value) {
  if (!value) return [];
  const full = value.toLowerCase();
  const leaf = full.split(".").at(-1);
  return full === leaf ? [full] : [full, leaf];
}

function candidateHunks(
  unit,
  candidate,
  facts,
  declarationsByHunk,
  operationsByHunk,
) {
  const matched = new Set();
  const candidateOperations = new Set(candidate.operationIds ?? []);
  for (const [hunkId, operations] of operationsByHunk) {
    if (
      operations.some((operation) =>
        candidateOperations.has(operation.operationId),
      )
    ) {
      matched.add(hunkId);
    }
  }

  const candidateOperationKeys = new Set(
    (candidate.evidenceFactIds ?? [])
      .map((id) => facts[id])
      .filter((fact) => fact?.operation?.verb && fact.operation.path)
      .map(
        (fact) => `${fact.operation.verb.toLowerCase()} ${fact.operation.path}`,
      ),
  );
  if (candidateOperationKeys.size) {
    for (const [hunkId, operations] of operationsByHunk) {
      const operationKeys = operations.flatMap((operation) =>
        [operation.beforeFactId, operation.afterFactId]
          .map((id) => facts[id])
          .filter((fact) => fact?.method && fact.path)
          .map((fact) => `${fact.method.toLowerCase()} ${fact.path}`),
      );
      if (operationKeys.some((key) => candidateOperationKeys.has(key)))
        matched.add(hunkId);
    }
  }

  const candidateSymbols = new Set(
    normalizedSymbolNames(candidate.crossLanguageDefinitionId),
  );
  if (candidateSymbols.size) {
    for (const [hunkId, declarations] of declarationsByHunk) {
      if (
        declarations.some((declaration) =>
          normalizedSymbolNames(declaration.qualifiedName).some((name) =>
            candidateSymbols.has(name),
          ),
        )
      ) {
        matched.add(hunkId);
      }
    }
  }
  return matched;
}

function deterministicCoverage({
  unit,
  sourceChanges,
  semantic,
  rest,
  downstream,
  complianceRequest,
}) {
  const declarationsByHunk = declarationHunkIds(unit, sourceChanges);
  const operationsByHunk = operationHunkIds(unit);
  const restByHunk = new Map((unit.hunkIds ?? []).map((id) => [id, []]));
  const downstreamByHunk = new Map((unit.hunkIds ?? []).map((id) => [id, []]));
  const unitSources = new Set(unit.sourceChangeIds ?? []);
  const relevant = (candidate) =>
    intersection(candidate.sourceChangeIds ?? [], unitSources).length > 0;

  for (const candidate of rest.status === "ready" ? rest.candidates : []) {
    if (!relevant(candidate)) continue;
    for (const hunkId of candidateHunks(
      unit,
      candidate,
      { ...semantic.facts, ...rest.facts },
      declarationsByHunk,
      operationsByHunk,
    )) {
      restByHunk.get(hunkId)?.push(candidate.id);
    }
  }
  for (const candidate of downstream.status === "ready"
    ? downstream.candidates
    : []) {
    if (!relevant(candidate)) continue;
    for (const hunkId of candidateHunks(
      unit,
      candidate,
      { ...semantic.facts, ...downstream.facts },
      declarationsByHunk,
      operationsByHunk,
    )) {
      downstreamByHunk.get(hunkId)?.push(candidate.id);
    }
  }

  const classifications = (unit.hunkIds ?? []).map((hunkId) => {
    const restCandidateIds = [...new Set(restByHunk.get(hunkId) ?? [])].sort();
    const downstreamCandidateIds = [
      ...new Set(downstreamByHunk.get(hunkId) ?? []),
    ].sort();
    const source = sourceForHunk(unit, sourceChanges, hunkId);
    if (rest.status === "blocked" || downstream.status === "blocked") {
      return {
        hunkId,
        status: "blocked",
        reason: "required-deterministic-analysis-blocked",
        restCandidateIds: [],
        downstreamCandidateIds: [],
      };
    }
    const gapReason = inferenceGapReason(source, hunkId);
    if (gapReason) {
      return {
        hunkId,
        status: "unknown",
        reason: gapReason,
        restCandidateIds: [],
        downstreamCandidateIds: [],
      };
    }
    if (restCandidateIds.length || downstreamCandidateIds.length) {
      return {
        hunkId,
        status: "candidate-generated",
        reason: "mapped-deterministic-candidate",
        restCandidateIds,
        downstreamCandidateIds,
      };
    }
    if (declarationsByHunk.has(hunkId) || operationsByHunk.has(hunkId)) {
      return {
        hunkId,
        status: "no-impact",
        reason: declarationsByHunk.has(hunkId)
          ? "declaration-mapped-and-contracts-compared"
          : "operation-mapped-and-contracts-compared",
        restCandidateIds: [],
        downstreamCandidateIds: [],
      };
    }
    if (documentationOnly(source, hunkId)) {
      return {
        hunkId,
        status: "semantic-only",
        reason: "documentation-only-change",
        restCandidateIds: [],
        downstreamCandidateIds: [],
      };
    }
    if (docDecoratorOnly(source, hunkId)) {
      return {
        hunkId,
        status: "semantic-only",
        reason: "documentation-decorator-only-change",
        restCandidateIds: [],
        downstreamCandidateIds: [],
      };
    }
    if (contractNeutralSupportingChange(source, hunkId)) {
      return {
        hunkId,
        status: "semantic-only",
        reason: "contract-neutral-supporting-change",
        restCandidateIds: [],
        downstreamCandidateIds: [],
      };
    }
    const representedReason = representedArtifactChange(
      source,
      hunkId,
      rest,
      downstream,
    );
    if (representedReason) {
      return {
        hunkId,
        status: "no-impact",
        reason: representedReason,
        restCandidateIds: [],
        downstreamCandidateIds: [],
      };
    }
    return {
      hunkId,
      status: "unknown",
      reason: "source-change-not-mapped-to-deterministic-contract-evidence",
      restCandidateIds: [],
      downstreamCandidateIds: [],
    };
  });
  const uncoveredHunkIds = classifications
    .filter((item) => item.status === "unknown")
    .map((item) => item.hunkId);
  return {
    restCandidateIds: [
      ...new Set(classifications.flatMap((item) => item.restCandidateIds)),
    ].sort(),
    downstreamCandidateIds: [
      ...new Set(
        classifications.flatMap((item) => item.downstreamCandidateIds),
      ),
    ].sort(),
    complianceSearchRequestIds: complianceRequest
      ? [complianceRequest.requestId]
      : [],
    relatedOperationIds: [
      ...new Set(
        (unit.operations ?? []).map((operation) => operation.operationId),
      ),
    ].sort(),
    coveredHunkIds: classifications
      .filter((item) => item.status !== "unknown")
      .map((item) => item.hunkId),
    uncoveredHunkIds,
    classifications,
    gaps: classifications
      .filter((item) => item.status === "unknown")
      .map((item) => ({
        hunkId: item.hunkId,
        reason: item.reason,
      })),
  };
}

function buildInferenceRequests(semanticUnits, sourceChanges, coverages) {
  return semanticUnits.flatMap((unit) => {
    const coverage = coverages.get(unit.id);
    return coverage.uncoveredHunkIds.map((hunkId) => {
      const source = sourceForHunk(unit, sourceChanges, hunkId);
      const request = {
        reviewUnitId: unit.id,
        sourceChangeId: source.id,
        hunkId,
        reason: coverage.gaps.find((item) => item.hunkId === hunkId)?.reason,
        sourceExcerpt: inferenceHunkText(source, hunkId),
        relatedOperationIds: (unit.operations ?? [])
          .filter((operation) => operation.hunkIds?.includes(hunkId))
          .map((operation) => operation.operationId)
          .sort(),
        allowedDimensions: ["rest", "downstream"],
      };
      return {
        requestId: stableId("inference-request", request),
        ...request,
      };
    });
  });
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
        downstreamRootCauses: input.downstreamRootCauses.length,
        complianceSearchRequests: input.complianceSearchRequests.length,
        inferenceRequests: input.inferenceRequests.length,
        complianceSearchSourceBytes: Buffer.byteLength(
          JSON.stringify({
            sourceChanges: input.sourceChanges,
            complianceSearchRequests: input.complianceSearchRequests,
          }),
        ),
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
  input.inputAccounting.estimatedTokens = Math.ceil(
    input.inputAccounting.bytes / 4,
  );
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
  const sourceChanges = compactSources(sourceIndex, semantic, rest, downstream);
  const semanticUnits = semantic.status === "ready" ? semantic.reviewUnits : [];
  const complianceSearchRequests = buildComplianceSearchRequests({
    semanticReviewUnits: semanticUnits,
    sourceChanges,
  });
  const complianceRequestsByUnit = new Map(
    complianceSearchRequests.map((request) => [request.reviewUnitId, request]),
  );
  const coverages = new Map(
    semanticUnits.map((unit) => [
      unit.id,
      deterministicCoverage({
        unit,
        sourceChanges,
        semantic,
        rest,
        downstream,
        complianceRequest: complianceRequestsByUnit.get(unit.id),
      }),
    ]),
  );
  const semanticReviewUnits = semanticUnits.map((unit) =>
    compactSemanticReviewUnit(unit, sourceChanges, coverages.get(unit.id)),
  );
  const inferenceRequests = buildInferenceRequests(
    semanticUnits,
    sourceChanges,
    coverages,
  );
  const retainedInferenceFactIds = inferenceRelevantFactIds({
    semanticUnits,
    sourceChanges,
    semantic,
    rest,
    downstream,
    coverages,
  });
  const input = {
    schemaVersion: 1,
    context: {
      sourceComparison: {
        baseCommit: manifest.comparison.mergeBaseCommit,
        headCommit: manifest.comparison.headCommit,
        baseRef: manifest.comparison.baseRef,
        workingTree: manifest.comparison.workingTree,
      },
      projects: manifest.projects.map(
        ({ id, path: projectPath, artifactComparison, apiVersions }) => ({
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
        }),
      ),
    },
    sourceChanges,
    facts: referencedFacts(
      semantic,
      rest,
      downstream,
      retainedInferenceFactIds,
    ),
    semanticReviewUnits,
    restCandidates: rest.status === "ready" ? rest.candidates : [],
    downstreamCandidates:
      downstream.status === "ready" ? downstream.candidates : [],
    downstreamRootCauses:
      downstream.status === "ready" ? (downstream.rootCauses ?? []) : [],
    complianceSearchRequests,
    inferenceRequests,
    deferredDimensions: {
      documentQuality: "not-assessed",
    },
    blockers: [
      ...manifest.blockers,
      ...semantic.blockers,
      ...rest.blockers,
      ...downstream.blockers,
    ],
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
      semantic: {
        status: "not-assessed",
        items: [],
        blockers: semantic.blockers,
      },
      rest: { status: "not-assessed", findings: [], blockers: rest.blockers },
      downstream: {
        status: "not-assessed",
        findings: [],
        blockers: downstream.blockers,
      },
      compliance: {
        status: "not-assessed",
        summary:
          "Compliance could not run because deterministic analysis was blocked.",
        coverage: {
          semanticIntentCount: 0,
          assessedIntentCount: 0,
          selectedDocumentCount: 0,
          unassessedIntentIds: [],
        },
        intentAssessments: [],
        findings: [],
        retrievalFailures: [],
        blockers: [...manifest.blockers, ...semantic.blockers],
      },
      documentQuality: {
        status: "not-assessed",
        summary: "Document quality is not assessed.",
      },
    },
    changedFiles: manifest.changedFiles,
    projects: manifest.projects,
    blockers: [
      ...manifest.blockers,
      ...semantic.blockers,
      ...rest.blockers,
      ...downstream.blockers,
    ],
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
  const sourceIndex = readJson(
    path.join(output, "source", "source-index.json"),
  );
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
  const allBlocked = [semantic, rest, downstream].every(
    (item) => item.status === "blocked",
  );
  if (allBlocked) {
    const assessment = blockedAssessment(manifest, semantic, rest, downstream);
    const errors = validateAssessment(assessment);
    if (errors.length) throw new Error(errors.join("\n"));
    writeJson(path.join(output, "assessment.json"), assessment);
    fs.writeFileSync(
      path.join(output, "assessment.html"),
      renderAssessmentHtml(assessment),
    );
    return { status: "blocked", assessment };
  }
  const configuredMaximum =
    options.model_input_budget_bytes === undefined
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
    console.log(
      `${result.status}: ${path.join(path.resolve(args.output), "model-input.json")}`,
    );
    if (result.status === "blocked") process.exitCode = 1;
  });
}
