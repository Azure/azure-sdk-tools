import assert from "node:assert/strict";
import test from "node:test";
import { buildSourceIndex, parseUnifiedHunks } from "./source-index.mjs";

test("parseUnifiedHunks retains base and current ranges", () => {
  const hunks = parseUnifiedHunks(`diff --git a/main.tsp b/main.tsp
--- a/main.tsp
+++ b/main.tsp
@@ -2,2 +2,3 @@
 model Widget {
-  name: string;
+  name: string;
+  mode?: string;
 }`);
  assert.equal(hunks.length, 1);
  assert.deepEqual(hunks[0].base, { startLine: 2, endLine: 3 });
  assert.deepEqual(hunks[0].current, { startLine: 2, endLine: 4 });
  assert.match(hunks[0].id, /^hunk-/);
});

test("indexes changed interface operation members", () => {
  const files = {
    base: "interface Widgets {\n  get is ArmResourceRead<Widget>;\n}\n",
    working: "interface Widgets {\n  get is ArmResourceRead<Widget>;\n  cancel is ArmResourceActionAsync<Widget>;\n}\n",
  };
  const index = buildSourceIndex({
    repo: "repo",
    mergeBase: "base",
    headCommit: "head",
    changedFiles: [{
      path: "specification/widgets/main.tsp",
      status: "modified",
      origins: ["committed"],
    }],
    remoteUrl: "",
    readFile: (revision) => files[revision] ?? files.working,
    diffFile: () => `@@ -1,3 +1,4 @@
 interface Widgets {
   get is ArmResourceRead<Widget>;
+  cancel is ArmResourceActionAsync<Widget>;
 }`,
  });
  assert.ok(index.sourceChanges[0].declarations.some(
    (item) => item.kind === "operation" && item.qualifiedName === "Widgets.cancel",
  ));
});

test("does not classify inline operation response fields as interface properties", () => {
  const content = `interface Widgets {
  get(...ResourceParameters<Widget>):
    | OkResponse
    | {
        location: string;
      };
}
`;
  const index = buildSourceIndex({
    repo: "repo",
    mergeBase: "base",
    headCommit: "head",
    changedFiles: [{
      path: "specification/widgets/main.tsp",
      status: "modified",
      origins: ["committed"],
    }],
    remoteUrl: "",
    readFile: () => content,
    diffFile: () => `@@ -1,7 +1,7 @@
 interface Widgets {
   get(...ResourceParameters<Widget>):
     | OkResponse
     | {
-        location: string;
+        location: url;
       };
 }`,
  });
  assert.ok(!index.sourceChanges[0].declarations.some(
    (item) => item.kind === "property" && item.qualifiedName === "Widgets.location",
  ));
});
