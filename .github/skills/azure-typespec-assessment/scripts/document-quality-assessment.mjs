import { canonicalJson, stableId } from "./stable-id.mjs";
import { buildDocumentQualityInput } from "./document-quality-input.mjs";

export const DOCUMENT_QUALITY_ARTIFACT = "dimensions/document-quality-input.json";
const CHECKS = ["correctness", "meaning"];
const DECISION_FIELDS = [
  "reviewUnitId", "documentId", "check", "decision", "rationale",
  "title", "expected", "docQuote",
];
const LEGACY_SUMMARY = "Document Quality was not assessed: source documentation input is unavailable.";

function requireValue(condition, message) {
  if (!condition) throw new Error(`Document Quality: ${message}`);
}

function object(value, fields, label) {
  requireValue(value && typeof value === "object" && !Array.isArray(value), `${label} must be an object.`);
  const unknown = Object.keys(value).filter((key) => !fields.includes(key));
  requireValue(!unknown.length, `${label} contains unknown fields: ${unknown.join(", ")}.`);
}

function text(value, label) {
  requireValue(typeof value === "string" && value.trim().length > 0, `${label} must be a nonempty string.`);
}

function array(value, label) {
  requireValue(Array.isArray(value), `${label} must be an array.`);
}

function ids(value, label) {
  array(value, label);
  value.forEach((id) => text(id, label));
  requireValue(new Set(value).size === value.length, `${label} contains duplicates.`);
}

function coverage(expected, actual, label) {
  ids(actual, label);
  requireValue(
    expected.length === actual.length && expected.every((id) => actual.includes(id)),
    `${label} coverage mismatch.`,
  );
}

function equal(expected, actual, label) {
  requireValue(canonicalJson(expected) === canonicalJson(actual), `${label} does not match canonical evidence.`);
}

function validateSnapshot(snapshot, revision, source, label) {
  if (snapshot === null) return;
  object(snapshot, ["doc", "declaration", "source"], label);
  requireValue(typeof snapshot.doc === "string", `${label}.doc must be a string.`);
  text(snapshot.declaration, `${label}.declaration`);
  object(snapshot.source, ["path", "revision", "startLine", "endLine"], `${label}.source`);
  requireValue(
    snapshot.source.path === source.path && snapshot.source.revision === revision &&
      Number.isInteger(snapshot.source.startLine) && snapshot.source.startLine > 0 &&
      Number.isInteger(snapshot.source.endLine) && snapshot.source.endLine >= snapshot.source.startLine,
    `${label} has invalid source provenance.`,
  );
}

function validateDocument(document, unit, sources) {
  object(document, ["id", "sourceChangeId", "qualifiedName", "kind", "before", "after"], "document");
  text(document.id, "document.id");
  requireValue(document.id.startsWith("document-"), "document.id must start with document-.");
  text(document.qualifiedName, "document.qualifiedName");
  text(document.kind, "document.kind");
  const source = sources.get(document.sourceChangeId);
  requireValue(source && unit.sourceChangeIds.includes(document.sourceChangeId), `document ${document.id} references an unknown source.`);
  const evidence = source.documentEvidence?.documents?.find((item) => item.id === document.id);
  const scopedHunks = unit.hunkIds.length ? unit.hunkIds : (source.declarations ?? [])
    .filter((declaration) => unit.declarationIds.includes(declaration.id))
    .flatMap((declaration) => declaration.hunkIds ?? []);
  requireValue(evidence && evidence.hunkIds?.some((id) => scopedHunks.includes(id)),
    `document ${document.id} does not match its semantic declaration scope.`);
  const { hunkIds: _hunks, blocker: _blocker, ...canonical } = evidence;
  validateSnapshot(document.before, "base", source, `${document.id}.before`);
  requireValue(document.after !== null, `document ${document.id} requires current documentation.`);
  validateSnapshot(document.after, "current", source, `${document.id}.after`);
  requireValue(document.after.doc.trim().length > 0,
    `document ${document.id} has empty or whitespace @doc, which is missing documentation and outside assessment scope.`);
  equal(canonical, document, `document ${document.id}`);
}

function scopeForSemantic(unit) {
  const sourceIds = unit.sourceChangeIds ?? (unit.sources ?? []).map((source) => source.id);
  const hunkIds = unit.hunkIds ?? (unit.sources ?? []).flatMap((source) => source.hunks.map((hunk) => hunk.id));
  return {
    sourceIds, hunkIds,
    declarationIds: unit.declarationIds ?? [],
  };
}

export function validateDocumentQualityInput(input, semanticUnits, sourceChanges, semanticStatus = "ready") {
  object(input, ["schemaVersion", "status", "blockers", "reviewUnits"], "input");
  requireValue(input.schemaVersion === 1, "unsupported input schemaVersion.");
  requireValue(["ready", "blocked"].includes(input.status), "invalid input status.");
  array(input.blockers, "input.blockers");
  array(input.reviewUnits, "input.reviewUnits");
  const semantic = new Map(semanticUnits.map((unit) => [unit.id ?? unit.reviewUnitId, unit]));
  const sources = new Map(sourceChanges.map((source) => [source.id, source]));
  coverage([...semantic.keys()], input.reviewUnits.map((unit) => unit.reviewUnitId), "review unit");
  for (const unit of input.reviewUnits) {
    object(unit, ["reviewUnitId", "status", "reason", "sourceChangeIds", "hunkIds", "declarationIds", "documents"], "review unit");
    requireValue(["ready", "not-applicable", "blocked"].includes(unit.status), "invalid review unit status.");
    if (unit.status !== "ready" || unit.reason !== undefined) text(unit.reason, "review unit reason");
    const scope = scopeForSemantic(semantic.get(unit.reviewUnitId));
    coverage(scope.sourceIds, unit.sourceChangeIds, "source change");
    coverage(scope.hunkIds, unit.hunkIds, "hunk");
    coverage(scope.declarationIds, unit.declarationIds, "declaration");
    for (const id of unit.sourceChangeIds) {
      requireValue(sources.has(id), `unknown source ${id}.`);
    }
    for (const id of unit.hunkIds) {
      requireValue(unit.sourceChangeIds.some((sourceId) =>
        sources.get(sourceId).hunks?.some((hunk) => hunk.id === id)), `unknown hunk ${id}.`);
    }
    for (const id of unit.declarationIds) {
      requireValue(unit.sourceChangeIds.some((sourceId) =>
        sources.get(sourceId).declarations?.some((declaration) =>
          declaration.id === id && (!unit.hunkIds.length ||
            declaration.hunkIds?.some((hunkId) => unit.hunkIds.includes(hunkId))))),
      `unknown or out-of-scope declaration ${id}.`);
    }
    array(unit.documents, "review unit documents");
    ids(unit.documents.map((document) => document.id), "document IDs");
    requireValue(unit.status !== "ready" || unit.documents.length > 0, "ready review unit requires documents.");
    requireValue(unit.status !== "not-applicable" || unit.documents.length === 0, "not-applicable review unit cannot contain documents.");
    for (const document of unit.documents) {
      validateDocument(document, unit, sources);
    }
  }
  requireValue(input.status !== "ready" || (!input.blockers.length && input.reviewUnits.every((unit) => unit.status !== "blocked")),
    "ready input cannot contain blockers or blocked review units.");
  requireValue(input.status !== "blocked" || input.blockers.length > 0 || input.reviewUnits.some((unit) => unit.status === "blocked"),
    "blocked input requires a blocker or blocked review unit.");
  equal(buildDocumentQualityInput({
    sourceIndex: { sourceChanges },
    semantic: { status: semanticStatus, reviewUnits: semanticUnits.map((unit) => {
      const scope = scopeForSemantic(unit);
      return {
        ...unit, id: unit.id ?? unit.reviewUnitId,
        sourceChangeIds: scope.sourceIds,
        hunkIds: scope.hunkIds,
        declarationIds: scope.declarationIds,
      };
    }) },
  }), input, "input");
}

function validateModelSummaries(modelInput, input) {
  array(modelInput.documentQualityReviewUnits, "model documentQualityReviewUnits");
  coverage(input.reviewUnits.map((unit) => unit.reviewUnitId),
    modelInput.documentQualityReviewUnits.map((unit) => unit.reviewUnitId), "model review unit");
  for (const summary of modelInput.documentQualityReviewUnits) {
    object(summary, [
      "reviewUnitId", "status", "documentIds", "evidenceSetId", "reason",
      "qualifiedNames", "sourceChangeIds", "hunkIds", "declarationIds",
    ], "model review unit");
    const unit = input.reviewUnits.find((item) => item.reviewUnitId === summary.reviewUnitId);
    requireValue(summary.status === unit.status, "model review unit status mismatch.");
    coverage(unit.documents.map((document) => document.id), summary.documentIds, "model document");
    text(summary.evidenceSetId, "model evidenceSetId");
    const evidence = modelInput.evidenceSets?.[summary.evidenceSetId];
    requireValue(evidence, "model references an unknown evidence set.");
    equal({
      sourceChangeIds: unit.sourceChangeIds,
      hunkIds: unit.hunkIds,
      declarationCount: unit.declarationIds.length,
      evidenceFactIds: [],
      evidenceRef: { artifact: DOCUMENT_QUALITY_ARTIFACT, id: unit.reviewUnitId },
    }, evidence, "model evidence set");
    if (summary.qualifiedNames !== undefined) {
      coverage([...new Set(unit.documents.map((document) => document.qualifiedName))].sort().slice(0, 24),
        summary.qualifiedNames, "model qualified name");
    }
    for (const field of ["sourceChangeIds", "hunkIds", "declarationIds"]) {
      if (summary[field] !== undefined) coverage(unit[field], summary[field], `model ${field}`);
    }
    if (unit.reason !== undefined || summary.reason !== undefined) {
      requireValue(unit.reason === summary.reason, "model reason mismatch.");
    }
  }
}

export function validateDocumentQualityDecisions(input, decisions) {
  array(decisions, "decisions");
  const expected = input.reviewUnits.filter((unit) => unit.status === "ready")
    .flatMap((unit) => unit.documents.flatMap((document) =>
      CHECKS.map((check) => `${unit.reviewUnitId}/${document.id}/${check}`)));
  for (const decision of decisions) {
    object(decision, DECISION_FIELDS, "decision");
    for (const field of ["reviewUnitId", "documentId", "rationale"]) text(decision[field], `decision.${field}`);
    requireValue(CHECKS.includes(decision.check), "invalid check.");
    requireValue(["pass", "fail", "not-assessed"].includes(decision.decision), "invalid decision.");
    for (const field of ["title", "expected", "docQuote"]) {
      if (decision.decision === "fail" || decision[field] !== undefined) text(decision[field], `decision.${field}`);
    }
    const unit = input.reviewUnits.find((item) => item.reviewUnitId === decision.reviewUnitId);
    const document = unit?.documents.find((item) => item.id === decision.documentId);
    requireValue(unit?.status === "ready" && document, "decision references an unknown or ineligible document.");
    if (decision.docQuote !== undefined) {
      requireValue(document.after.doc.includes(decision.docQuote), "docQuote must be an exact substring of current canonical @doc text.");
    }
  }
  coverage(expected, decisions.map((decision) =>
    `${decision.reviewUnitId}/${decision.documentId}/${decision.check}`), "decision");
}

function findingSources(unit, document, sources) {
  const source = sources.get(document.sourceChangeId);
  return [{
    ...source,
    hunks: (source.hunks ?? []).filter((hunk) => unit.hunkIds.includes(hunk.id)),
    declarations: (source.declarations ?? []).filter((declaration) => unit.declarationIds.includes(declaration.id)),
  }];
}

function assembleDimension(input, decisions, sourceChanges) {
  const sources = new Map(sourceChanges.map((source) => [source.id, source]));
  const unassessedIntentIds = [];
  const notApplicableIntentIds = [];
  let documentCount = 0;
  let assessedDocumentCount = 0;
  let assessedCheckCount = 0;
  const findings = [];
  const intentAssessments = input.reviewUnits.map((unit) => {
    const checks = decisions.filter((decision) => decision.reviewUnitId === unit.reviewUnitId);
    const eligible = unit.status === "ready";
    const complete = unit.status === "not-applicable" ||
      (eligible && checks.every((check) => check.decision !== "not-assessed"));
    if (!complete) unassessedIntentIds.push(unit.reviewUnitId);
    if (unit.status === "not-applicable") notApplicableIntentIds.push(unit.reviewUnitId);
    if (eligible) {
      documentCount += unit.documents.length;
      assessedDocumentCount += unit.documents.filter((document) =>
        checks.filter((check) => check.documentId === document.id && check.decision !== "not-assessed").length === 2).length;
      assessedCheckCount += checks.filter((check) => check.decision !== "not-assessed").length;
    }
    for (const check of checks.filter((check) => check.decision === "fail")) {
      const document = unit.documents.find((document) => document.id === check.documentId);
      findings.push({
        id: stableId("document-finding", [unit.reviewUnitId, document.id, check.check]),
        reviewUnitId: unit.reviewUnitId,
        documentId: document.id,
        check: check.check,
        title: check.title,
        expected: check.expected,
        actual: document.after.doc,
        rationale: check.rationale,
        docQuote: check.docQuote,
        semanticIntentIds: [unit.reviewUnitId],
        sources: findingSources(unit, document, sources),
        document,
      });
    }
    const status = checks.some((check) => check.decision === "fail") ? "failed"
      : unit.status === "not-applicable" ? "not-applicable"
        : complete ? "passed" : "not-assessed";
    return {
      reviewUnitId: unit.reviewUnitId,
      status,
      ...(unit.reason ? { reason: unit.reason } : {}),
      documents: unit.documents,
      checks,
    };
  });
  const status = findings.length ? "failed"
    : input.status === "blocked" || unassessedIntentIds.length || input.blockers.length ? "not-assessed" : "passed";
  return {
    status,
    summary: status === "failed"
      ? `${findings.length} source @doc Correctness or Meaning check(s) failed.`
      : status === "not-assessed"
        ? "Source @doc assessment is incomplete; blocked or not-assessed checks are not passes."
        : documentCount === 0
          ? "No applicable current source @doc declarations in the changed semantic scope."
          : "All applicable source @doc Correctness and Meaning checks passed.",
    coverage: {
      semanticIntentCount: input.reviewUnits.length,
      assessedIntentCount: input.reviewUnits.length - unassessedIntentIds.length,
      documentCount,
      assessedDocumentCount,
      checkCount: documentCount * 2,
      assessedCheckCount,
      unassessedIntentIds,
      notApplicableIntentIds,
    },
    intentAssessments,
    findings,
    blockers: input.blockers,
  };
}

export function assembleDocumentQuality({ input, modelInput = {}, decisions, semanticUnits, sourceChanges, semanticStatus = "ready" }) {
  const declared = modelInput.artifactReferences?.documentQuality;
  if (input === undefined && declared === undefined && modelInput.documentQualityReviewUnits === undefined) {
    requireValue(decisions === undefined || (Array.isArray(decisions) && decisions.length === 0),
      "nonempty decisions require a declared canonical artifact.");
    return { status: "not-assessed", summary: LEGACY_SUMMARY };
  }
  requireValue(declared === DOCUMENT_QUALITY_ARTIFACT, "documentQualityReviewUnits requires the declared canonical artifact.");
  requireValue(input !== undefined, "declared canonical artifact is missing.");
  validateDocumentQualityInput(input, semanticUnits, sourceChanges, semanticStatus);
  validateModelSummaries(modelInput, input);
  validateDocumentQualityDecisions(input, decisions);
  return assembleDimension(input, decisions, sourceChanges);
}

export function validateDocumentQualityDimension(dimension, semanticItems, semanticStatus = "ready") {
  const errors = [];
  try {
    object(dimension, ["status", "summary", "coverage", "intentAssessments", "findings", "blockers"], "dimension");
    text(dimension.summary, "dimension.summary");
    if (Object.keys(dimension).every((key) => ["status", "summary"].includes(key))) {
      requireValue(dimension.status === "not-assessed", "legacy dimension must be not-assessed.");
      return errors;
    }
    for (const field of ["intentAssessments", "findings", "blockers"]) array(dimension[field], `dimension.${field}`);
    coverage(semanticItems.map((item) => item.id), dimension.intentAssessments.map((item) => item.reviewUnitId), "final intent");
    const sourceMap = new Map();
    for (const item of semanticItems) {
      for (const source of item.sources ?? []) {
        const previous = sourceMap.get(source.id);
        sourceMap.set(source.id, previous ? {
          ...source,
          hunks: [...new Map([...previous.hunks, ...source.hunks].map((hunk) => [hunk.id, hunk])).values()],
          declarations: [...new Map([...previous.declarations, ...source.declarations].map((declaration) => [declaration.id, declaration])).values()],
        } : source);
      }
    }
    const reviewUnits = dimension.intentAssessments.map((item) => {
      object(item, ["reviewUnitId", "status", "reason", "documents", "checks"], "intent assessment");
      requireValue(["passed", "failed", "not-assessed", "not-applicable"].includes(item.status), "invalid intent status.");
      array(item.documents, "intent documents");
      array(item.checks, "intent checks");
      const semantic = semanticItems.find((unit) => unit.id === item.reviewUnitId);
      const scope = scopeForSemantic(semantic);
      const status = item.status === "not-applicable" ? "not-applicable"
        : item.checks.length ? "ready" : "blocked";
      return {
        reviewUnitId: item.reviewUnitId,
        status,
        ...(item.reason !== undefined ? { reason: item.reason } : {}),
        sourceChangeIds: scope.sourceIds,
        hunkIds: scope.hunkIds,
        declarationIds: scope.declarationIds,
        documents: item.documents,
      };
    });
    const input = {
      schemaVersion: 1,
      status: dimension.blockers.length || reviewUnits.some((unit) => unit.status === "blocked") ? "blocked" : "ready",
      blockers: dimension.blockers,
      reviewUnits,
    };
    validateDocumentQualityInput(input, semanticItems, [...sourceMap.values()], semanticStatus);
    const decisions = dimension.intentAssessments.flatMap((item) => item.checks);
    validateDocumentQualityDecisions(input, decisions);
    const expected = assembleDimension(input, decisions, [...sourceMap.values()]);
    equal(expected.status, dimension.status, "dimension status");
    equal(expected.coverage, dimension.coverage, "dimension coverage");
    equal(expected.intentAssessments, dimension.intentAssessments, "intent assessments");
    equal(expected.findings, dimension.findings, "findings");
  } catch (error) {
    errors.push(error.message);
  }
  return errors;
}
