const SEVERITY_ORDER = { high: 0, medium: 1, low: 2 };

function allFindings(assessment) {
  const dimensions = assessment.dimensions;
  return [
    ...dimensions.restBreakingChanges.findings.map((finding) => ({
      ...finding,
      dimension: "REST",
    })),
    ...dimensions.restCompatibleDownstreamBreakingChanges.findings.map(
      (finding) => ({ ...finding, dimension: "Downstream" }),
    ),
    ...dimensions.azureCompliance.findings.map((finding) => ({
      ...finding,
      dimension: "Compliance",
    })),
  ].sort(
    (left, right) =>
      (SEVERITY_ORDER[left.severity] ?? 99) -
        (SEVERITY_ORDER[right.severity] ?? 99) ||
      left.dimension.localeCompare(right.dimension) ||
      left.title.localeCompare(right.title),
  );
}

export function deriveCodeSafety(assessment) {
  const findings = allFindings(assessment);
  if (
    (assessment.errors ?? []).length > 0 ||
    findings.some((finding) => finding.severity === "high")
  ) {
    return "Low";
  }
  if (
    findings.length > 0 ||
    assessment.dimensions.azureCompliance.status === "not-assessed"
  ) {
    return "Medium";
  }
  return "High";
}

function lineIndexForReference(hunk, sourceReferences) {
  const reference = sourceReferences.find(
    (candidate) => candidate.path === hunk.path,
  );
  if (!reference) return 0;
  let oldLine = hunk.oldStart;
  let newLine = hunk.newStart;
  for (const [index, line] of hunk.lines.entries()) {
    const appliesToReference =
      reference.revision === "base"
        ? !line.startsWith("+") && oldLine >= reference.startLine
        : !line.startsWith("-") && newLine >= reference.startLine;
    if (appliesToReference) return index;
    if (!line.startsWith("+")) oldLine += 1;
    if (!line.startsWith("-")) newLine += 1;
  }
  return 0;
}

function explicitDecoratorMarkers(change) {
  return [...change.typeSpecCause.matchAll(/`(@[a-zA-Z][\w.]*)/g)].map(
    (match) => match[1].toLowerCase(),
  );
}

function relevantDecoratorMarkers(change) {
  const cause = change.typeSpecCause.toLowerCase();
  const markers = explicitDecoratorMarkers(change);
  if (change.kind === "added" || /\badd(?:ed)?\b/.test(cause)) {
    markers.push("@added");
  }
  if (change.kind === "removed" || /\bremove[ds]?\b/.test(cause)) {
    markers.push("@removed");
  }
  return [...new Set(markers)];
}

function relevantTypeSpecSymbols(change) {
  const ignored = new Set([
    "Add",
    "After",
    "Before",
    "Mark",
    "Remove",
    "Removed",
    "TypeSpec",
    "Versions",
  ]);
  return [
    ...new Set(
      [
        ...change.typeSpecCause.matchAll(
          /\b[A-Z][A-Za-z0-9]*(?:[A-Z][A-Za-z0-9]*)+\b/g,
        ),
        ...change.typeSpecCause.matchAll(/`([A-Za-z_][A-Za-z0-9_]*)`/g),
      ]
        .map((match) => match[1] ?? match[0])
        .filter((value) => !ignored.has(value)),
    ),
  ];
}

function relevantLineIndex(hunk, change) {
  const symbols = relevantTypeSpecSymbols(change);
  const declarationIndex = hunk.lines.findIndex(
    (line) =>
      /^\+?\s*(?:model|interface|enum|union|scalar|alias|op)\s/.test(line) &&
      symbols.some((symbol) => line.includes(symbol)),
  );
  if (declarationIndex >= 0) return declarationIndex;
  const markers = relevantDecoratorMarkers(change);
  const markerIndex = hunk.lines.findIndex((line) =>
    markers.some((marker) => line.toLowerCase().includes(marker)),
  );
  if (markerIndex >= 0) return markerIndex;
  return hunk.lines.findIndex((line) =>
    symbols.some((symbol) => line.includes(symbol)),
  );
}

export function displayedHunkLines(
  hunk,
  sourceReferences,
  change,
  focusIndex = undefined,
) {
  const maximumLines = 12;
  if (hunk.lines.length <= maximumLines) return hunk.lines;
  const relevantIndex = relevantLineIndex(hunk, change);
  const targetIndex = Number.isInteger(focusIndex)
    ? focusIndex
    : relevantIndex >= 0
      ? relevantIndex
      : lineIndexForReference(hunk, sourceReferences);
  const start = Math.max(
    0,
    Math.min(targetIndex - 2, hunk.lines.length - maximumLines),
  );
  const end = start + maximumLines;
  const result = hunk.lines.slice(start, end);
  if (start > 0) {
    result.unshift(` ... ${start} earlier diff lines omitted ...`);
  }
  if (end < hunk.lines.length) {
    result.push(` ... ${hunk.lines.length - end} later diff lines omitted ...`);
  }
  return result;
}

function candidateFocuses(hunks, change) {
  const candidates = [];
  const addCandidate = (hunk, focusIndex, priority) => {
    if (focusIndex < 0) return;
    if (
      candidates.some(
        (candidate) =>
          candidate.hunk === hunk &&
          Math.abs(candidate.focusIndex - focusIndex) <= 6,
      )
    ) {
      return;
    }
    candidates.push({ hunk, focusIndex, priority });
  };
  for (const marker of explicitDecoratorMarkers(change)) {
    for (const hunk of hunks) {
      addCandidate(
        hunk,
        hunk.lines.findIndex((line) => line.toLowerCase().includes(marker)),
        0,
      );
    }
  }
  for (const symbol of relevantTypeSpecSymbols(change)) {
    for (const hunk of hunks) {
      for (const [index, line] of hunk.lines.entries()) {
        if (!line.includes(symbol)) continue;
        const declaration = line.match(
          /^\+?\s*(?:(model|interface|enum|union|scalar|alias|op)\s+[A-Za-z_][A-Za-z0-9_]*|([A-Za-z_][A-Za-z0-9_]*)\s+is\b)/,
        );
        if (!declaration) continue;
        const kind = declaration[1] ?? "operation";
        const priority = ["model", "interface", "op", "operation"].includes(
          kind,
        )
          ? 1
          : 2;
        addCandidate(hunk, index, priority);
      }
    }
  }
  for (const marker of relevantDecoratorMarkers(change)) {
    for (const hunk of hunks) {
      addCandidate(
        hunk,
        hunk.lines.findIndex((line) => line.toLowerCase().includes(marker)),
        2,
      );
    }
  }
  return candidates.sort((left, right) => left.priority - right.priority);
}

export function displayedTypeSpecExcerpts(hunks, change) {
  const excerpts = candidateFocuses(hunks, change).slice(0, 2);
  for (const hunk of hunks) {
    if (excerpts.length === 2) break;
    if (!excerpts.some((excerpt) => excerpt.hunk === hunk)) {
      excerpts.push({
        hunk,
        focusIndex: relevantLineIndex(hunk, change),
        priority: 3,
      });
    }
  }
  const displayedHunkCount = new Set(excerpts.map((excerpt) => excerpt.hunk))
    .size;
  return {
    excerpts,
    omittedCount: Math.max(0, hunks.length - displayedHunkCount),
  };
}
