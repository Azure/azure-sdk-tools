import assert from "node:assert/strict";
import test from "node:test";

import {
  buildSourceIndex,
  extractTypeSpecSymbols,
  extractVersionedMembers,
} from "./source-index.mjs";

test("source index extracts changed declarations and decorators with lines", () => {
  const diffs = [
    {
      path: "spec/main.tsp",
      oldStart: 10,
      newStart: 10,
      lines: [
        " model Widget {",
        "-  oldName?: string;",
        "+  @added(Versions.v2)",
        "+  newName?: string;",
        " }",
      ],
    },
  ];
  const sourceReferences = [
    {
      path: "spec/main.tsp",
      revision: "head",
      startLine: 10,
      endLine: 13,
      link: "spec/main.tsp#L10-L13",
    },
  ];
  const index = buildSourceIndex(diffs, sourceReferences);
  assert.deepEqual(
    index.map((entry) => [entry.symbol, entry.change, entry.line]),
    [["@added", "added", 11]],
  );
  assert.equal(index[0].sourceReference.link, "spec/main.tsp#L10-L13");
  assert.ok(extractTypeSpecSymbols(diffs).includes("@added"));
});

test("versioned members retain their enclosing declaration", () => {
  assert.deepEqual(
    extractVersionedMembers([
      {
        path: "spec/models.tsp",
        oldStart: 10,
        newStart: 12,
        context: "model Widget {",
        lines: [
          "+  @added(Versions.v2)",
          "+  displayName?: string;",
          " }",
          "+@added(Versions.v2)",
          '+#suppress "legacy-rule" "justification"',
          "+@Legacy.customAzureResource(#{ isAzureResource: true })",
          "+model Gadget {",
          "+  id: string;",
          "+}",
        ],
      },
    ]),
    [
      {
        path: "spec/models.tsp",
        owner: "Widget",
        symbol: "displayName",
        version: "v2",
        sourceChangeId: "spec/models.tsp:12:10",
      },
      {
        path: "spec/models.tsp",
        owner: "Gadget",
        symbol: "Gadget",
        version: "v2",
        sourceChangeId: "spec/models.tsp:12:10",
      },
    ],
  );
});
