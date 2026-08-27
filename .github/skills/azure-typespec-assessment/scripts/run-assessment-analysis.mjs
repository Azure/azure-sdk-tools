#!/usr/bin/env node

import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import {
  canonicalTempDirectory,
  parseArgs,
  prepareAssessment,
} from "./prepare-assessment.mjs";
import { analyzeArtifacts } from "./analyze-artifacts.mjs";
import { prepareComplianceEvidence } from "./prepare-compliance-evidence.mjs";
import { extractVersionedMembers } from "./source-index.mjs";

function elapsedMs(startedAt) {
  return Math.round(Number(process.hrtime.bigint() - startedAt) / 1_000_000);
}

function analysisArgs(argv) {
  const prepareValues = [];
  let documentCache;
  let evidenceDirectory;
  let sourceAssessment;
  let fastMode = false;
  let modelInputBudgetBytes = 250 * 1024;
  for (let index = 0; index < argv.length; index += 1) {
    if (argv[index] === "--document-cache") {
      documentCache = argv[index + 1];
      index += 1;
    } else if (argv[index] === "--model-input-budget-bytes") {
      const value = Number(argv[index + 1]);
      if (!Number.isSafeInteger(value) || value <= 0) {
        throw new Error(
          "--model-input-budget-bytes requires a positive integer",
        );
      }
      modelInputBudgetBytes = value;
      index += 1;
    } else if (argv[index] === "--evidence-directory") {
      evidenceDirectory = argv[index + 1];
      if (!evidenceDirectory) {
        throw new Error("--evidence-directory requires a value");
      }
      index += 1;
    } else if (argv[index] === "--source-assessment") {
      sourceAssessment = argv[index + 1];
      if (!sourceAssessment) {
        throw new Error("--source-assessment requires a value");
      }
      index += 1;
    } else if (argv[index] === "--fast") {
      fastMode = true;
    } else {
      prepareValues.push(argv[index]);
    }
  }
  return {
    prepare: parseArgs(prepareValues),
    documentCache:
      documentCache ??
      join(canonicalTempDirectory(), "typespec-assessment-document-cache"),
    modelInputBudgetBytes,
    evidenceDirectory,
    sourceAssessment,
    fastMode,
  };
}

function hydrateHistoricalSourceEvidence(evidence, assessmentPath) {
  if (!assessmentPath) {
    return { ...evidence, typeSpecDiffs: evidence.typeSpecDiffs ?? [] };
  }
  const assessment = JSON.parse(readFileSync(resolve(assessmentPath), "utf8"));
  const semanticItems =
    assessment.dimensions?.semanticUnderstanding?.items ?? [];
  const changes = semanticItems.flatMap((item) => item.changes ?? []);
  const complianceFindings =
    assessment.dimensions?.azureCompliance?.findings ?? [];
  const complianceSnippets = complianceFindings.flatMap(
    (finding) => finding.codeSnippets ?? [],
  );
  const typeSpecDiffs = [
    ...new Map(
      [
        ...changes.flatMap((change) => change.typeSpecDiffs ?? []),
        ...complianceSnippets.map((snippet) => ({
          path: snippet.path,
          oldStart: snippet.startLine,
          oldCount: 0,
          newStart: snippet.startLine,
          newCount: snippet.lines.length,
          context: "",
          lines: snippet.lines.map((line) => `+${line}`),
          supplemental: true,
        })),
      ].map((hunk) => [
        `${hunk.path}:${hunk.newStart}:${hunk.newCount}:${hunk.lines.join("\n")}`,
        hunk,
      ]),
    ).values(),
  ];
  const sourceReferences = [
    ...new Map(
      [
        ...(evidence.sourceReferences ?? []),
        ...semanticItems.flatMap((item) => item.sourceReferences ?? []),
        ...changes.flatMap((change) => change.sourceReferences ?? []),
        ...complianceFindings.flatMap(
          (finding) => finding.sourceReferences ?? [],
        ),
      ].map((reference) => [
        `${reference.path}:${reference.revision}:${reference.startLine}:${reference.endLine}`,
        reference,
      ]),
    ).values(),
  ];
  return { ...evidence, typeSpecDiffs, sourceReferences };
}

export function mergeHistoricalComplianceDocuments(
  complianceEvidence,
  assessmentPath,
) {
  if (!assessmentPath) return complianceEvidence;
  const assessment = JSON.parse(readFileSync(resolve(assessmentPath), "utf8"));
  const historicalDocuments =
    assessment.dimensions?.azureCompliance?.documents ?? [];
  const documents = new Map(
    (complianceEvidence.documents ?? []).map((document) => [
      document.url,
      document,
    ]),
  );
  for (const document of historicalDocuments) {
    if (!document.url || !document.guidanceExcerpt) continue;
    const current = documents.get(document.url);
    documents.set(document.url, {
      category: current?.category ?? "Retained authoritative evidence",
      title: document.title,
      url: document.url,
      routingScore: current?.routingScore ?? 0,
      fetchedAt: current?.fetchedAt,
      contentHash: current?.contentHash,
      cache: "retained-assessment",
      section: document.section,
      matchingExcerpt: document.guidanceExcerpt,
      candidateCodeBlocks: (document.expectedCodeSnippets ?? []).map(
        (snippet) => snippet.lines.join("\n"),
      ),
    });
  }
  return {
    ...complianceEvidence,
    documents: [...documents.values()],
  };
}

function operationChangeRecord(change) {
  const operation = change.after ?? change.before;
  const compactChanges = compactAspectChanges(change);
  return {
    id: `${change.kind}-${operation.key
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-|-$/g, "")}`,
    kind: change.kind,
    operationKey: operation.key,
    operationId: operation.operationId,
    apiVersion: operation.apiVersion,
    changedAspects: Object.keys(compactChanges.aspects),
    aspectChanges: compactChanges.aspects,
    reviewRequired: true,
  };
}

function operationFamily(operationId) {
  return operationId?.split("_", 1)[0] ?? "unknown";
}

const operationComparisonFields = [
  "operationId",
  "method",
  "path",
  "parameters",
  "request",
  "responses",
  "lro",
  "paging",
];

function changedOperationAspects(before, after) {
  return operationComparisonFields.filter(
    (field) =>
      JSON.stringify(comparableOperationField(field, before[field])) !==
      JSON.stringify(comparableOperationField(field, after[field])),
  );
}

function normalizeContractReferences(value, key) {
  if (Array.isArray(value)) {
    return value.map((item) => normalizeContractReferences(item));
  }
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value)
        .filter(([entryKey]) => entryKey !== "sourceArtifact")
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([entryKey, entryValue]) => [
          entryKey,
          normalizeContractReferences(entryValue, entryKey),
        ]),
    );
  }
  if (key === "reference" && typeof value === "string") {
    const fragment = value.indexOf("#");
    return fragment >= 0 ? value.slice(fragment) : value.split(/[\\/]/).at(-1);
  }
  return value;
}

function comparableOperationField(field, value) {
  if (field !== "parameters") {
    return normalizeContractReferences(value);
  }
  return normalizeContractReferences(
    (value ?? []).map((parameter) => {
      if (parameter.name.toLowerCase() !== "api-version") return parameter;
      const comparable = structuredClone(parameter);
      delete comparable.default;
      if (comparable.contract) {
        delete comparable.contract.enum;
        delete comparable.contract.default;
      }
      return comparable;
    }),
  );
}

function latestVersionPredecessor(operation, baseline) {
  return baseline
    .filter(
      (candidate) =>
        candidate.method === operation.method &&
        candidate.path === operation.path &&
        candidate.apiVersion.localeCompare(operation.apiVersion, "en") < 0,
    )
    .sort((left, right) =>
      right.apiVersion.localeCompare(left.apiVersion, "en"),
    )[0];
}

function summarizeRequest(request) {
  if (!request) return null;
  return {
    required: request.required,
    schemas: request.content?.map((content) => content.schema).filter(Boolean),
  };
}

function summarizeResponses(responses) {
  return responses.map((response) => ({
    status: response.status,
    schemas: response.content?.map((content) => content.schema).filter(Boolean),
    headers: response.headers?.map((header) => header.name),
  }));
}

function summarizeParameters(parameters) {
  return parameters.map((parameter) => ({
    in: parameter.in,
    name: parameter.name,
    required: parameter.required,
    type: parameter.type,
  }));
}

function compactAspectChanges(change) {
  const aspectChanges = {};
  for (const aspect of change.aspects ?? []) {
    const field = typeof aspect === "string" ? aspect : aspect.field;
    let before = change.before?.[field];
    let after = change.after?.[field];
    if (field === "request") {
      before = summarizeRequest(before);
      after = summarizeRequest(after);
    } else if (field === "responses") {
      before = summarizeResponses(before ?? []);
      after = summarizeResponses(after ?? []);
    } else if (field === "parameters") {
      before = summarizeParameters(before ?? []);
      after = summarizeParameters(after ?? []);
    }
    aspectChanges[field] = { before, after };
  }
  return {
    operationId: change.after?.operationId ?? change.before?.operationId,
    aspects: aspectChanges,
  };
}

function materialAspectChanges(change) {
  const compact = compactAspectChanges(change);
  compact.aspects = Object.fromEntries(
    Object.entries(compact.aspects).filter(
      ([, value]) =>
        JSON.stringify(value.before) !== JSON.stringify(value.after),
    ),
  );
  return compact;
}

function compactRestCandidate(candidate, index) {
  const evidence = candidate.evidence ?? {};
  let compactEvidence = evidence;
  if (candidate.rule === "parameter-contract-changed") {
    compactEvidence = {
      operation: evidence.operation,
      parameters: (evidence.parameters ?? []).map((parameter) => ({
        before: summarizeParameters([parameter.before])[0],
        after: summarizeParameters([parameter.after])[0],
      })),
    };
  } else if (candidate.rule === "request-contract-changed") {
    compactEvidence = {
      operation: evidence.operation,
      before: summarizeRequest(evidence.before),
      after: summarizeRequest(evidence.after),
    };
  } else if (candidate.rule === "response-contract-changed") {
    compactEvidence = {
      operation: evidence.operation,
      before: summarizeResponses(evidence.before ?? []),
      after: summarizeResponses(evidence.after ?? []),
    };
  }
  const operation = compactEvidence.operation ?? `candidate-${index + 1}`;
  return {
    id: `rest-${index + 1}-${candidate.rule}-${String(operation)
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-|-$/g, "")}`,
    rule: candidate.rule,
    severity: candidate.severity,
    summary: candidate.summary,
    evidence: compactEvidence,
    reviewRequired: candidate.reviewRequired,
  };
}

function relevantOperationChanges(changes, baseline) {
  return changes.flatMap((change) => {
    if (change.kind !== "added") return [change];
    const predecessor = latestVersionPredecessor(change.after, baseline);
    if (!predecessor) return [change];
    const aspects = changedOperationAspects(predecessor, change.after);
    if (aspects.length === 0) return [];
    return [
      {
        ...change,
        kind: "version-modified",
        before: predecessor,
        aspects,
      },
    ];
  });
}

function groupOperationManifest(changes, baseline) {
  const groups = new Map();
  for (const change of relevantOperationChanges(changes, baseline)) {
    if (change.kind === "modified") continue;
    const operation = change.after ?? change.before;
    const family = operationFamily(operation.operationId);
    const compactChange =
      change.kind === "version-modified"
        ? materialAspectChanges(change)
        : undefined;
    const behavior =
      change.kind === "version-modified" &&
      Object.keys(compactChange.aspects).length === 0
        ? "version-propagation"
        : change.kind === "version-modified"
          ? "material-change"
          : change.kind;
    const key = `${change.kind}:${operation.apiVersion}:${family}:${behavior}`;
    const group = groups.get(key) ?? {
      id: key.toLowerCase().replace(/[^a-z0-9]+/g, "-"),
      kind: change.kind,
      behavior,
      apiVersion: operation.apiVersion,
      family,
      operationIds: [],
      changedAspects: new Set(),
      changes: [],
    };
    group.operationIds.push(operation.operationId);
    if (compactChange) {
      for (const aspect of Object.keys(compactChange.aspects)) {
        group.changedAspects.add(aspect);
      }
      if (Object.keys(compactChange.aspects).length > 0) {
        group.changes.push(compactChange);
      }
    }
    groups.set(key, group);
  }
  return [...groups.values()]
    .map((group) => ({
      ...group,
      operationIds: [...new Set(group.operationIds)].sort(),
      changedAspects:
        group.changedAspects.size > 0
          ? [...group.changedAspects].sort()
          : undefined,
      changes: group.changes.length > 0 ? group.changes : undefined,
    }))
    .sort((left, right) => left.id.localeCompare(right.id));
}

function singularFamily(family) {
  const normalized = family.toLowerCase().replace(/[^a-z0-9]/g, "");
  if (normalized.endsWith("ies")) return `${normalized.slice(0, -3)}y`;
  if (normalized.endsWith("sses")) return normalized.slice(0, -2);
  return normalized.endsWith("s") ? normalized.slice(0, -1) : normalized;
}

function normalizedOwner(owner) {
  return owner
    ?.toLowerCase()
    .replace(/[^a-z0-9]/g, "")
    .replace(
      /(propertiesformat|properties|listresult|result|request|response)$/,
      "",
    );
}

function linkOperationGroupsToSource(projects, sourceFiles) {
  const members = sourceFiles.flatMap((file) =>
    file.versionedMembers.map((member) => ({ path: file.path, ...member })),
  );
  return projects.map((project) => ({
    ...project,
    rest: {
      ...project.rest,
      operationGroups: project.rest.operationGroups.map((group) => {
        const family = singularFamily(group.family);
        const memberLinks = members
          .filter((member) => {
            const owner = normalizedOwner(member.owner ?? member.symbol);
            return (
              member.version.replace(/^v/, "").replaceAll("_", "-") ===
                group.apiVersion &&
              owner &&
              owner === family
            );
          })
          .map(({ path, owner, symbol, sourceChangeId }) => ({
            path,
            owner: owner ?? symbol,
            sourceChangeIds: [sourceChangeId],
          }));
        const operationFileLinks = sourceFiles
          .filter((file) => {
            const stem = file.path
              .split("/")
              .at(-1)
              .replace(/\.tsp$/i, "")
              .toLowerCase()
              .replace(/[^a-z0-9]/g, "");
            return stem === family;
          })
          .map((file) => ({
            path: file.path,
            sourceChangeIds: file.changes.map((change) => change.id),
          }));
        const linksByPath = new Map();
        for (const link of [...memberLinks, ...operationFileLinks]) {
          const linked = linksByPath.get(link.path) ?? {
            owners: new Set(),
            sourceChangeIds: new Set(),
          };
          if (link.owner) linked.owners.add(link.owner);
          for (const id of link.sourceChangeIds ?? []) {
            if (id) linked.sourceChangeIds.add(id);
          }
          linksByPath.set(link.path, linked);
        }
        const sourceLinks = [...linksByPath]
          .map(([path, linked]) => ({
            path,
            owners:
              linked.owners.size > 0
                ? [...linked.owners].sort()
                : undefined,
            sourceChangeIds: [...linked.sourceChangeIds].sort(),
          }))
          .sort((left, right) => left.path.localeCompare(right.path));
        return {
          ...group,
          sourceLinks: sourceLinks.length > 0 ? sourceLinks : undefined,
        };
      }),
    },
  }));
}

function reviewUnitId(parts) {
  return parts
    .filter(Boolean)
    .join("-")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

function sourceOnlyBehavior(change) {
  const lines = change.diffLines.map((line) => line.text.trim());
  if (
    lines.some((line) =>
      /^[A-Za-z_][A-Za-z0-9_]*\s*:\s*["'][^"']+["']\s*,?$/.test(line),
    )
  ) {
    return "enum-values";
  }
  if (
    lines.some(
      (line) =>
        /\b(model|scalar|union|enum)\s+[A-Za-z_]/.test(line) ||
        /^[A-Za-z_][A-Za-z0-9_]*\??\s*:/.test(line) ||
        line.startsWith("@Xml."),
    )
  ) {
    return "model-shape";
  }
  if (
    lines.some(
      (line) =>
        /\b(interface|op)\s+[A-Za-z_]/.test(line) ||
        /^@(get|put|post|patch|delete|route|action|list)\b/.test(line),
    )
  ) {
    return "operation-shape";
  }
  return "versioning-metadata";
}

function sourceEvidenceForFamily(sourceFiles, projectPath, family) {
  const normalizedFamily = singularFamily(family);
  const projectFiles = sourceFiles.filter(
    (file) =>
      projectPath === "." ||
      file.path === projectPath ||
      file.path.startsWith(`${projectPath.replaceAll("\\", "/")}/`),
  );
  let matches = projectFiles.filter((file) => {
    const stem = file.path
      .split("/")
      .at(-1)
      .replace(/\.tsp$/i, "")
      .toLowerCase()
      .replace(/[^a-z0-9]/g, "");
    return stem === normalizedFamily;
  });
  const memberMatches = projectFiles
    .flatMap((file) =>
      file.versionedMembers
        .filter(
          (member) =>
            normalizedOwner(member.owner ?? member.symbol) ===
            normalizedFamily,
        )
        .map((member) => ({
          path: file.path,
          sourceChangeId: member.sourceChangeId,
        })),
    );
  return {
    sourceChangeIds: [
      ...new Set(
        [
          ...matches.flatMap((file) =>
            file.changes.map((change) => change.id),
          ),
          ...memberMatches.map((member) => member.sourceChangeId),
        ].filter(Boolean),
      ),
    ],
    sourcePaths: [
      ...new Set([
        ...matches.map((file) => file.path),
        ...memberMatches.map((member) => member.path),
      ]),
    ],
  };
}

export function buildSemanticReviewUnits(projects, sourceFiles) {
  const units = [];
  const propagation = new Map();
  for (const project of projects) {
    let hasOperationCoverage = project.rest.operationChanges.length > 0;
    const materialUnits = new Map();
    for (const change of project.rest.operationChanges) {
      const family = operationFamily(change.operationId);
      const source = sourceEvidenceForFamily(
        sourceFiles,
        project.path,
        family,
      );
      if (source.sourceChangeIds.length === 0) continue;
      const behavior = change.changedAspects.join("+");
      const key = `${change.apiVersion}:${family}:${behavior}`;
      const unit = materialUnits.get(key) ?? {
        id: reviewUnitId([
          "intent",
          project.path,
          change.apiVersion,
          family,
          behavior,
        ]),
        kind: "material-change",
        family,
        behavior,
        operationChangeIds: [],
        operationGroupIds: [],
        sourceChangeIds: [],
        sourcePaths: [],
      };
      unit.operationChangeIds.push(change.id);
      unit.sourceChangeIds.push(...source.sourceChangeIds);
      unit.sourcePaths.push(...source.sourcePaths);
      materialUnits.set(key, unit);
    }
    units.push(...materialUnits.values());
    for (const group of project.rest.operationGroups) {
      hasOperationCoverage = true;
      const sourceChangeIds = [
        ...new Set(
          (group.sourceLinks ?? []).flatMap(
            (link) => link.sourceChangeIds ?? [],
          ),
        ),
      ];
      const sourcePaths = [
        ...new Set((group.sourceLinks ?? []).map((link) => link.path)),
      ];
      if (group.behavior === "version-propagation") {
        const projectFamilyRoot = project.path.split("/").slice(0, -1).join("/");
        const key = `${projectFamilyRoot}:${group.apiVersion}`;
        const unit = propagation.get(key) ?? {
          id: reviewUnitId([
            "intent",
            projectFamilyRoot,
            group.apiVersion,
            "version-lineage",
          ]),
          kind: "version-propagation",
          family: "version-lineage",
          behavior: "publish unchanged contracts in the new API version",
          operationChangeIds: [],
          operationGroupIds: [],
          sourceChangeIds: [],
          sourcePaths: [],
        };
        unit.operationGroupIds.push(group.id);
        unit.sourceChangeIds.push(...sourceChangeIds);
        unit.sourcePaths.push(...sourcePaths);
        propagation.set(key, unit);
        continue;
      }
      units.push({
        id: reviewUnitId([
          "intent",
          project.path,
          group.apiVersion,
          group.family,
          group.behavior,
        ]),
        kind:
          group.behavior === "material-change"
            ? "material-change"
            : group.kind,
        family: group.family,
        behavior:
          group.behavior === "material-change"
            ? (group.changedAspects ?? []).join("+")
            : group.behavior,
        operationChangeIds: [],
        operationGroupIds: [group.id],
        sourceChangeIds,
        sourcePaths,
      });
    }
    if (!hasOperationCoverage) {
      const projectFiles = sourceFiles.filter(
        (file) =>
          project.path === "." ||
          file.path === project.path ||
          file.path.startsWith(`${project.path.replaceAll("\\", "/")}/`),
      );
      for (const file of projectFiles.filter(
        (candidate) => candidate.changes.length > 0,
      )) {
        const addedDependencyVersions = file.changes.flatMap((change) =>
          change.diffLines
            .filter((line) => line.kind === "add")
            .flatMap((line) => [
              ...line.text.matchAll(/@useDependency\([^)]*\.v(\d{4}_\d{2}_\d{2})\)/g),
            ])
            .map((match) => match[1].replaceAll("_", "-")),
        );
        const onlyDependencyChanges = file.changes.every((change) =>
          change.diffLines.every((line) =>
            line.text.trim().startsWith("@useDependency("),
          ),
        );
        if (onlyDependencyChanges && addedDependencyVersions.length === 1) {
          const apiVersion = addedDependencyVersions[0];
          const projectFamilyRoot = project.path
            .split("/")
            .slice(0, -1)
            .join("/");
          const key = `${projectFamilyRoot}:${apiVersion}`;
          const unit = propagation.get(key) ?? {
            id: reviewUnitId([
              "intent",
              projectFamilyRoot,
              apiVersion,
              "version-lineage",
            ]),
            kind: "version-propagation",
            family: "version-lineage",
            behavior: "publish unchanged contracts in the new API version",
            operationChangeIds: [],
            operationGroupIds: [],
            sourceChangeIds: [],
            sourcePaths: [],
          };
          unit.sourceChangeIds.push(
            ...file.changes.map((change) => change.id),
          );
          unit.sourcePaths.push(file.path);
          propagation.set(key, unit);
          continue;
        }
        const behaviorChanges = new Map();
        for (const change of file.changes) {
          const behavior = sourceOnlyBehavior(change);
          const changes = behaviorChanges.get(behavior) ?? [];
          changes.push(change);
          behaviorChanges.set(behavior, changes);
        }
        if (
          behaviorChanges.has("versioning-metadata") &&
          behaviorChanges.size > 1
        ) {
          const target = [...behaviorChanges]
            .filter(([behavior]) => behavior !== "versioning-metadata")
            .sort(
              (left, right) =>
                right[1].length - left[1].length ||
                Number(right[0] === "model-shape") -
                  Number(left[0] === "model-shape"),
            )[0];
          target[1].push(...behaviorChanges.get("versioning-metadata"));
          behaviorChanges.delete("versioning-metadata");
        }
        const candidateRules = [
          ...new Set(
            project.downstream.candidates
              .filter((candidate) =>
                (candidate.evidence ?? []).some(
                  (entry) => entry.path === file.path,
                ),
              )
              .map((candidate) => candidate.rule),
          ),
        ];
        const family = file.path
          .split("/")
          .at(-1)
          .replace(/\.tsp$/i, "");
        for (const [sourceBehavior, changes] of behaviorChanges) {
          const behavior =
            candidateRules.length > 0
              ? [...new Set([...candidateRules, sourceBehavior])].join("+")
              : sourceBehavior;
          units.push({
            id: reviewUnitId([
              "intent",
              project.path,
              family,
              behavior,
              "source-change",
            ]),
            kind: "source-change",
            family,
            behavior,
            operationChangeIds: [],
            operationGroupIds: [],
            sourceChangeIds: changes.map((change) => change.id),
            sourcePaths: [file.path],
          });
        }
      }
    }
  }
  units.push(...propagation.values());
  const reviewUnits = units
    .map((unit) => ({
      ...unit,
      operationGroupIds: [...new Set(unit.operationGroupIds)].sort(),
      sourceChangeIds: [...new Set(unit.sourceChangeIds)].sort(),
      sourcePaths: [...new Set(unit.sourcePaths)].sort(),
    }))
    .sort((left, right) => left.id.localeCompare(right.id));
  const missingSource = reviewUnits.filter(
    (unit) =>
      unit.kind !== "version-propagation" &&
      unit.sourceChangeIds.length === 0,
  );
  if (missingSource.length > 0) {
    throw new Error(
      `Material semantic review unit(s) have no changed TypeSpec source evidence: ${missingSource
        .map((unit) => unit.id)
        .join(", ")}.`,
    );
  }
  return reviewUnits;
}

export function buildComplianceReviewItems(
  sourceFiles,
  documents,
  semanticReviewUnits,
) {
  const relevantChangeIds = new Set(
    semanticReviewUnits
      .filter((unit) => unit.kind !== "version-propagation")
      .flatMap((unit) => unit.sourceChangeIds),
  );
  const changes = sourceFiles.flatMap((file) =>
    file.changes
      .filter((change) => relevantChangeIds.has(change.id))
      .map((change) => ({
        id: change.id,
        path: file.path,
      })),
  );
  return documents.flatMap((document, documentIndex) =>
    changes.map((change, changeIndex) => ({
      id: `compliance-${documentIndex + 1}-${changeIndex + 1}`,
      documentationUrl: document.url,
      sourceChangeId: change.id,
      sourcePath: change.path,
    })),
  );
}

function sourceDownstreamCandidates(sourceFiles, projectPath, typeSpecDiffs) {
  const pagingDecorators = sourceFiles.flatMap((file) => {
    if (
      projectPath !== "." &&
      file.path !== projectPath &&
      !file.path.startsWith(`${projectPath.replaceAll("\\", "/")}/`)
    ) {
      return [];
    }
    return file.decorators
      .filter(
        (decorator) =>
          decorator.change === "added" &&
          ["@list", "@pageItems", "@nextLink"].includes(decorator.symbol),
      )
      .map((decorator) => ({
        path: file.path,
        symbol: decorator.symbol,
        count: decorator.count,
      }));
  });
  const candidates = [];
  if (pagingDecorators.length > 0) {
    candidates.push({
      id: "source-paging-metadata-added",
      rule: "paging-metadata-added",
      severity: "medium",
      summary:
        "Added paging metadata can change generated SDK return and iteration shapes while preserving the REST wire contract.",
      evidence: pagingDecorators,
      reviewRequired: true,
    });
  }
  const projectFiles = sourceFiles.filter(
    (file) =>
      projectPath === "." ||
      file.path === projectPath ||
      file.path.startsWith(`${projectPath.replaceAll("\\", "/")}/`),
  );
  const changedSource = typeSpecDiffs
    .filter(
      (hunk) =>
        projectPath === "." ||
        hunk.path === projectPath ||
        hunk.path.startsWith(`${projectPath.replaceAll("\\", "/")}/`),
    )
    .flatMap((hunk) => hunk.lines.map((line) => line.slice(1)))
    .join("\n");
  const declarations = projectFiles.flatMap((file) =>
    file.declarations.map((declaration) => ({
      path: file.path,
      ...declaration,
    })),
  );
  if (
    /flattenProperty/.test(changedSource) &&
    /flattenProperty\(["']!javascript["']\)/.test(changedSource)
  ) {
    candidates.push({
      id: "source-javascript-flattening-scope-changed",
      rule: "client-property-flattening-changed",
      severity: "high",
      summary:
        "Changing flattenProperty to exclude JavaScript can change generated JavaScript construction and property access from flattened to nested.",
      evidence: projectFiles
        .filter((file) =>
          file.changes.some((change) =>
            change.lines.some((line) => line.includes("flattenProperty")),
          ),
        )
        .map((file) => ({ path: file.path })),
      reviewRequired: true,
    });
  }
  if (
    /@@clientLocation/.test(changedSource) &&
    /!csharp,!go/.test(changedSource)
  ) {
    candidates.push({
      id: "source-go-client-location-changed",
      rule: "client-location-changed",
      severity: "high",
      summary:
        "Expanding a clientLocation exclusion from C# to C# and Go can move existing Go methods between generated clients.",
      evidence: projectFiles
        .filter((file) =>
          file.changes.some((change) =>
            change.lines.some((line) => line.includes("@@clientLocation")),
          ),
        )
        .map((file) => ({ path: file.path })),
      reviewRequired: true,
    });
  }
  const removedEnums = new Set(
    declarations
      .filter(
        (declaration) =>
          declaration.kind === "enum" && declaration.change === "removed",
      )
      .map((declaration) => declaration.symbol),
  );
  const openedEnums = declarations.filter(
    (declaration) =>
      declaration.kind === "union" &&
      declaration.change === "added" &&
      removedEnums.has(declaration.symbol),
  );
  if (openedEnums.length > 0) {
    candidates.push({
      id: "source-enum-replaced-by-open-union",
      rule: "sdk-enum-shape-changed",
      severity: "high",
      summary:
        "Replacing an enum with a string-backed union can change generated enum shape and member identities while preserving wire values.",
      evidence: openedEnums.map(({ path, symbol }) => ({ path, symbol })),
      reviewRequired: true,
    });
  }
  const removedAsyncActions = new Set(
    typeSpecDiffs
      .filter(
        (hunk) =>
          projectPath === "." ||
          hunk.path === projectPath ||
          hunk.path.startsWith(`${projectPath.replaceAll("\\", "/")}/`),
      )
      .flatMap((hunk) => hunk.lines)
      .flatMap((line) => {
        const match = line.match(
          /^-\s*([A-Za-z_]\w*)\s+is\s+ArmResourceActionAsync(?:Base)?</,
        );
        return match ? [match[1]] : [];
      }),
  );
  const synchronousReplacements = [
    ...new Set(
      typeSpecDiffs
        .filter(
          (hunk) =>
            projectPath === "." ||
            hunk.path === projectPath ||
            hunk.path.startsWith(`${projectPath.replaceAll("\\", "/")}/`),
        )
        .flatMap((hunk) => hunk.lines)
        .flatMap((line) => {
          const match = line.match(
            /^\+\s*([A-Za-z_]\w*)\s+is\s+ArmResourceActionSync</,
          );
          return match && removedAsyncActions.has(match[1]) ? [match[1]] : [];
        }),
    ),
  ];
  if (synchronousReplacements.length > 0) {
    candidates.push({
      id: "source-arm-action-changed-from-async-to-sync",
      rule: "sdk-lro-to-synchronous",
      severity: "high",
      summary:
        "Replacing existing asynchronous ARM actions with synchronous actions can change generated SDK methods from pollers to immediate-return calls.",
      evidence: projectFiles
        .filter((file) =>
          file.changes.some((change) =>
            change.lines.some((line) =>
              synchronousReplacements.includes(
                line.match(/^([A-Za-z_]\w*)\s+is\s+ArmResourceActionSync</)?.[1],
              ),
            ),
          ),
        )
        .map((file) => ({
          path: file.path,
          symbols: synchronousReplacements,
        })),
      reviewRequired: true,
    });
  }
  return candidates;
}

const sourceLineLimitPerFile = 200;

function compactSourceHunk(hunk, limit, sourceReferences = []) {
  const significantLines = hunk.lines.flatMap((rawLine, index) => {
    if (!/^[+-]/.test(rawLine)) return [];
    const line = rawLine.slice(1).trim();
    if (
      !line ||
      line.startsWith("//") ||
      line.startsWith("/**") ||
      line.startsWith("*") ||
      line.startsWith("import ") ||
      line.startsWith("using ") ||
      ["{", "}", "};"].includes(line) ||
      !/^(?:#suppress\b|@|model\b|interface\b|union\b|enum\b|alias\b|op\b|namespace\b|scalar\b|[A-Za-z_]\w*\??\s*(?::|is\s+ArmResourceAction))/.test(
        line,
      )
    ) {
      return [];
    }
    const declaration = /^(?:model|interface|union|enum|alias|op)\b/.test(line);
    const followsCompliancePattern =
      declaration &&
      /customAzureResource|parentResource|RoutedOperations|arm-custom-resource/.test(
        hunk.lines
          .slice(Math.max(0, index - 8), index)
          .map((nearbyLine) => nearbyLine.slice(1))
          .join("\n"),
      );
    const priority = followsCompliancePattern
      ? 5
      : /#suppress\b|Legacy\.|customAzureResource|parentResource|RoutedOperations|@route\b/.test(
            line,
          )
        ? 4
        : declaration
          ? 3
          : /@added\b/.test(line)
            ? 2
            : 1;
    return [
      {
        index,
        line,
        kind: rawLine.startsWith("+") ? "add" : "remove",
        priority,
      },
    ];
  });
  const uniqueSignificantLines = [
    ...new Map(
      significantLines.map((entry) => [
        `${entry.kind}:${entry.line}`,
        entry,
      ]),
    ).values(),
  ];
  const selectedLines = [...uniqueSignificantLines]
    .sort(
      (left, right) =>
        right.priority - left.priority || left.index - right.index,
    )
    .slice(0, limit)
    .sort((left, right) => left.index - right.index);
  const revision = hunk.newCount === 0 ? "base" : "head";
  const startLine = revision === "base" ? hunk.oldStart : hunk.newStart;
  const lineCount = revision === "base" ? hunk.oldCount : hunk.newCount;
  const endLine = Math.max(startLine, startLine + lineCount - 1);
  const sourceReference = sourceReferences.find(
    (reference) =>
      reference.path === hunk.path &&
      reference.revision === revision &&
      reference.startLine <= endLine &&
      reference.endLine >= startLine,
  );
  const sourceLink = sourceReference?.link.replace(
    /#L\d+-L\d+$/,
    `#L${startLine}-L${endLine}`,
  );
  return {
    id: `${hunk.path}:${hunk.newStart}:${hunk.oldStart}`,
    path: hunk.path,
    revision,
    oldStart: hunk.oldStart,
    oldCount: hunk.oldCount,
    newStart: hunk.newStart,
    newCount: hunk.newCount,
    lines: selectedLines.map((entry) => entry.line),
    diffLines: selectedLines.map(({ kind, line }) => ({
      kind,
      text: line,
    })),
    ...(sourceLink ? { sourceLink } : {}),
    omittedLineCount: Math.max(0, uniqueSignificantLines.length - limit),
  };
}

function compactSourceFiles(sourceIndex, typeSpecDiffs, sourceReferences = []) {
  const files = new Map();
  const hunkCounts = new Map();
  for (const hunk of typeSpecDiffs) {
    hunkCounts.set(hunk.path, (hunkCounts.get(hunk.path) ?? 0) + 1);
  }
  for (const member of extractVersionedMembers(typeSpecDiffs)) {
    const file = files.get(member.path) ?? {
      path: member.path,
      declarations: [],
      decoratorCounts: new Map(),
      versionedMembers: [],
      changes: [],
    };
    file.versionedMembers.push({
      owner: member.owner,
      symbol: member.symbol,
      version: member.version,
      sourceChangeId: member.sourceChangeId,
    });
    files.set(member.path, file);
  }
  for (const entry of sourceIndex) {
    const file = files.get(entry.path) ?? {
      path: entry.path,
      declarations: [],
      decoratorCounts: new Map(),
      versionedMembers: [],
      changes: [],
    };
    if (entry.kind === "decorator") {
      const key = `${entry.change}:${entry.symbol}`;
      const decorator = file.decoratorCounts.get(key) ?? {
        symbol: entry.symbol,
        change: entry.change,
        count: 0,
      };
      decorator.count += 1;
      file.decoratorCounts.set(key, decorator);
    } else {
      file.declarations.push({
        symbol: entry.symbol,
        kind: entry.kind,
        change: entry.change,
        revision: entry.revision,
        line: entry.line,
      });
    }
    files.set(entry.path, file);
  }
  for (const hunk of typeSpecDiffs) {
    const file = files.get(hunk.path) ?? {
      path: hunk.path,
      declarations: [],
      decoratorCounts: new Map(),
      versionedMembers: [],
      changes: [],
    };
    file.changes.push(
      compactSourceHunk(
        hunk,
        Math.max(
          1,
          Math.floor(sourceLineLimitPerFile / (hunkCounts.get(hunk.path) ?? 1)),
        ),
        sourceReferences,
      ),
    );
    files.set(hunk.path, file);
  }
  return [...files.values()]
    .map((file) => ({
      path: file.path,
      declarations: file.declarations,
      versionedMembers: file.versionedMembers,
      decorators: [...file.decoratorCounts.values()].sort((left, right) =>
        `${left.change}:${left.symbol}`.localeCompare(
          `${right.change}:${right.symbol}`,
        ),
      ),
      changes: file.changes.filter(
        (change) => change.lines.length > 0 || change.omittedLineCount > 0,
      ),
    }))
    .filter(
      (file) =>
        file.declarations.length > 0 ||
        file.versionedMembers.length > 0 ||
        file.decorators.length > 0 ||
        file.changes.length > 0,
    )
    .sort((left, right) => left.path.localeCompare(right.path));
}

export function compactAnalysisProject(project) {
  const detailedChanges = project.rest.changes
    .filter((change) => change.kind === "modified")
    .map((change) => ({
      ...change,
      aspects: changedOperationAspects(change.before, change.after),
    }))
    .filter((change) => change.aspects.length > 0);
  const operationGroups = groupOperationManifest(
    project.rest.changes,
    project.rest.baseline,
  );
  const detailedOperationKeys = new Set(
    detailedChanges.flatMap((change) => [
      change.before?.key,
      change.after?.key,
    ]),
  );
  const breakingCandidates = project.rest.restBreakingCandidates
    .filter(
      (candidate) =>
        candidate.rule === "operation-removed" ||
        !candidate.evidence?.operation ||
        detailedOperationKeys.has(candidate.evidence.operation),
    )
    .map(compactRestCandidate);
  const downstreamCandidates = [...project.downstream.candidates];
  if (
    breakingCandidates.length > 0 &&
    !downstreamCandidates.some(
      (candidate) => candidate.id === "derived-rest-contract-sdk-impact",
    )
  ) {
    downstreamCandidates.push({
      id: "derived-rest-contract-sdk-impact",
      rule: "rest-contract-sdk-impact",
      severity: "high",
      summary:
        "Confirmed REST contract changes can require generated SDK signature, serialization, or response-shape changes.",
      evidence: breakingCandidates.map((candidate) => ({
        candidateId: candidate.id,
        operation: candidate.evidence?.operation,
      })),
      reviewRequired: true,
    });
  }
  return {
    path: project.path,
    rest: {
      operationChanges: detailedChanges.map(operationChangeRecord),
      operationGroups,
      breakingCandidates,
      counts: {
        baselineOperations: project.rest.baseline.length,
        headOperations: project.rest.head.length,
        changedOperations: project.rest.changes.length,
        modelRelevantOperations:
          detailedChanges.length +
          operationGroups.reduce(
            (count, group) => count + group.operationIds.length,
            0,
          ),
      },
    },
    downstream: {
      candidates: downstreamCandidates,
      candidateCount: downstreamCandidates.length,
    },
  };
}

export function buildAssessmentDraft({
  evidence,
  analysis,
  complianceEvidence,
  totalMs,
}) {
  const sourceFiles = compactSourceFiles(
    analysis.sourceIndex,
    evidence.typeSpecDiffs,
    evidence.sourceReferences,
  );
  const projects = linkOperationGroupsToSource(
    analysis.projects.map((project) => compactAnalysisProject(project)),
    sourceFiles,
  ).map((project) => {
    const sourceCandidates = sourceDownstreamCandidates(
      sourceFiles,
      project.path,
      evidence.typeSpecDiffs,
    );
    const operationChanges = project.rest.operationChanges.filter((change) => {
      const source = sourceEvidenceForFamily(
        sourceFiles,
        project.path,
        operationFamily(change.operationId),
      );
      return source.sourceChangeIds.length > 0;
    });
    const breakingCandidates =
      operationChanges.length === 0 &&
      project.rest.operationGroups.length === 0
        ? []
        : project.rest.breakingCandidates;
    return {
      ...project,
      rest: {
        ...project.rest,
        operationChanges,
        breakingCandidates,
        candidateCount: breakingCandidates.length,
        counts: {
          ...project.rest.counts,
          modelRelevantOperations:
            operationChanges.length + project.rest.operationGroups.length,
        },
      },
      downstream: {
        ...project.downstream,
        candidates: [...project.downstream.candidates, ...sourceCandidates],
        candidateCount:
          project.downstream.candidateCount + sourceCandidates.length,
      },
    };
  });
  const semanticReviewUnits = buildSemanticReviewUnits(projects, sourceFiles);
  for (const unit of semanticReviewUnits) {
    if (
      unit.kind !== "version-propagation" &&
      unit.sourceChangeIds.length === 0
    ) {
      throw new Error(
        `Semantic review unit ${unit.id} has no changed TypeSpec source evidence.`,
      );
    }
  }
  const boundedComplianceEvidence = {
    ...complianceEvidence,
    reviewItems: buildComplianceReviewItems(
      sourceFiles,
      complianceEvidence.documents ?? [],
      semanticReviewUnits,
    ),
  };
  return {
    schemaVersion: 1,
    comparison: {
      kind: evidence.head.hasWorkingTreeChanges
        ? "local-working-tree"
        : "committed-range",
      baseline: evidence.baseline,
      head: evidence.head,
      includedChanges: evidence.head.changeScope,
    },
    baseline: evidence.baseline,
    head: evidence.head,
    changedFiles: evidence.changedFiles.filter(
      (path) =>
        path.endsWith(".tsp") ||
        path.endsWith("tspconfig.yaml") ||
        path.endsWith("package.json"),
    ),
    sourceFiles,
    semanticReviewUnits,
    projects,
    complianceEvidence: boundedComplianceEvidence,
    artifactCache: evidence.artifactCache,
    checkoutCache: evidence.checkoutCache,
    errors: evidence.errors,
    assessmentDuration: {
      preparationMs: evidence.durationMs,
      deterministicAnalysisMs:
        evidence.phaseDurations?.deterministicAnalysisMs ?? analysis.durationMs,
      documentationEvidenceMs: boundedComplianceEvidence.durationMs,
      totalMs,
    },
    modelTasks: [
      "Group deterministic changes into semantic intents.",
      "Review REST and downstream candidates and author only supported final findings.",
      "Compare compliance excerpts with exact TypeSpec declarations and decide applicability.",
      "Set final confidence and concise service-behavior explanations.",
    ],
  };
}

export function buildFastAssessmentDraft(draft) {
  return {
    schemaVersion: 1,
    mode: "impact-only",
    comparison: draft.comparison,
    baseline: draft.baseline,
    head: draft.head,
    changedFiles: draft.changedFiles,
    sourceFiles: draft.sourceFiles,
    projects: draft.projects.map((project) => ({
      path: project.path,
      rest: {
        operationChanges: project.rest.operationChanges,
        operationGroups: project.rest.operationGroups,
        breakingCandidates: project.rest.breakingCandidates,
        counts: project.rest.counts,
      },
      downstream: {
        candidates: project.downstream.candidates,
        candidateCount: project.downstream.candidateCount,
      },
    })),
    complianceEvidence: draft.complianceEvidence,
    errors: draft.errors,
    assessmentDuration: draft.assessmentDuration,
    modelTasks: [
      "Review every REST and downstream candidate and approve or reject it.",
      "For each approved impact, explain actual and expected behavior, affected operations, evidence, and exact changed source.",
      "Assess documentation-grounded compliance and include actual, expected, guidance, evidence, and exact changed source for each finding.",
      "Do not generate semantic intents or explain changes without an actionable impact.",
    ],
  };
}

export function applyModelInputBudget(draft, budgetBytes) {
  const measuredDraft = {
    ...draft,
    modelInput: {
      serialization: "minified-json",
      bytes: 0,
      estimatedTokens: 0,
      budgetBytes,
    },
  };
  let bytes = 0;
  for (let attempt = 0; attempt < 3; attempt += 1) {
    measuredDraft.modelInput.bytes = bytes;
    measuredDraft.modelInput.estimatedTokens = Math.ceil(bytes / 4);
    bytes = Buffer.byteLength(JSON.stringify(measuredDraft));
  }
  measuredDraft.modelInput.bytes = bytes;
  measuredDraft.modelInput.estimatedTokens = Math.ceil(bytes / 4);
  const finalBytes = Buffer.byteLength(JSON.stringify(measuredDraft));
  measuredDraft.modelInput.bytes = finalBytes;
  measuredDraft.modelInput.estimatedTokens = Math.ceil(finalBytes / 4);
  if (finalBytes > budgetBytes) {
    throw new Error(
      `Model input is ${finalBytes} bytes, exceeding the ${budgetBytes}-byte budget`,
    );
  }
  return measuredDraft;
}

export async function runAssessmentAnalysis(options) {
  const startedAt = process.hrtime.bigint();
  const documentCache =
    options.documentCache ??
    join(canonicalTempDirectory(), "typespec-assessment-document-cache");
  let evidence;
  let analysis;
  let outputRoot;
  if (options.evidenceDirectory) {
    const evidenceRoot = resolve(options.evidenceDirectory);
    evidence = hydrateHistoricalSourceEvidence(
      JSON.parse(readFileSync(join(evidenceRoot, "evidence.json"), "utf8")),
      options.sourceAssessment,
    );
    outputRoot = resolve(options.prepare.output);
    mkdirSync(outputRoot, { recursive: true });
    analysis = analyzeArtifacts(evidence, evidenceRoot);
    rmSync(join(outputRoot, "analysis.json"), { force: true });
    writeFileSync(
      join(outputRoot, "analysis-metadata.json"),
      `${JSON.stringify(
        {
          schemaVersion: analysis.schemaVersion,
          generatedAt: analysis.generatedAt,
          durationMs: analysis.durationMs,
          sourceIndexCount: analysis.sourceIndex.length,
          projects: analysis.projects.map((project) => ({
            path: project.path,
            baselineOperations: project.rest.baseline.length,
            headOperations: project.rest.head.length,
            changedOperations: project.rest.changes.length,
            restBreakingCandidates: project.rest.restBreakingCandidates.length,
            downstreamCandidates: project.downstream.candidates.length,
          })),
          fullAnalysisPersistence: "in-memory-only",
        },
        null,
        2,
      )}\n`,
    );
    evidence = {
      ...evidence,
      repositoryRoot: evidenceRoot,
      durationMs: elapsedMs(startedAt),
      phaseDurations: {
        ...(evidence.phaseDurations ?? {}),
        deterministicAnalysisMs: analysis.durationMs,
      },
    };
  } else {
    evidence = await prepareAssessment({
      ...options.prepare,
      excludePaths: [
        ...(options.prepare.excludePaths ?? []),
        resolve(documentCache),
      ],
    });
    outputRoot = resolve(evidence.repositoryRoot, options.prepare.output);
    analysis = JSON.parse(
      readFileSync(join(outputRoot, "analysis.json"), "utf8"),
    );
  }
  const catalogPath = new URL(
    "../references/reference-document-links.md",
    import.meta.url,
  );
  const preparedComplianceEvidence = await prepareComplianceEvidence({
    evidence,
    catalogText: readFileSync(catalogPath, "utf8"),
    cacheRoot: resolve(documentCache),
  });
  const complianceEvidence = mergeHistoricalComplianceDocuments(
    preparedComplianceEvidence,
    options.sourceAssessment,
  );
  writeFileSync(
    join(outputRoot, "compliance-evidence.json"),
    `${JSON.stringify(complianceEvidence, null, 2)}\n`,
  );
  const fullDraft = buildAssessmentDraft({
      evidence,
      analysis,
      complianceEvidence,
      totalMs: elapsedMs(startedAt),
    });
  const draft = applyModelInputBudget(
    options.fastMode ? buildFastAssessmentDraft(fullDraft) : fullDraft,
    options.modelInputBudgetBytes ?? 250 * 1024,
  );
  const draftName = options.fastMode
    ? "fast-assessment-draft.json"
    : "assessment-draft.json";
  const inputName = options.fastMode
    ? "fast-model-input.json"
    : "model-input.json";
  writeFileSync(
    join(outputRoot, draftName),
    `${JSON.stringify(draft, null, 2)}\n`,
  );
  writeFileSync(join(outputRoot, inputName), JSON.stringify(draft));
  return draft;
}

async function main() {
  const options = analysisArgs(process.argv.slice(2));
  const draft = await runAssessmentAnalysis(options);
  process.stdout.write(
    `Prepared deterministic assessment draft for ${draft.projects.length} TypeSpec project(s) in ${(draft.assessmentDuration.totalMs / 1000).toFixed(1)}s.\n`,
  );
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  main().catch((error) => {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  });
}
