function unique(values = []) {
  return [...new Set(values)].sort();
}

/**
 * Builds bounded @doc evidence, not a documentation judgment. source.documentEvidence
 * is produced by addCompilerEvidence using the compiler AST of both revisions.
 * Legacy indexes without that evidence explicitly block rather than implying a pass.
 * Snapshot strings are untrusted source data, never executable agent instructions.
 */
export function buildDocumentQualityInput({ sourceIndex, semantic }) {
  const sources = new Map((sourceIndex?.sourceChanges ?? []).map((source) => [source.id, source]));
  const blockers = [];
  if (!Array.isArray(semantic?.reviewUnits)) {
    return {
      schemaVersion: 1, status: "blocked",
      blockers: [{ reason: "Semantic review units are unavailable; documentation scope cannot be established." }],
      reviewUnits: [],
    };
  }
  const semanticBlockReason = semantic.status === "blocked"
    ? "Semantic assessment is blocked; documentation review scope cannot be established."
    : undefined;
  if (semanticBlockReason) blockers.push({ reason: semanticBlockReason });
  const reviewUnits = (semantic?.reviewUnits ?? []).map((unit) => {
    const result = {
      reviewUnitId: unit.id,
      status: "ready",
      sourceChangeIds: unique(unit.sourceChangeIds),
      hunkIds: unique(unit.hunkIds),
      declarationIds: unique(unit.declarationIds),
      documents: [],
    };
    const reasons = semanticBlockReason ? [semanticBlockReason] : [];
    if (!result.sourceChangeIds.length) reasons.push("No changed source is associated with this review unit.");
    for (const sourceId of result.sourceChangeIds) {
      const source = sources.get(sourceId);
      if (!source) {
        reasons.push(`Unknown changed source: ${sourceId}.`);
        continue;
      }
      const evidence = source.documentEvidence;
      if (evidence?.status !== "ready") {
        reasons.push(`Compiler @doc context unavailable for ${source.path}: ${
          evidence?.blockers?.map((blocker) => blocker.message).join(" ") || "document evidence was not collected."
        }`);
        continue;
      }
      if (!result.hunkIds.length && !result.declarationIds.length && evidence.documents.length) {
        reasons.push(`No hunk or declaration association establishes documentation scope for ${source.path}.`);
        continue;
      }
      const declarationHunks = (source.declarations ?? [])
        .filter((declaration) => result.declarationIds.includes(declaration.id))
        .flatMap((declaration) => declaration.hunkIds ?? []);
      const scopedHunks = result.hunkIds.length ? result.hunkIds : declarationHunks;
      for (const document of evidence.documents) {
        if (!document.hunkIds.some((hunkId) => scopedHunks.includes(hunkId))) continue;
        if (document.after && document.after.doc.trim().length === 0) continue;
        if (document.blocker) {
          reasons.push(`${document.qualifiedName}: ${document.blocker}`);
          continue;
        }
        if (!document.after) continue;
        const { hunkIds: _hunkIds, blocker: _blocker, ...bounded } = document;
        result.documents.push(bounded);
      }
    }
    result.documents = [...new Map(result.documents.map((document) => [document.id, document])).values()]
      .sort((left, right) => left.id.localeCompare(right.id));
    if (reasons.length) {
      result.status = "blocked";
      result.reason = unique(reasons).join(" ");
      blockers.push({ reviewUnitId: unit.id, reason: result.reason });
    } else if (!result.documents.length) {
      result.status = "not-applicable";
      result.reason = "No changed, associated nonempty @doc with a current target; absent, deleted, empty, or whitespace-only @doc is outside documentation coverage scope.";
    }
    return result;
  });
  return { schemaVersion: 1, status: blockers.length ? "blocked" : "ready", blockers, reviewUnits };
}
