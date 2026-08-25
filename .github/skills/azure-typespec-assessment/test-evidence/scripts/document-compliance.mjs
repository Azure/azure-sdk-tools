function selectedSources(assessment, entry, label) {
  if (entry.sourceReferences?.length > 0) return entry.sourceReferences;
  if (entry.sourcePathIncludes?.length > 0) {
    const sources = [
      ...assessment.dimensions.semanticUnderstanding.items.flatMap(
        (item) => item.sourceReferences,
      ),
      ...(assessment.assessmentEvidence?.changedTypeSpec ?? []),
    ].filter((source) =>
      entry.sourcePathIncludes.some((value) => source.path.includes(value)),
    );
    if (sources.length === 0) {
      throw new Error(
        `${label} sourcePathIncludes matched no source evidence.`,
      );
    }
    return [
      ...new Map(
        sources.map((source) => [
          `${source.path}:${source.revision}:${source.startLine}:${source.endLine}`,
          source,
        ]),
      ).values(),
    ];
  }
  if (
    !Array.isArray(entry.semanticItemIds) ||
    entry.semanticItemIds.length === 0
  ) {
    throw new Error(
      `${label} requires sourceReferences or explicit semanticItemIds.`,
    );
  }
  const items = assessment.dimensions.semanticUnderstanding.items;
  const selected = entry.semanticItemIds.map((id) => {
    const item = items.find((candidate) => candidate.id === id);
    if (!item) {
      throw new Error(`${label} references unknown semantic item: ${id}`);
    }
    return item;
  });
  const sources = selected.flatMap((item) => item.sourceReferences);
  return [
    ...new Map(
      sources.map((source) => [
        `${source.path}:${source.revision}:${source.startLine}:${source.endLine}`,
        source,
      ]),
    ).values(),
  ];
}

export function buildCompliance(assessment, specification) {
  const documents = specification.documents.map((document, index) => {
    const { semanticItemIds, sourcePathIncludes, ...content } = document;
    return {
      ...content,
      sourceReferences: selectedSources(
        assessment,
        document,
        `Compliance document ${index + 1}`,
      ),
    };
  });
  return {
    status: specification.status,
    ...(specification.reason ? { reason: specification.reason } : {}),
    ...(specification.status === "not-assessed"
      ? {}
      : {
          summary: {
            patternsAssessed: documents.length,
            findingCount: specification.findings?.length ?? 0,
          },
        }),
    documents,
    findings: (specification.findings ?? []).map((finding, index) => {
      const { semanticItemIds, sourcePathIncludes, ...content } = finding;
      const supportingDocument = documents.find(
        (document) => document.url === finding.documentationUrl,
      );
      const codeSnippets =
        content.codeSnippets ?? supportingDocument?.codeSnippets;
      const sourceReferences =
        codeSnippets === supportingDocument?.codeSnippets
          ? supportingDocument.sourceReferences
          : selectedSources(
              assessment,
              finding,
              `Compliance finding ${index + 1}`,
            );
      for (const snippet of codeSnippets ?? []) {
        const reference = sourceReferences.find(
          (candidate) => candidate.path === snippet.path,
        );
        if (!reference) continue;
        reference.startLine = Math.min(reference.startLine, snippet.startLine);
        reference.endLine = Math.max(reference.endLine, snippet.endLine);
        reference.link = reference.link.replace(
          /#L\d+-L\d+$/,
          `#L${reference.startLine}-L${reference.endLine}`,
        );
      }
      return {
        ...content,
        ...(codeSnippets ? { codeSnippets } : {}),
        sourceReferences,
      };
    }),
  };
}
