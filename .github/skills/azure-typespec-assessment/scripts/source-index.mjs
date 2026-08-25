function unique(values) {
  return [...new Set(values)];
}

export function extractTypeSpecSymbols(typeSpecDiffs) {
  const changedLines = typeSpecDiffs
    .flatMap((hunk) => hunk.lines ?? [])
    .filter((line) => /^[+-](?![+-])/.test(line))
    .map((line) => line.slice(1));
  const symbols = [];
  for (const line of changedLines) {
    symbols.push(...(line.match(/@@?[A-Za-z_][A-Za-z0-9_.]*/g) ?? []));
    symbols.push(
      ...(line.match(
        /\b(?:Azure\.(?:Core|ResourceManager)|TypeSpec\.[A-Za-z0-9_.]+)\b/g,
      ) ?? []),
    );
    const declaration = line.match(
      /\b(?:model|interface|enum|union|scalar|alias|op)\s+([A-Za-z_][A-Za-z0-9_]*)/,
    );
    if (declaration) symbols.push(declaration[1]);
    for (const keyword of [
      "client",
      "common types",
      "enum",
      "long-running",
      "paging",
      "resource",
      "versioning",
    ]) {
      const normalizedLine = line.toLowerCase().replace(/[^a-z0-9]/g, "");
      const normalizedKeyword = keyword.toLowerCase().replace(/[^a-z0-9]/g, "");
      if (normalizedLine.includes(normalizedKeyword)) symbols.push(keyword);
    }
  }
  return unique(symbols).sort();
}

function sourceReferenceFor(sourceReferences, path, revision, line) {
  return sourceReferences.find(
    (reference) =>
      reference.path === path &&
      reference.revision === revision &&
      line >= reference.startLine &&
      line <= reference.endLine,
  );
}

export function buildSourceIndex(typeSpecDiffs, sourceReferences) {
  const entries = [];
  for (const hunk of typeSpecDiffs) {
    let oldLine = hunk.oldStart;
    let newLine = hunk.newStart;
    for (const rawLine of hunk.lines ?? []) {
      const prefix = rawLine[0];
      const content = rawLine.slice(1);
      const revision = prefix === "-" ? "base" : "head";
      const line = prefix === "-" ? oldLine : newLine;
      if (prefix === "+" || prefix === "-") {
        const decorators = content.match(/@@?[A-Za-z_][A-Za-z0-9_.]*/g) ?? [];
        for (const decorator of decorators) {
          entries.push({
            symbol: decorator,
            kind: "decorator",
            change: prefix === "+" ? "added" : "removed",
            path: hunk.path,
            revision,
            line,
            sourceReference: sourceReferenceFor(
              sourceReferences,
              hunk.path,
              revision,
              line,
            ),
          });
        }
        const declaration = content.match(
          /\b(model|interface|enum|union|scalar|alias|op)\s+([A-Za-z_][A-Za-z0-9_]*)/,
        );
        if (declaration) {
          entries.push({
            symbol: declaration[2],
            kind: declaration[1],
            change: prefix === "+" ? "added" : "removed",
            path: hunk.path,
            revision,
            line,
            sourceReference: sourceReferenceFor(
              sourceReferences,
              hunk.path,
              revision,
              line,
            ),
          });
        }
      }
      if (prefix !== "+") oldLine += 1;
      if (prefix !== "-") newLine += 1;
    }
  }
  return entries;
}

export function extractVersionedMembers(typeSpecDiffs) {
  const members = [];
  for (const hunk of typeSpecDiffs) {
    let owner = hunk.context?.match(
      /\b(?:model|interface|enum|union)\s+([A-Za-z_][A-Za-z0-9_]*)/,
    )?.[1];
    let pendingVersion;
    for (const rawLine of hunk.lines ?? []) {
      const prefix = rawLine[0];
      const content = rawLine.slice(1).trim();
      const declaration = content.match(
        /\b(model|interface|enum|union)\s+([A-Za-z_][A-Za-z0-9_]*)/,
      );
      if (declaration && prefix !== "-") {
        owner = declaration[2];
      }
      if (prefix !== "+") continue;
      const added = content.match(
        /@added\s*\(\s*Versions\.([A-Za-z_][A-Za-z0-9_]*)\s*\)/,
      );
      if (added) {
        pendingVersion = added[1];
      }
      if (!pendingVersion) continue;
      if (
        (content.startsWith("@") && !content.startsWith("@added")) ||
        content.startsWith("#suppress")
      ) {
        continue;
      }
      const withoutDecorators = content.replace(
        /^(?:@\S+(?:\([^)]*\))?\s*)+/,
        "",
      );
      const memberDeclaration = withoutDecorators.match(
        /\b(?:model|interface|enum|union|scalar|alias|op)\s+([A-Za-z_][A-Za-z0-9_]*)/,
      );
      const property = withoutDecorators.match(
        /^([A-Za-z_][A-Za-z0-9_]*)\??\s*:/,
      );
      const symbol = memberDeclaration?.[1] ?? property?.[1];
      if (!symbol) continue;
      members.push({
        path: hunk.path,
        owner,
        symbol,
        version: pendingVersion,
      });
      pendingVersion = undefined;
    }
  }
  return members.filter(
    (member, index) =>
      members.findIndex(
        (candidate) =>
          candidate.path === member.path &&
          candidate.owner === member.owner &&
          candidate.symbol === member.symbol &&
          candidate.version === member.version,
      ) === index,
  );
}
