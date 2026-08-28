import path from "node:path";
import { normalizeTcgcContract } from "./tcgc-contract.mjs";
import { parseArgs, isMain, readJson, runMain, writeJson } from "./cli.mjs";
import { canonicalJson, stableId } from "./stable-id.mjs";
import {
  publicParameterContract,
  semanticLroContract,
  typeIdentity,
} from "./sdk-method-delta.mjs";

function loadInputs(options) {
  const manifestPath = typeof options.manifest === "string" ? path.resolve(options.manifest) : undefined;
  const workRoot = path.resolve(options.workRoot ?? (manifestPath ? path.dirname(manifestPath) : process.cwd()));
  return {
    workRoot,
    manifest: typeof options.manifest === "object" ? options.manifest : readJson(manifestPath),
    sourceIndex: options.sourceIndex ?? readJson(path.join(workRoot, "source", "source-index.json")),
  };
}

function artifactReady(artifact) {
  return artifact && (!artifact.status || artifact.status === "succeeded") && artifact.files?.length;
}

function evidence(project, sourceIndex) {
  const sourceById = new Map(sourceIndex.sourceChanges.map((item) => [item.id, item]));
  const sourceChangeIds = (project.sourceChangeIds ?? []).filter((id) => sourceById.has(id)).sort();
  const declarations = sourceChangeIds.flatMap((id) => sourceById.get(id).declarations ?? []);
  return {
    sourceChangeIds,
    declarationIds: declarations.map((item) => item.id).filter(Boolean).sort(),
    declarations,
  };
}

function addFact(facts, projectId, comparisonRole, kind, value, artifactComparison) {
  const selection = artifactComparison?.[comparisonRole];
  const fact = {
    projectId,
    comparisonRole,
    sourceRevision: selection?.sourceRevision ?? (comparisonRole === "baseline" ? "base" : "current"),
    sourceCommit: selection?.commit,
    apiVersion: selection?.apiVersion,
    factKind: kind,
    ...value,
  };
  const id = stableId("sdk-fact", fact);
  facts[id] = { ...fact, id };
  return id;
}

function same(left, right) {
  return left === undefined || right === undefined
    ? left === right
    : canonicalJson(left) === canonicalJson(right);
}

function unique(values) {
  return [...new Set(values.filter(Boolean))].sort();
}

function severity(rule) {
  return ["method-paging-changed", "method-lro-changed", "customization-changed"].includes(rule)
    ? "medium"
    : "high";
}

function candidateText(rule, symbol, detail) {
  let actual;
  switch (rule) {
    case "method-removed": actual = `${symbol} is no longer generated.`; break;
    case "method-location-changed": actual = `${symbol} moved to a different client.`; break;
    case "method-kind-changed":
      actual = `${symbol} changed method kind${detail ? ` from ${detail.before} to ${detail.after}` : ""}.`;
      break;
    case "method-parameters-changed": actual = `${symbol} has a different ordered public parameter list.`; break;
    case "method-response-changed": actual = `${symbol} has a different response type.`; break;
    case "method-access-changed": actual = `${symbol} is no longer public.`; break;
    case "method-paging-changed": actual = `${symbol} changed paging behavior.`; break;
    case "method-lro-changed": actual = `${symbol} changed long-running behavior.`; break;
    case "model-property-removed": actual = `${symbol} no longer exposes property ${detail.name}.`; break;
    case "model-property-changed":
      actual = `${symbol}.${detail.name} changed type, optionality, flattening, or access.`;
      break;
    case "model-property-added-required": actual = `${symbol} added required property ${detail.name}.`; break;
    case "enum-values-removed": actual = `${symbol} removed enum values: ${detail.values.join(", ")}.`; break;
    case "enum-extensibility-changed": actual = `${symbol} changed enum extensibility.`; break;
    case "public-surface-changed": actual = `${symbol} changed public access, usage, or reachability.`; break;
    case "client-location-changed": actual = `${symbol} changed client ownership or name.`; break;
    case "customization-changed": actual = `${symbol} changed SDK customization decorators.`; break;
    default: throw new Error(`Unsupported downstream rule: ${rule}`);
  }
  return {
    actual,
    expected: `${symbol} preserves its existing language-neutral generated SDK contract.`,
  };
}

function pushCandidate(candidates, facts, source, projectId, rule, symbol, before, after, kind, detail) {
  const beforeFactId = before
    ? addFact(facts, projectId, "baseline", kind, before, source.artifactComparison)
    : undefined;
  const afterFactId = after
    ? addFact(facts, projectId, "target", kind, after, source.artifactComparison)
    : undefined;
  const text = candidateText(rule, symbol, detail);
  const candidate = {
    rule,
    defaultSeverity: severity(rule),
    actual: text.actual,
    expected: text.expected,
    crossLanguageDefinitionId: symbol,
    sourceChangeIds: source.sourceChangeIds,
    declarationIds: source.declarationIds,
    evidenceFactIds: [beforeFactId, afterFactId].filter(Boolean),
    reviewRequired: true,
  };
  candidates.push({ id: stableId("downstream", candidate), ...candidate });
}

function compareMethods(projectId, base, current, source, facts, candidates) {
  const currentMethods = new Map(current.methods.map((item) => [item.identity, item]));
  const currentByHttp = new Map();
  for (const method of current.methods) {
    const protocol = method.operation;
    if (!protocol?.verb || !protocol?.path) continue;
    const key = `${protocol.verb}\u0000${protocol.path}`;
    const values = currentByHttp.get(key) ?? [];
    values.push(method);
    currentByHttp.set(key, values);
  }
  for (const before of base.methods) {
    let after = currentMethods.get(before.identity);
    if (!after && before.operation?.verb && before.operation?.path) {
      const matches = currentByHttp.get(`${before.operation.verb}\u0000${before.operation.path}`) ?? [];
      if (matches.length === 1) after = matches[0];
    }
    const symbol = before.crossLanguageDefinitionId ?? before.identity;
    if (!after) {
      pushCandidate(candidates, facts, source, projectId, "method-removed", symbol, before, undefined, "method");
      continue;
    }
    if (before.client !== after.client) {
      pushCandidate(candidates, facts, source, projectId, "method-location-changed", symbol, before, after, "method");
    }
    if (before.kind !== after.kind) {
      pushCandidate(
        candidates,
        facts,
        source,
        projectId,
        "method-kind-changed",
        symbol,
        before,
        after,
        "method",
        { before: before.kind, after: after.kind },
      );
    }
    if (!same(
      publicParameterContract(before.parameters),
      publicParameterContract(after.parameters),
    )) {
      pushCandidate(candidates, facts, source, projectId, "method-parameters-changed", symbol, before, after, "method");
    }
    if (!same(before.responseType, after.responseType)) {
      pushCandidate(candidates, facts, source, projectId, "method-response-changed", symbol, before, after, "method");
    }
    if (before.access === "public" && after.access !== "public") {
      pushCandidate(candidates, facts, source, projectId, "method-access-changed", symbol, before, after, "method");
    }
    if (!same(before.paging, after.paging)) {
      pushCandidate(candidates, facts, source, projectId, "method-paging-changed", symbol, before, after, "method");
    }
    if (!same(semanticLroContract(before.lro), semanticLroContract(after.lro))) {
      pushCandidate(candidates, facts, source, projectId, "method-lro-changed", symbol, before, after, "method");
    }
  }
}

function compareModels(projectId, base, current, source, facts, candidates) {
  const currentModels = new Map(current.models.map((item) => [item.identity, item]));
  for (const before of base.models.filter((item) => item.reachable)) {
    const after = currentModels.get(before.identity);
    const symbol = before.crossLanguageDefinitionId ?? before.identity;
    if (!after) {
      pushCandidate(candidates, facts, source, projectId, "public-surface-changed", symbol, before, undefined, "model");
      continue;
    }
    if (
      before.access !== after.access ||
      before.usage !== after.usage ||
      before.reachable !== after.reachable
    ) {
      pushCandidate(candidates, facts, source, projectId, "public-surface-changed", symbol, before, after, "model");
    }
    const afterProperties = new Map(after.properties.map((item) => [item.name, item]));
    const beforeProperties = new Set(before.properties.map((item) => item.name));
    for (const property of before.properties) {
      const currentProperty = afterProperties.get(property.name);
      if (!currentProperty) {
        pushCandidate(
          candidates,
          facts,
          source,
          projectId,
          "model-property-removed",
          symbol,
          before,
          after,
          "model",
          { name: property.name },
        );
      } else if (canonicalJson(property) !== canonicalJson(currentProperty)) {
        pushCandidate(
          candidates,
          facts,
          source,
          projectId,
          "model-property-changed",
          symbol,
          before,
          after,
          "model",
          { name: property.name },
        );
      }
    }
    for (const property of after.properties) {
      if (!beforeProperties.has(property.name) && !property.optional) {
        pushCandidate(
          candidates,
          facts,
          source,
          projectId,
          "model-property-added-required",
          symbol,
          before,
          after,
          "model",
          { name: property.name },
        );
      }
    }
  }
}

function compareEnums(projectId, base, current, source, facts, candidates) {
  const currentEnums = new Map(current.enums.map((item) => [item.identity, item]));
  for (const before of base.enums.filter((item) => item.reachable)) {
    if (before.name === "Versions" || before.identity.endsWith(".Versions")) continue;
    const after = currentEnums.get(before.identity);
    const symbol = before.crossLanguageDefinitionId ?? before.identity;
    if (!after) {
      pushCandidate(candidates, facts, source, projectId, "public-surface-changed", symbol, before, undefined, "enum");
      continue;
    }
    const currentValues = new Set(after.values.map((item) => canonicalJson(item)));
    const removed = before.values.filter((item) => !currentValues.has(canonicalJson(item)));
    if (removed.length) {
      pushCandidate(
        candidates,
        facts,
        source,
        projectId,
        "enum-values-removed",
        symbol,
        before,
        after,
        "enum",
        { values: removed.map((item) => item.name ?? item.value) },
      );
    }
    if (before.isFixed !== after.isFixed || before.isUnionAsEnum !== after.isUnionAsEnum) {
      pushCandidate(candidates, facts, source, projectId, "enum-extensibility-changed", symbol, before, after, "enum");
    }
    if (before.access !== after.access || before.usage !== after.usage || before.reachable !== after.reachable) {
      pushCandidate(candidates, facts, source, projectId, "public-surface-changed", symbol, before, after, "enum");
    }
  }
}

function compareUnions(projectId, base, current, source, facts, candidates) {
  const currentUnions = new Map(current.unions.map((item) => [item.identity, item]));
  for (const before of base.unions.filter((item) => item.reachable)) {
    const after = currentUnions.get(before.identity);
    const symbol = before.crossLanguageDefinitionId ?? before.identity;
    if (!after || canonicalJson(before) !== canonicalJson(after)) {
      pushCandidate(
        candidates,
        facts,
        source,
        projectId,
        "public-surface-changed",
        symbol,
        before,
        after,
        "union",
      );
    }
  }
}

function compareClients(projectId, base, current, source, facts, candidates) {
  const currentClients = new Map(current.clients.map((item) => [item.identity, item]));
  for (const before of base.clients) {
    const after = currentClients.get(before.identity);
    if (!after) continue;
    if (before.name !== after.name || before.owner !== after.owner || before.parent !== after.parent) {
      const symbol = before.crossLanguageDefinitionId ?? before.identity;
      pushCandidate(candidates, facts, source, projectId, "client-location-changed", symbol, before, after, "client");
    }
  }
}

function compareCustomizations(projectId, source, facts, candidates) {
  const relevant = /^@(clientName|flattenProperty|clientLocation|override)\b/;
  const byDeclaration = new Map();
  for (const declaration of source.declarations) {
    const selected = (declaration.decorators ?? []).filter((item) => relevant.test(item)).sort();
    if (!selected.length) continue;
    const record = byDeclaration.get(declaration.qualifiedName) ?? {};
    record[declaration.source?.revision ?? "current"] = selected;
    record.declaration = declaration;
    byDeclaration.set(declaration.qualifiedName, record);
  }
  for (const [name, record] of byDeclaration) {
    if (canonicalJson(record.base ?? []) === canonicalJson(record.current ?? [])) continue;
    const before = { symbol: name, decorators: record.base ?? [] };
    const after = { symbol: name, decorators: record.current ?? [] };
    pushCandidate(
      candidates,
      facts,
      source,
      projectId,
      "customization-changed",
      name,
      before,
      after,
      "customization",
    );
  }
}

function factRole(fact) {
  return fact?.comparisonRole ??
    (fact?.revision === "base" ? "baseline" : fact?.revision === "current" ? "target" : undefined);
}

function referencedTypeNames(value, names = new Set(), seen = new WeakSet()) {
  if (!value || typeof value !== "object" || seen.has(value)) return names;
  seen.add(value);
  const identity = typeIdentity(value);
  if (identity) names.add(identity);
  for (const [key, child] of Object.entries(value)) {
    if (["id", "projectId", "sourceCommit", "operation", "httpOperation"].includes(key)) continue;
    if (Array.isArray(child)) child.forEach((item) => referencedTypeNames(item, names, seen));
    else referencedTypeNames(child, names, seen);
  }
  return names;
}

function buildRootCauses(candidates, facts) {
  const afterFact = (candidate) => candidate.evidenceFactIds
    .map((id) => facts[id])
    .find((fact) => factRole(fact) === "target");
  const methodCandidates = candidates.filter((candidate) => candidate.rule.startsWith("method-"));
  const typeCandidates = candidates.filter((candidate) =>
    ["model", "enum", "union"].includes(afterFact(candidate)?.factKind));
  const methodGroups = new Map();
  for (const candidate of methodCandidates) {
    const fact = afterFact(candidate);
    const response = typeIdentity(fact?.responseType) ?? typeIdentity(fact?.lro?.logicalResult);
    const key = response
      ? `${candidate.projectId ?? fact?.projectId}:${response}`
      : `${fact?.projectId}:${candidate.crossLanguageDefinitionId}`;
    const group = methodGroups.get(key) ?? [];
    group.push(candidate);
    methodGroups.set(key, group);
  }
  const roots = [];
  const coveredTypes = new Set();
  for (const [key, direct] of methodGroups) {
    const reachable = new Set();
    const queue = direct
      .map(afterFact)
      .flatMap((fact) => [fact?.responseType, fact?.lro?.logicalResult])
      .filter(Boolean);
    while (queue.length) {
      const value = queue.shift();
      const identity = typeIdentity(value);
      if (!identity || reachable.has(identity)) continue;
      reachable.add(identity);
      for (const candidate of typeCandidates) {
        const fact = afterFact(candidate);
        if (typeIdentity(fact) !== identity) continue;
        for (const reference of referencedTypeNames(fact)) {
          if (!reachable.has(reference)) queue.push({ identity: reference });
        }
      }
    }
    const propagated = typeCandidates.filter((candidate) => {
      const identity = typeIdentity(afterFact(candidate));
      return identity && reachable.has(identity);
    });
    propagated.forEach((candidate) => coveredTypes.add(candidate.id));
    const root = {
      kind: "method-return-propagation",
      directCandidateIds: unique(direct.map((item) => item.id)),
      propagatedCandidateIds: unique(propagated.map((item) => item.id)),
      operationFactIds: unique(direct.flatMap((item) => item.evidenceFactIds)),
      methodFactIds: unique(direct.flatMap((item) => item.evidenceFactIds)),
      typeFactIds: unique(propagated.flatMap((item) => item.evidenceFactIds)),
      referenceEvidence: [],
      rootKey: key,
    };
    roots.push({ id: stableId("downstream-root-cause", root), ...root });
  }
  const unresolved = typeCandidates.filter((candidate) => !coveredTypes.has(candidate.id));
  if (unresolved.length) {
    const root = {
      kind: "unresolved",
      directCandidateIds: [],
      propagatedCandidateIds: unique(unresolved.map((item) => item.id)),
      operationFactIds: [],
      methodFactIds: [],
      typeFactIds: unique(unresolved.flatMap((item) => item.evidenceFactIds)),
      referenceEvidence: [],
    };
    roots.push({ id: stableId("downstream-root-cause", root), ...root });
  }
  for (const candidate of candidates) {
    candidate.rootCauseIds = roots
      .filter((root) =>
        root.directCandidateIds.includes(candidate.id) ||
        root.propagatedCandidateIds.includes(candidate.id))
      .map((root) => root.id);
  }
  return roots.sort((left, right) => left.id.localeCompare(right.id));
}

export function analyzeDownstreamBreaking(options) {
  const { workRoot, manifest, sourceIndex } = loadInputs(options);
  const facts = {};
  const candidates = [];
  const blockers = [];
  let analyzedProjects = 0;
  for (const project of [...(manifest.projects ?? [])].sort((left, right) => left.id.localeCompare(right.id))) {
    const baseArtifact = project.artifacts?.baseline?.tcgc ?? project.artifacts?.base?.tcgc;
    const currentArtifact = project.artifacts?.target?.tcgc ?? project.artifacts?.current?.tcgc;
    if (!artifactReady(baseArtifact) || !artifactReady(currentArtifact)) {
      blockers.push({
        code: "tcgc-artifacts-unavailable",
        projectId: project.id,
        message: `Baseline and target TCGC artifacts are required for ${project.id}.`,
      });
      continue;
    }
    try {
      const base = normalizeTcgcContract({ workRoot, artifact: baseArtifact });
      const current = normalizeTcgcContract({ workRoot, artifact: currentArtifact });
      const source = evidence(project, sourceIndex);
      source.artifactComparison = project.artifactComparison;
      compareMethods(project.id, base, current, source, facts, candidates);
      compareModels(project.id, base, current, source, facts, candidates);
      compareEnums(project.id, base, current, source, facts, candidates);
      compareUnions(project.id, base, current, source, facts, candidates);
      compareClients(project.id, base, current, source, facts, candidates);
      compareCustomizations(project.id, source, facts, candidates);
      analyzedProjects += 1;
    } catch (error) {
      blockers.push({ code: "tcgc-contract-unsupported", projectId: project.id, message: error.message });
    }
  }
  const uniqueCandidates = new Map(candidates.map((item) => [item.id, item]));
  const normalizedCandidates = [...uniqueCandidates.values()]
    .sort((left, right) => left.id.localeCompare(right.id));
  const rootCauses = buildRootCauses(normalizedCandidates, facts);
  const result = {
    schemaVersion: 1,
    status: analyzedProjects ? "ready" : "blocked",
    facts: Object.fromEntries(Object.entries(facts).sort(([left], [right]) => left.localeCompare(right))),
    rootCauses,
    candidates: normalizedCandidates,
    blockers,
  };
  if (options.output) writeJson(path.resolve(options.output), result);
  return result;
}

if (isMain(import.meta.url)) {
  runMain(async () => {
    const args = parseArgs(process.argv.slice(2), { required: ["manifest", "output"] });
    const result = analyzeDownstreamBreaking(args);
    console.log(path.resolve(args.output));
    if (result.status === "blocked") process.exitCode = 1;
  });
}
