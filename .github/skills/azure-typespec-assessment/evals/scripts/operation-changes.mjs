const METADATA_FIELDS = [
  ["paging", ["paging", "pageable", "nextlink"]],
  ["lro", ["lro", "long-running", "asynchronous"]],
  ["client location", ["client placement", "client location"]],
  ["client shape", ["flatten", "operation-group", "operation group"]],
  ["parameter order", ["parameter order"]],
  ["enum name", ["enum constant", "enum name"]],
  ["enum extensibility", ["extensible", "closed enum"]],
  ["common types", ["common-types", "common types"]],
];

function unique(values) {
  return [...new Set(values)];
}

function hasAddedDeclaration(typeSpecDiffs) {
  return typeSpecDiffs.some((hunk) =>
    hunk.lines.some((line, index) => {
      if (!/^\+\s*@added\(/.test(line)) return false;
      return hunk.lines
        .slice(index + 1, index + 10)
        .some((candidate) =>
          /^\+\s*(?:(?:model|interface|enum|union|scalar|alias|op)\s+[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*\s+is\b)/.test(
            candidate,
          ),
        );
    }),
  );
}

function changeKind(item, typeSpecDiffs, override) {
  if (override?.kind) return override.kind;
  const intent = item.intent.toLowerCase();
  if (intent.startsWith("remove ")) return "removed";
  if (intent.startsWith("add ") && hasAddedDeclaration(typeSpecDiffs)) {
    return "added";
  }
  return "modified";
}

function metadataField(intent) {
  const normalized = intent.toLowerCase();
  return (
    METADATA_FIELDS.find(([, terms]) =>
      terms.some((term) => normalized.includes(term)),
    )?.[0] ?? "API metadata"
  );
}

function aspectsFor(item, operations, kind, override) {
  if (override?.aspects) return override.aspects;
  if (kind === "added") {
    return [
      {
        field: "operation family",
        before: null,
        after: `${operations.length} operation${operations.length === 1 ? "" : "s"} added for ${unique(operations.flatMap((operation) => operation.apiVersions)).join(", ")} (${unique(operations.map((operation) => operation.method)).join(", ")}).`,
      },
    ];
  }
  if (kind === "removed") {
    return [
      {
        field: "operation or contract surface",
        before: `${operations.length} affected operation${operations.length === 1 ? "" : "s"} expose the removed surface.`,
        after: null,
      },
    ];
  }
  return [
    {
      field: metadataField(item.intent),
      before: "Baseline metadata and generated client shape.",
      after: item.intent,
    },
  ];
}

export function deriveOperationChanges(
  item,
  operations,
  { typeSpecDiffs = [], linkedFindingIds = [] } = {},
) {
  const override = item.changes?.[0];
  const kind = changeKind(item, typeSpecDiffs, override);
  return [
    {
      kind,
      summary: item.intent,
      operationIds: unique(
        operations.map((operation) => operation.operationId),
      ),
      apiVersions: unique(
        operations.flatMap((operation) => operation.apiVersions),
      ),
      aspects: aspectsFor(item, operations, kind, override),
      effect: item.restRepresentation?.summary ?? item.restRepresentation,
      typeSpecCause:
        override?.typeSpecCause ??
        item.transformationChain?.[0] ??
        "The changed TypeSpec declaration produces this semantic API change.",
      sourceReferences: item.sourceReferences,
      typeSpecDiffs,
      linkedFindingIds,
    },
  ];
}

export function linkImpactFindings(assessment) {
  const items = assessment.dimensions.semanticUnderstanding.items;
  const findings = [
    ...assessment.dimensions.restBreakingChanges.findings,
    ...assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings,
    ...assessment.dimensions.azureCompliance.findings,
  ];
  for (const item of items) {
    for (const change of item.changes) change.linkedFindingIds = [];
  }
  for (const finding of findings) {
    const findingPaths = new Set(
      (finding.sourceReferences ?? []).map((reference) => reference.path),
    );
    let matchingItems = items.filter((item) => {
      const itemReferences = [
        ...(item.sourceReferences ?? []),
        ...item.changes.flatMap((change) => change.sourceReferences ?? []),
        ...item.restRepresentation.operations.flatMap(
          (operation) => operation.sourceReferences ?? [],
        ),
      ];
      return itemReferences.some((reference) =>
        findingPaths.has(reference.path),
      );
    });
    if (matchingItems.length === 0 && items.length === 1) {
      matchingItems = items;
    }
    for (const item of matchingItems) {
      for (const change of item.changes) {
        if (!change.linkedFindingIds.includes(finding.id)) {
          change.linkedFindingIds.push(finding.id);
        }
      }
    }
  }
}
