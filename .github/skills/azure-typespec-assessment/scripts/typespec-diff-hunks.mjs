function finishHunk(hunks, current) {
  if (current && current.lines.some((line) => /^[+-]/.test(line))) {
    hunks.push(current);
  }
}

export function parseTypeSpecDiffHunks(diffText) {
  const hunks = [];
  let path;
  let current;
  for (const line of diffText.split(/\r?\n/)) {
    if (line.startsWith("diff --git ")) {
      finishHunk(hunks, current);
      current = undefined;
      const match = line.match(/^diff --git a\/(.+) b\/(.+)$/);
      path = match?.[2];
      continue;
    }
    if (line.startsWith("+++ ")) {
      const value = line.slice(4).trim();
      if (value !== "/dev/null") path = value.replace(/^b\//, "");
      continue;
    }
    if (line.startsWith("@@ ")) {
      finishHunk(hunks, current);
      const match = line.match(
        /^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@(.*)$/,
      );
      if (!match || !path?.endsWith(".tsp")) {
        current = undefined;
        continue;
      }
      current = {
        path,
        oldStart: Number(match[1]),
        oldCount: Number(match[2] ?? 1),
        newStart: Number(match[3]),
        newCount: Number(match[4] ?? 1),
        context: match[5].trim(),
        lines: [],
      };
      continue;
    }
    if (
      current &&
      (/^[ +\-]/.test(line) || line === "\\ No newline at end of file")
    ) {
      current.lines.push(line);
    }
  }
  finishHunk(hunks, current);
  return hunks;
}

function rangesOverlap(start, count, referenceStart, referenceEnd) {
  if (count === 0) return false;
  const end = start + count - 1;
  return start <= referenceEnd && end >= referenceStart;
}

export function selectTypeSpecDiffHunks(hunks, sourceReferences) {
  return hunks.filter((hunk) =>
    sourceReferences.some((reference) => {
      if (reference.path !== hunk.path) return false;
      return reference.revision === "base"
        ? rangesOverlap(
            hunk.oldStart,
            hunk.oldCount,
            reference.startLine,
            reference.endLine,
          )
        : rangesOverlap(
            hunk.newStart,
            hunk.newCount,
            reference.startLine,
            reference.endLine,
          );
    }),
  );
}

export function untrackedTypeSpecDiffHunk(path, content) {
  const lines = content.replace(/\r\n/g, "\n").split("\n");
  if (lines.at(-1) === "") lines.pop();
  return {
    path,
    oldStart: 0,
    oldCount: 0,
    newStart: 1,
    newCount: lines.length,
    context: "",
    lines: lines.map((line) => `+${line}`),
  };
}
