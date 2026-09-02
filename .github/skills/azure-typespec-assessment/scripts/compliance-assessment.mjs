import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { canonicalJson, stableId } from "./stable-id.mjs";

const CATALOG_PATH = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "references",
  "reference-document-links.md",
);
const SCORE_FIELDS = [
  "exactSymbol",
  "patternCategory",
  "servicePlane",
  "changeContext",
];
const SCORE_VALUES = {
  exactSymbol: [0, 4],
  patternCategory: [0, 3],
  servicePlane: [0, 2],
  changeContext: [0, 1],
};
const DECISIONS = [
  "applicable-pass",
  "applicable-fail",
  "no-applicable-guidance",
  "not-assessed",
];

function duplicates(values) {
  const seen = new Set();
  return [
    ...new Set(
      values.filter((value) =>
        seen.has(value) ? true : (seen.add(value), false),
      ),
    ),
  ];
}

function assertKeys(value, allowed, label) {
  const unknown = Object.keys(value ?? {}).filter(
    (key) => !allowed.includes(key),
  );
  if (unknown.length)
    throw new Error(`${label} contains unknown fields: ${unknown.join(", ")}.`);
}

function exactCoverage(expected, actual, label) {
  const duplicate = duplicates(actual);
  const expectedSet = new Set(expected);
  const unknown = actual.filter((item) => !expectedSet.has(item));
  const missing = expected.filter((item) => !actual.includes(item));
  if (duplicate.length || unknown.length || missing.length) {
    throw new Error(
      `${label} coverage mismatch. Missing: ${missing.join(", ") || "none"}; ` +
        `unknown: ${unknown.join(", ") || "none"}; duplicate: ${duplicate.join(", ") || "none"}.`,
    );
  }
}

export function readComplianceCatalog(file = CATALOG_PATH) {
  const entries = [];
  const urls = new Set();
  let catalogOrder = 0;
  let category = "";
  for (const line of fs.readFileSync(file, "utf8").split(/\r?\n/)) {
    const heading = line.match(/^##\s+(.+)$/);
    if (heading) {
      category = heading[1];
      continue;
    }
    const entry = line.match(/^- \[([^\]]+)\]\((https?:\/\/[^)]+)\):\s*(.+)$/);
    if (!entry) continue;
    catalogOrder += 1;
    if (urls.has(entry[2])) continue;
    urls.add(entry[2]);
    entries.push({
      catalogOrder,
      category,
      title: entry[1],
      canonicalUrl: entry[2],
      description: entry[3],
    });
  }
  return entries;
}

function validateScore(score, label) {
  assertKeys(score, [...SCORE_FIELDS, "total"], `${label} score`);
  let total = 0;
  for (const field of SCORE_FIELDS) {
    if (!SCORE_VALUES[field].includes(score?.[field])) {
      throw new Error(`${label} has invalid ${field} score.`);
    }
    total += score[field];
  }
  if (score.total !== total) {
    throw new Error(`${label} score total must be ${total}.`);
  }
}

function validateGuidance(guidance, declarationIds, label) {
  assertKeys(
    guidance,
    [
      "section",
      "excerpt",
      "queryTerms",
      "examples",
      "applicableDeclarationIds",
    ],
    label,
  );
  if (!guidance.section?.trim() || !guidance.excerpt?.trim()) {
    throw new Error(`${label} requires a section and excerpt.`);
  }
  if (
    !Array.isArray(guidance.queryTerms) ||
    !Array.isArray(guidance.examples)
  ) {
    throw new Error(`${label} requires queryTerms and examples arrays.`);
  }
  if (
    !Array.isArray(guidance.applicableDeclarationIds) ||
    !guidance.applicableDeclarationIds.length ||
    guidance.applicableDeclarationIds.some(
      (id) => !declarationIds.includes(id),
    ) ||
    duplicates(guidance.applicableDeclarationIds).length
  ) {
    throw new Error(`${label} has invalid declaration applicability.`);
  }
  if (guidance.examples.length > 2) {
    throw new Error(`${label} may retain at most two examples.`);
  }
  for (const example of guidance.examples) {
    if (String(example).split(/\r?\n/).length > 12) {
      throw new Error(`${label} examples may contain at most 12 lines.`);
    }
  }
}

function validateDocument(document, catalogByUrl, declarationIds, label) {
  assertKeys(
    document,
    [
      "rank",
      "catalogOrder",
      "title",
      "canonicalUrl",
      "score",
      "selectionRationale",
      "retrieval",
      "guidance",
      "noRelevantGuidance",
    ],
    label,
  );
  const catalog = catalogByUrl.get(document.canonicalUrl);
  if (!catalog) throw new Error(`${label} uses an uncataloged URL.`);
  if (
    document.catalogOrder !== catalog.catalogOrder ||
    document.title !== catalog.title
  ) {
    throw new Error(`${label} does not match catalog identity or order.`);
  }
  if (!Number.isInteger(document.rank) || document.rank < 1) {
    throw new Error(`${label} has an invalid rank.`);
  }
  validateScore(document.score, label);
  if (!document.selectionRationale?.trim()) {
    throw new Error(`${label} requires a selection rationale.`);
  }
  if (
    document.retrieval?.status !== "fetched" ||
    Number.isNaN(Date.parse(document.retrieval.retrievedAt ?? "")) ||
    !/^sha256:[0-9a-f]{64}$/i.test(document.retrieval.contentHash ?? "")
  ) {
    throw new Error(`${label} requires fetched-content provenance.`);
  }
  if (
    !Array.isArray(document.guidance) ||
    typeof document.noRelevantGuidance !== "boolean"
  ) {
    throw new Error(`${label} requires guidance and noRelevantGuidance.`);
  }
  if (document.noRelevantGuidance === document.guidance.length > 0) {
    throw new Error(
      `${label} must contain guidance or declare no relevant guidance, not both.`,
    );
  }
  document.guidance.forEach((item, index) =>
    validateGuidance(item, declarationIds, `${label} guidance ${index + 1}`),
  );
}

function validateRankingEntry(entry, catalogByUrl, expectedRank, label) {
  assertKeys(
    entry,
    [
      "rank",
      "catalogOrder",
      "title",
      "canonicalUrl",
      "score",
      "selectionRationale",
    ],
    label,
  );
  const catalog = catalogByUrl.get(entry.canonicalUrl);
  if (
    !catalog ||
    entry.catalogOrder !== catalog.catalogOrder ||
    entry.title !== catalog.title
  ) {
    throw new Error(`${label} does not match a catalog entry.`);
  }
  if (entry.rank !== expectedRank)
    throw new Error(`${label} has an invalid rank.`);
  validateScore(entry.score, label);
  if (!entry.selectionRationale?.trim()) {
    throw new Error(`${label} requires a selection rationale.`);
  }
}

function sourceEvidence(request, declarationIds, hunkIds, sourceMap) {
  const sources = request.sourceChangeIds
    .map((id) => sourceMap.get(id))
    .filter(Boolean);
  const selectedDeclarations = sources.flatMap((source) =>
    (source.declarations ?? [])
      .filter((item) => declarationIds.includes(item.id))
      .map((item) => ({ ...item, sourcePath: source.path })),
  );
  const selectedHunks = new Set(hunkIds);
  const snippets = sources
    .flatMap((source) =>
      (source.hunks ?? [])
        .filter((hunk) => selectedHunks.has(hunk.id))
        .map((hunk) => ({
          path: source.path,
          hunkId: hunk.id,
          lines: (hunk.lines ?? [])
            .filter((line) => /^[+-](?![+-])/.test(line))
            .slice(0, 12),
        })),
    )
    .filter((snippet) => snippet.lines.length);
  return {
    sourceLinks: selectedDeclarations.map((declaration) => ({
      path: declaration.sourcePath,
      startLine: declaration.source?.startLine,
      endLine: declaration.source?.endLine,
      link: declaration.source?.link,
    })),
    codeSnippets: snippets,
  };
}

export function assembleCompliance({
  requests,
  evidence,
  decisions,
  sourceChanges,
  initialBlockers = [],
}) {
  if (
    !evidence ||
    evidence.schemaVersion !== 1 ||
    !Array.isArray(evidence.intents)
  ) {
    throw new Error(
      "Compliance search evidence is missing or has an unsupported schema.",
    );
  }
  if (!Array.isArray(decisions)) {
    throw new Error("Judgment.complianceDecisions must be an array.");
  }
  const catalog = readComplianceCatalog();
  const catalogByUrl = new Map(
    catalog.map((item) => [item.canonicalUrl, item]),
  );
  const requestMap = new Map(requests.map((item) => [item.reviewUnitId, item]));
  exactCoverage(
    requests.map((item) => item.reviewUnitId),
    evidence.intents.map((item) => item.reviewUnitId),
    "Compliance intent",
  );

  const sourceMap = new Map(sourceChanges.map((item) => [item.id, item]));
  const documentsByIntent = new Map();
  const catalogRankingByIntent = new Map();
  const blockersByIntent = new Map();
  const blockers = initialBlockers.map((message) => ({
    message: String(message?.message ?? message),
  }));
  const retrievalFailures = [];
  let guidanceExcerptCount = 0;

  for (const intent of evidence.intents) {
    const request = requestMap.get(intent.reviewUnitId);
    assertKeys(
      intent,
      [
        "reviewUnitId",
        "queryProfile",
        "catalogRanking",
        "rankedDocuments",
        "retrievalAttempts",
        "blockers",
      ],
      `Compliance intent ${intent.reviewUnitId}`,
    );
    if (
      canonicalJson(intent.queryProfile) !== canonicalJson(request.queryProfile)
    ) {
      throw new Error(
        `Compliance intent ${intent.reviewUnitId} changed its query profile.`,
      );
    }
    if (
      !Array.isArray(intent.rankedDocuments) ||
      !Array.isArray(intent.catalogRanking) ||
      !Array.isArray(intent.retrievalAttempts) ||
      !Array.isArray(intent.blockers)
    ) {
      throw new Error(
        `Compliance intent ${intent.reviewUnitId} has invalid evidence arrays.`,
      );
    }
    const hasCatalogExhaustion = intent.blockers.some((item) =>
      item.startsWith("catalog-exhausted:"),
    );
    if (intent.rankedDocuments.length !== 4 && !hasCatalogExhaustion) {
      throw new Error(
        `Compliance intent ${intent.reviewUnitId} requires four documents or catalog exhaustion.`,
      );
    }
    const urls = intent.rankedDocuments.map((item) => item.canonicalUrl);
    const ranks = intent.rankedDocuments.map((item) => item.rank);
    if (duplicates(urls).length) {
      throw new Error(
        `Compliance intent ${intent.reviewUnitId} selected duplicate documents.`,
      );
    }
    if (urls.some((url) => !catalogByUrl.has(url))) {
      throw new Error(
        `Compliance intent ${intent.reviewUnitId} uses an uncataloged URL.`,
      );
    }
    if (duplicates(ranks).length) {
      throw new Error(
        `Compliance intent ${intent.reviewUnitId} selected duplicate ranks.`,
      );
    }
    exactCoverage(
      catalog.map((item) => item.canonicalUrl),
      intent.catalogRanking.map((item) => item.canonicalUrl),
      `Compliance catalog ranking ${intent.reviewUnitId}`,
    );
    intent.catalogRanking.forEach((entry, index) =>
      validateRankingEntry(
        entry,
        catalogByUrl,
        index + 1,
        `Compliance ranking ${intent.reviewUnitId}[${index}]`,
      ),
    );
    const orderedRanking = [...intent.catalogRanking].sort(
      (left, right) =>
        right.score.total - left.score.total ||
        left.catalogOrder - right.catalogOrder,
    );
    if (
      canonicalJson(orderedRanking.map((item) => item.canonicalUrl)) !==
      canonicalJson(intent.catalogRanking.map((item) => item.canonicalUrl))
    ) {
      throw new Error(
        `Compliance intent ${intent.reviewUnitId} catalog is not score ordered.`,
      );
    }
    for (const attempt of intent.retrievalAttempts) {
      if (
        !catalogByUrl.has(attempt.canonicalUrl) ||
        attempt.status !== "failed" ||
        !attempt.error?.trim()
      ) {
        throw new Error(
          `Compliance intent ${intent.reviewUnitId} has an invalid retrieval attempt.`,
        );
      }
      retrievalFailures.push({ reviewUnitId: intent.reviewUnitId, ...attempt });
    }
    const failedUrls = new Set(
      intent.retrievalAttempts.map((item) => item.canonicalUrl),
    );
    const expectedDocuments = intent.catalogRanking
      .filter((item) => !failedUrls.has(item.canonicalUrl))
      .slice(0, 4);
    if (
      canonicalJson(expectedDocuments.map((item) => item.canonicalUrl)) !==
      canonicalJson(urls)
    ) {
      throw new Error(
        `Compliance intent ${intent.reviewUnitId} did not select the first four retrievable documents.`,
      );
    }
    intent.rankedDocuments.forEach((document, index) => {
      validateDocument(
        document,
        catalogByUrl,
        request.declarationIds,
        `Compliance document ${intent.reviewUnitId}[${index}]`,
      );
      const ranking = expectedDocuments[index];
      for (const field of [
        "rank",
        "catalogOrder",
        "title",
        "canonicalUrl",
        "score",
        "selectionRationale",
      ]) {
        if (canonicalJson(document[field]) !== canonicalJson(ranking[field])) {
          throw new Error(
            `Compliance document ${intent.reviewUnitId}[${index}] differs from its ranking.`,
          );
        }
      }
    });
    const intentBlockers = [...intent.blockers];
    blockersByIntent.set(intent.reviewUnitId, intentBlockers);
    blockers.push(
      ...intentBlockers.map((message) => ({
        reviewUnitId: intent.reviewUnitId,
        message,
      })),
    );
    const finalDocuments = intent.rankedDocuments.map(
      ({ retrieval, ...document }) => ({
        ...document,
        retrievedAt: retrieval.retrievedAt,
        contentHash: retrieval.contentHash,
      }),
    );
    catalogRankingByIntent.set(intent.reviewUnitId, intent.catalogRanking);
    guidanceExcerptCount += finalDocuments.reduce(
      (total, document) => total + document.guidance.length,
      0,
    );
    documentsByIntent.set(intent.reviewUnitId, finalDocuments);
  }

  exactCoverage(
    requests.map((item) => item.reviewUnitId),
    decisions.map((item) => item.reviewUnitId),
    "Compliance decision",
  );
  const selectedDocumentCount = [...documentsByIntent.values()].reduce(
    (total, documents) => total + documents.length,
    0,
  );
  const accounting = evidence.inputAccounting;
  if (
    !accounting ||
    accounting.catalogEntriesScored !== catalog.length * requests.length ||
    accounting.documentsFetched !== selectedDocumentCount ||
    accounting.guidanceExcerptsRetained !== guidanceExcerptCount ||
    !Number.isInteger(accounting.documentBytesFetched) ||
    accounting.documentBytesFetched < 0 ||
    !Number.isInteger(accounting.guidanceExcerptBytesRetained) ||
    accounting.guidanceExcerptBytesRetained < 0
  ) {
    throw new Error("Compliance search input accounting is inconsistent.");
  }

  const decisionsByIntent = new Map();
  const findings = [];
  for (const decision of decisions) {
    assertKeys(
      decision,
      [
        "reviewUnitId",
        "applicableGuidance",
        "sourceChangeIds",
        "hunkIds",
        "declarationIds",
        "decision",
        "title",
        "severity",
        "expected",
        "actual",
        "rationale",
      ],
      `Compliance decision ${decision.reviewUnitId}`,
    );
    if (
      !DECISIONS.includes(decision.decision) ||
      !Array.isArray(decision.applicableGuidance) ||
      !Array.isArray(decision.sourceChangeIds) ||
      !Array.isArray(decision.hunkIds) ||
      !Array.isArray(decision.declarationIds) ||
      !decision.actual?.trim() ||
      !decision.rationale?.trim()
    ) {
      throw new Error(
        `Compliance decision ${decision.reviewUnitId} is incomplete.`,
      );
    }
    if (
      decision.decision === "applicable-fail" &&
      (!decision.title?.trim() ||
        !["high", "medium", "low"].includes(decision.severity))
    ) {
      throw new Error(
        `Compliance decision ${decision.reviewUnitId} lacks finding presentation.`,
      );
    }
    if (
      decision.decision === "no-applicable-guidance" &&
      (decision.applicableGuidance.length ||
        blockersByIntent.get(decision.reviewUnitId).length)
    ) {
      throw new Error(
        `Compliance decision ${decision.reviewUnitId} cannot use no-applicable-guidance with applicable guidance or blockers.`,
      );
    }
    const request = requestMap.get(decision.reviewUnitId);
    const subset = (actual, expected) =>
      !duplicates(actual).length &&
      actual.every((item) => expected.includes(item));
    if (
      !subset(decision.sourceChangeIds, request.sourceChangeIds) ||
      !subset(decision.hunkIds, request.hunkIds) ||
      !subset(decision.declarationIds, request.declarationIds)
    ) {
      throw new Error(
        `Compliance decision ${decision.reviewUnitId} has incorrect source evidence.`,
      );
    }
    if (
      decision.decision.startsWith("applicable-") &&
      (!decision.expected?.trim() ||
        !decision.applicableGuidance.length ||
        !decision.sourceChangeIds.length ||
        !decision.hunkIds.length ||
        !decision.declarationIds.length)
    ) {
      throw new Error(
        `Compliance decision ${decision.reviewUnitId} lacks applicable evidence.`,
      );
    }
    for (const applicable of decision.applicableGuidance) {
      assertKeys(
        applicable,
        ["canonicalDocumentUrl", "guidanceSection"],
        `Compliance guidance ${decision.reviewUnitId}`,
      );
      const document = documentsByIntent
        .get(decision.reviewUnitId)
        .find((item) => item.canonicalUrl === applicable.canonicalDocumentUrl);
      const guidance = document?.guidance.find(
        (item) =>
          item.section === applicable.guidanceSection &&
          item.applicableDeclarationIds.some((id) =>
            decision.declarationIds.includes(id),
          ),
      );
      if (!guidance) {
        throw new Error(
          `Compliance decision ${decision.reviewUnitId} uses unfetched guidance.`,
        );
      }
    }
    const source = sourceEvidence(
      request,
      decision.declarationIds,
      decision.hunkIds,
      sourceMap,
    );
    const assessment = {
      ...decision,
      gap: decision.rationale,
      ...source,
    };
    decisionsByIntent.set(decision.reviewUnitId, assessment);
    if (decision.decision === "applicable-fail") {
      findings.push({
        id: stableId("compliance", {
          reviewUnitId: decision.reviewUnitId,
        }),
        semanticIntentId: decision.reviewUnitId,
        title: decision.title,
        severity: decision.severity,
        applicableGuidance: decision.applicableGuidance.map((item) => {
          const document = documentsByIntent
            .get(decision.reviewUnitId)
            .find(
              (candidate) =>
                candidate.canonicalUrl === item.canonicalDocumentUrl,
            );
          const guidance = document.guidance.find(
            (candidate) => candidate.section === item.guidanceSection,
          );
          return {
            ...item,
            excerpt: guidance.excerpt,
          };
        }),
        declarationIds: decision.declarationIds,
        sourceChangeIds: decision.sourceChangeIds,
        hunkIds: decision.hunkIds,
        expected: decision.expected,
        actual: decision.actual,
        gap: decision.rationale,
        ...source,
      });
    }
  }

  const intentAssessments = requests.map((request) => ({
    semanticIntentId: request.reviewUnitId,
    ...decisionsByIntent.get(request.reviewUnitId),
    sourceChangeIds: request.sourceChangeIds,
    hunkIds: request.hunkIds,
    declarationIds: request.declarationIds,
    catalogRanking: catalogRankingByIntent.get(request.reviewUnitId),
    documents: documentsByIntent.get(request.reviewUnitId),
    blockers: blockersByIntent.get(request.reviewUnitId),
  }));
  const assessedIntentIds = decisions
    .filter(
      (item) =>
        item.decision === "applicable-pass" ||
        item.decision === "applicable-fail" ||
        item.decision === "no-applicable-guidance",
    )
    .map((item) => item.reviewUnitId);
  const unassessedIntentIds = requests
    .map((item) => item.reviewUnitId)
    .filter((id) => !assessedIntentIds.includes(id));
  const status = findings.length
    ? "failed"
    : blockers.length || unassessedIntentIds.length
      ? "not-assessed"
      : "passed";
  return {
    status,
    summary:
      status === "failed"
        ? `${findings.length} documentation-grounded compliance finding(s).`
        : status === "passed"
          ? decisions.some((item) => item.decision === "no-applicable-guidance")
            ? "All Semantic intents were assessed; no applicable guidance was found for one or more intents."
            : "All Semantic intents match applicable fetched guidance."
          : "Compliance evidence or Semantic intent coverage is incomplete.",
    coverage: {
      semanticIntentCount: requests.length,
      assessedIntentCount: assessedIntentIds.length,
      selectedDocumentCount,
      unassessedIntentIds,
    },
    intentAssessments,
    findings,
    retrievalFailures,
    blockers,
  };
}
