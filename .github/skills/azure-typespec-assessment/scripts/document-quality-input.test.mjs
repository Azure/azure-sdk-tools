import assert from "node:assert/strict";
import path from "node:path";
import { pathToFileURL } from "node:url";
import test from "node:test";
import { addCompilerEvidence, buildSourceIndex } from "./source-index.mjs";
import { buildDocumentQualityInput } from "./document-quality-input.mjs";

function semanticFor(sourceIndex) {
  return {
    reviewUnits: sourceIndex.sourceChanges.map((source, index) => ({
      id: `unit-${index}`,
      sourceChangeIds: [source.id],
      hunkIds: source.hunks.map((hunk) => hunk.id),
      declarationIds: source.declarations.map((declaration) => declaration.id),
    })),
  };
}

test("legacy indexes explicitly block rather than silently pass", () => {
  const sourceIndex = { sourceChanges: [{ id: "source-1", path: "main.tsp" }] };
  const semantic = { reviewUnits: [{ id: "unit-1", sourceChangeIds: ["source-1"] }] };
  const result = buildDocumentQualityInput({ sourceIndex, semantic });
  assert.equal(result.schemaVersion, 3);
  assert.equal(result.status, "blocked");
  assert.equal(result.reviewUnits[0].reviewUnitId, "unit-1");
  assert.match(result.reviewUnits[0].reason, /must be recollected/);
  const legacy = buildDocumentQualityInput({ sourceIndex, semantic, schemaVersion: 1 });
  assert.equal(legacy.schemaVersion, 1);
  assert.match(legacy.reviewUnits[0].reason, /not collected/);
});

test("no @doc coverage is not applicable, including deleted docs", () => {
  const sourceIndex = { sourceChanges: [{
    id: "source-1", path: "main.tsp",
    documentEvidence: { schemaVersion: 3, status: "ready", blockers: [], documents: [] },
  }] };
  const semantic = { reviewUnits: [
    { id: "unit-1", sourceChangeIds: ["source-1"], hunkIds: ["hunk-1"] },
    { id: "unit-2", sourceChangeIds: ["source-1"], hunkIds: ["hunk-2"] },
  ] };
  const result = buildDocumentQualityInput({ sourceIndex, semantic });
  assert.equal(result.status, "ready");
  assert.deepEqual(result.reviewUnits.map((unit) => unit.status), ["not-applicable", "not-applicable"]);
  assert.ok(result.reviewUnits.every((unit) => /outside documentation coverage/.test(unit.reason)));
});

test("literal-only cached evidence must be recollected for new assessments", () => {
  for (const schemaVersion of [undefined, 1, 2]) {
    const sourceIndex = { sourceChanges: [{
      id: "source-1", path: "main.tsp",
      documentEvidence: { schemaVersion, status: "ready", blockers: [], documents: [] },
    }] };
    const semantic = { reviewUnits: [{ id: "unit-1", sourceChangeIds: ["source-1"] }] };
    const result = buildDocumentQualityInput({ sourceIndex, semantic });
    assert.equal(result.status, "blocked");
    assert.match(result.reviewUnits[0].reason, /recollect/i);
    assert.equal(buildDocumentQualityInput({ sourceIndex, semantic, schemaVersion: 1 }).status, "ready");
  }
});

test("unassociated source and unknown source block context", () => {
  const result = buildDocumentQualityInput({
    sourceIndex: { sourceChanges: [] },
    semantic: { reviewUnits: [{ id: "empty" }, { id: "unknown", sourceChangeIds: ["missing"] }] },
  });
  assert.equal(result.status, "blocked");
  assert.equal(result.blockers.length, 2);
  assert.equal(buildDocumentQualityInput({ sourceIndex: {} }).status, "blocked");
});

test("blocked semantics cannot become a ready empty documentation assessment", () => {
  const result = buildDocumentQualityInput({
    sourceIndex: { sourceChanges: [] },
    semantic: { status: "blocked", reviewUnits: [] },
  });
  assert.equal(result.status, "blocked");
  assert.match(result.blockers[0].reason, /Semantic assessment is blocked/);
});

test("builder retains only scoped current documents and strips index-only metadata", () => {
  const snapshot = {
    doc: "Carries settings.", declaration: '@doc("Carries settings.") model M {}',
    source: { path: "main.tsp", revision: "current", startLine: 1, endLine: 1 },
  };
  const sourceIndex = { sourceChanges: [{
    id: "source-1", path: "main.tsp",
    documentEvidence: { schemaVersion: 3, status: "ready", blockers: [], documents: [
      { id: "document-1", sourceChangeId: "source-1", qualifiedName: "M", kind: "model",
        before: null, after: snapshot, hunkIds: ["hunk-1"] },
      { id: "document-2", sourceChangeId: "source-1", qualifiedName: "N", kind: "model",
        before: null, after: snapshot, hunkIds: ["hunk-2"] },
    ] },
  }] };
  const result = buildDocumentQualityInput({ sourceIndex, semantic: { reviewUnits: [{
    id: "unit-1", sourceChangeIds: ["source-1"], hunkIds: ["hunk-1"], declarationIds: [],
  }] } });
  assert.equal(result.status, "ready");
  assert.deepEqual(result.reviewUnits[0].documents, [{
    id: "document-1", sourceChangeId: "source-1", qualifiedName: "M", kind: "model",
    before: null, after: snapshot,
  }]);
});

test("empty and whitespace-only @doc are explicitly excluded as missing coverage", () => {
  for (const doc of ["", " \t\r\n "]) {
    const sourceIndex = { sourceChanges: [{
      id: "source-1", path: "main.tsp",
      documentEvidence: { schemaVersion: 3, status: "ready", blockers: [], documents: [{
        id: "document-1", sourceChangeId: "source-1", qualifiedName: "M", kind: "model",
        before: null,
        after: { doc, declaration: `@doc(${JSON.stringify(doc)}) model M {}`,
          source: { path: "main.tsp", revision: "current", startLine: 1, endLine: 1 } },
        hunkIds: ["hunk-1"],
      }] },
    }] };
    const result = buildDocumentQualityInput({ sourceIndex, semantic: { reviewUnits: [{
      id: "unit-1", sourceChangeIds: ["source-1"], hunkIds: ["hunk-1"],
    }] } });
    assert.equal(result.status, "ready");
    assert.equal(result.reviewUnits[0].status, "not-applicable");
    assert.match(result.reviewUnits[0].reason, /empty, or whitespace-only/);
    assert.deepEqual(result.reviewUnits[0].documents, []);
  }
});

// Run against a real installed compiler without adding a dependency to this skill:
// TYPESPEC_COMPILER_ENTRY=<worktree>\node_modules\@typespec\compiler\dist\src\index.js
test("real compiler @doc evidence", { skip: !process.env.TYPESPEC_COMPILER_ENTRY }, async (t) => {
  const entry = path.resolve(process.env.TYPESPEC_COMPILER_ENTRY);
  const core = await import(pathToFileURL(entry).href);
  const ast = await import(pathToFileURL(path.join(path.dirname(entry), "ast", "index.js")).href);
  const root = process.cwd();
  const fileName = ".document-quality-fixture.tsp";
  const filePath = path.join(root, fileName).replaceAll("\\", "/");
  async function collect(before, after, diff, imported) {
    const lines = (text) => text === null ? [] : text.split("\n");
    const baseLines = lines(before);
    const currentLines = lines(after);
    const sourceIndex = buildSourceIndex({
      repo: root, mergeBase: "base", headCommit: "head",
      changedFiles: [{ path: fileName, status: before === null ? "added" : after === null ? "deleted" : "modified" }],
      readFile: (revision) => revision === "base" ? before : after,
      diffFile: () => diff ?? `@@ -1,${baseLines.length} +1,${currentLines.length} @@\n${
        baseLines.map((line) => `-${line}`).concat(currentLines.map((line) => `+${line}`)).join("\n")}`,
    });
    let revision = 0;
    await addCompilerEvidence({
      sourceIndex, baseWorktree: root, currentWorktree: root, projects: [fileName],
      loadCompiler: async () => {
        const text = [before, after][revision++];
        const files = new Map([[filePath, core.createSourceFile(text ?? "", filePath)]]);
        if (imported) {
          const importedPath = path.join(root, "unrelated.tsp").replaceAll("\\", "/");
          files.set(importedPath, core.createSourceFile(imported, importedPath));
        }
        const normalized = (value) => value.replaceAll("\\", "/");
        const host = {
          ...core.NodeHost,
          readFile: async (value) => files.get(normalized(value)) ?? core.NodeHost.readFile(value),
          stat: async (value) => files.has(normalized(value))
            ? { isFile: () => true, isDirectory: () => false }
            : core.NodeHost.stat(value),
        };
        return { ...core, ...ast, NodeHost: host, compilerVersion: "test" };
      },
    });
    return { sourceIndex, result: buildDocumentQualityInput({ sourceIndex, semantic: semanticFor(sourceIndex) }) };
  }

  await t.test("reopened undocumented namespaces do not block scoped documentation", async () => {
    const before = `namespace Contoso {
  @doc("The retry count.")
  model Settings { retries?: int32; }
}
namespace Contoso { model Other {} }`;
    const { result } = await collect(before, before.replace("retries?:", "retries:"));
    assert.equal(result.status, "ready", JSON.stringify(result.blockers));
    assert.deepEqual(result.reviewUnits[0].documents.map((document) => document.qualifiedName),
      ["Contoso.Settings"]);
  });

  await t.test("changed @doc augmentation blocks instead of claiming no applicable documentation", async () => {
    const before = 'model Widget {}\n@@doc(Widget, "A widget.");';
    const { result } = await collect(before, before.replace("A widget.", "The widget."));
    assert.equal(result.status, "blocked");
    assert.match(result.reviewUnits[0].reason, /supported named declaration context/);
  });

  await t.test("unchanged docs on a changed contract retain defaults, types, and constraints; siblings excluded", async () => {
    const before = `model Settings {
  @doc("The number of retries, defaulting to three.")
  @minValue(1)
  retries?: int32 = 3;
  @doc("Unrelated sibling.")
  sibling: string;
}`;
    const after = before.replace("retries?: int32 = 3", "retries: int64 = 5");
    const { result } = await collect(before, after);
    assert.equal(result.status, "ready", JSON.stringify(result.blockers));
    const documents = result.reviewUnits[0].documents;
    assert.equal(documents.length, 1);
    const document = documents[0];
    assert.equal(document.qualifiedName, "Settings.retries");
    assert.equal(document.before.doc, document.after.doc);
    assert.match(document.before.declaration, /@minValue\(1\)[\s\S]*retries\?: int32 = 3/);
    assert.match(document.after.declaration, /retries: int64 = 5/);
    assert.deepEqual(document.after.source, { path: fileName, revision: "current", startLine: 2, endLine: 4 });
    assert.match(document.id, /^document-/);
    const repeated = await collect(before, after);
    assert.deepEqual(repeated.result, result);
    assert.deepEqual(Object.keys(document).sort(),
      ["id", "sourceChangeId", "qualifiedName", "kind", "before", "after"].sort());
  });

  await t.test("unchanged documented siblings in real hunk context are excluded, without losing parent docs", async () => {
    const before = `model Base { inherited?: string; }
@doc("A settings payload.")
model Settings {
  @doc("The display name.")
  name: string;
  @doc("The retry count.")
  retries?: int32;
}`;
    const changedProperty = before.replace("retries?:", "retries:");
    const { result } = await collect(before, changedProperty, `@@ -2,7 +2,7 @@
 @doc("A settings payload.")
 model Settings {
   @doc("The display name.")
   name: string;
   @doc("The retry count.")
-  retries?: int32;
+  retries: int32;
 }`);
    assert.equal(result.status, "ready", JSON.stringify(result));
    assert.deepEqual(result.reviewUnits[0].documents.map((document) => document.qualifiedName).sort(),
      ["Settings", "Settings.retries"]);

    const changedParent = before.replace("model Settings {", "model Settings extends Base {");
    const parent = await collect(before, changedParent, `@@ -2,7 +2,7 @@
 @doc("A settings payload.")
-model Settings {
+model Settings extends Base {
   @doc("The display name.")
   name: string;
   @doc("The retry count.")
   retries?: int32;
 }`);
    assert.equal(parent.result.status, "ready", JSON.stringify(parent.result));
    assert.deepEqual(parent.result.reviewUnits[0].documents.map((document) => document.qualifiedName),
      ["Settings"]);
    assert.match(parent.result.reviewUnits[0].documents[0].after.declaration, /extends Base/);
  });

  await t.test("doc-only hunk, multiline literal and decorator, exact strings with quoted braces", async () => {
    const before = `@TypeSpec.doc(
  """
  Original {purpose}.
  A "quoted" example.
  """
)
model Widget { value: "{" | "}"; }`;
    const after = before.replace("Original {purpose}.", "Updated {purpose}.");
    const { result } = await collect(before, after,
      "@@ -3 +3 @@\n-  Original {purpose}.\n+  Updated {purpose}.");
    assert.equal(result.status, "ready", JSON.stringify(result.blockers));
    const [document] = result.reviewUnits[0].documents;
    assert.equal(document.after.doc, 'Updated {purpose}.\nA "quoted" example.');
    assert.equal(document.after.declaration, after);
    assert.deepEqual(document.after.source, { path: fileName, revision: "current", startLine: 1, endLine: 7 });
  });

  await t.test("nested property and operation parameter docs have exact association", async () => {
    const before = `namespace Demo;
model Outer {
  child: {
    @doc("Nested value.")
    value?: string;
  };
}
interface Reads {
  read(
    @doc("Result count.")
    count?: int32
  ): Outer;
}`;
    const after = before.replace("value?: string", "value: string").replace("count?: int32", "count: int32");
    const { result } = await collect(before, after);
    assert.equal(result.status, "ready", JSON.stringify(result.blockers));
    assert.deepEqual(result.reviewUnits[0].documents.map((document) => document.qualifiedName).sort(),
      ["Demo.Outer.child.value", "Demo.Reads.read.count"]);
    assert.ok(result.reviewUnits[0].documents.every((document) => !document.after.declaration.includes("interface")));
  });

  await t.test("dynamic docs block, rather than fabricating interpolated text", async () => {
    const before = '@doc("The {name}.", Widget) model Widget { value?: string; }';
    const after = before.replace("value?:", "value:");
    const { result } = await collect(before, after);
    assert.equal(result.status, "blocked");
    assert.match(result.reviewUnits[0].reason, /dynamic or formatted/);
    assert.deepEqual(result.reviewUnits[0].documents, []);
  });

  await t.test("literal version value changes retain unchanged documentation", async () => {
    const before = `enum Versions {
  @doc("Version published in January.")
  latest: "2025-01-01",
}`;
    const { result } = await collect(before, before.replace("2025-01-01", "2025-06-01"));
    assert.equal(result.status, "ready", JSON.stringify(result));
    const [document] = result.reviewUnits[0].documents;
    assert.equal(document.qualifiedName, "Versions.latest");
    assert.equal(document.before.doc, document.after.doc);
    assert.match(document.before.declaration, /2025-01-01/);
    assert.match(document.after.declaration, /2025-06-01/);
  });

  await t.test("comment version descriptions retain stale text when only the version value changes", async () => {
    const before = `enum Versions {
  /** Version published in January. */
  latest: "2025-01-01",
}`;
    const after = before.replace("2025-01-01", "2025-06-01");
    const { sourceIndex, result } = await collect(before, after,
      '@@ -2,2 +2,2 @@\n   /** Version published in January. */\n-  latest: "2025-01-01",\n+  latest: "2025-06-01",');
    assert.equal(sourceIndex.sourceChanges[0].documentEvidence.schemaVersion, 3);
    assert.equal(result.status, "ready", JSON.stringify(result));
    const [document] = result.reviewUnits[0].documents;
    assert.equal(document.qualifiedName, "Versions.latest");
    assert.equal(document.after.doc, "Version published in January.");
    assert.equal(document.before.doc, document.after.doc);
    assert.equal(document.after.declaration, '/** Version published in January. */\n  latest: "2025-06-01"');
    assert.deepEqual(document.after.source, { path: fileName, revision: "current", startLine: 2, endLine: 3 });
    assert.deepEqual(Object.keys(document.after).sort(), ["declaration", "doc", "source"]);
  });

  await t.test("version enum main comments describe the associated enum", async () => {
    const before = '/** Supported API versions. */\nenum Versions { latest: "2025-01-01" }';
    const after = before.replace("2025-01-01", "2025-06-01");
    const { result } = await collect(before, after);
    assert.equal(result.status, "ready", JSON.stringify(result));
    const [document] = result.reviewUnits[0].documents;
    assert.equal(document.kind, "enum");
    assert.equal(document.qualifiedName, "Versions");
    assert.equal(document.after.doc, "Supported API versions.");
    assert.equal(document.after.declaration, after);
  });

  await t.test("comment property descriptions preserve associated contract and exclude unchanged siblings", async () => {
    const before = `/** A settings payload. */
model Settings {
  /** An unrelated name. */
  name: string;
  /**
   * The retry count, defaulting to three.
   * Must be positive.
   */
  @minValue(1)
  retries?: int32 = 3;
}`;
    const after = before.replace("retries?: int32 = 3", "retries: int64 = 5");
    const { result } = await collect(before, after, `@@ -3,9 +3,9 @@
   /** An unrelated name. */
   name: string;
   /**
    * The retry count, defaulting to three.
    * Must be positive.
    */
   @minValue(1)
-  retries?: int32 = 3;
+  retries: int64 = 5;
 }`);
    assert.equal(result.status, "ready", JSON.stringify(result));
    assert.deepEqual(result.reviewUnits[0].documents.map((document) => document.qualifiedName).sort(),
      ["Settings", "Settings.retries"]);
    const document = result.reviewUnits[0].documents.find((item) => item.kind === "property");
    assert.equal(document.after.doc, "The retry count, defaulting to three.\nMust be positive.");
    assert.equal(document.before.doc, document.after.doc);
    assert.equal(document.after.declaration, `/**
   * The retry count, defaulting to three.
   * Must be positive.
   */
  @minValue(1)
  retries: int64 = 5`);
    assert.deepEqual(document.after.source, { path: fileName, revision: "current", startLine: 5, endLine: 10 });
    const wholeFile = await collect(before, after);
    assert.deepEqual(wholeFile.result.reviewUnits[0].documents.map((item) => item.qualifiedName).sort(),
      ["Settings", "Settings.retries"]);
  });

  await t.test("comment-only changes preserve exact resolved main description and declaration", async () => {
    const before = "/** Original purpose. */\nmodel M { value: string; }";
    const after = before.replace("Original", "Updated");
    const { result } = await collect(before, after,
      "@@ -1 +1 @@\n-/** Original purpose. */\n+/** Updated purpose. */");
    assert.equal(result.status, "ready", JSON.stringify(result));
    const [document] = result.reviewUnits[0].documents;
    assert.equal(document.before.doc, "Original purpose.");
    assert.equal(document.after.doc, "Updated purpose.");
    assert.equal(document.after.declaration, after);
    assert.deepEqual(document.after.source, { path: fileName, revision: "current", startLine: 1, endLine: 2 });
  });

  await t.test("explicit @doc overrides local comment descriptions, including empty literals", async () => {
    for (const doc of ["Explicit description.", ""]) {
      const before = `/** Comment description. */\n@doc(${JSON.stringify(doc)})\nmodel M { value?: string; }`;
      const after = before.replace("value?:", "value:");
      const { sourceIndex, result } = await collect(before, after);
      assert.equal(result.status, "ready", JSON.stringify(result));
      const [document] = sourceIndex.sourceChanges[0].documentEvidence.documents;
      assert.equal(document.after.doc, doc);
      assert.equal(document.after.declaration, after);
      assert.equal(result.reviewUnits[0].status, doc ? "ready" : "not-applicable");
    }
  });

  await t.test("only main comment descriptions are assessed, never tag bodies or documented parameters", async () => {
    for (const description of ["Reads a value.", ""]) {
      const before = `/**
 * ${description}
 * @param count Number of results.
 * @returns The resulting value.
 * @example A sample invocation.
 */
op read(count?: int32): string;`;
      const { result } = await collect(before, before.replace("count?:", "count:"));
      assert.equal(result.status, "ready", JSON.stringify(result));
      assert.equal(result.reviewUnits[0].status, description ? "ready" : "not-applicable");
      assert.deepEqual(result.reviewUnits[0].documents.map((document) => [document.qualifiedName, document.after.doc]),
        description ? [["read", description]] : []);
    }
  });

  await t.test("comment descriptions do not mask dynamic or augmented documentation", async () => {
    for (const before of [
      '/** Local description. */\n@doc("The {name}.", M) model M { x?: string; }',
      '/** Local description. */\nmodel M { x?: string; }\n@@doc(M, "Augmented description.");',
    ]) {
      const { result } = await collect(before, before.replace("x?:", "x:"));
      assert.equal(result.status, "blocked", JSON.stringify(result));
      assert.match(result.reviewUnits[0].reason, /dynamic or formatted|augmented @doc/);
    }
  });

  await t.test("inherited descriptions cover changed models and operations without local descriptions", async () => {
    const before = `import "./unrelated.tsp";
model M is Other { x?: string; }
op read is original;`;
    const imported = `/** An external model. */
model Other { /** An inherited value. */ value: string; }
/** An external operation. */
op original(): string;
/** Another external operation. */
op replacement(): string;`;
    const { sourceIndex, result } = await collect(before, before.replace("x?:", "x:").replace("is original", "is replacement"),
      undefined, imported);
    assert.equal(result.status, "ready", JSON.stringify(result));
    assert.equal(result.reviewUnits[0].status, "not-applicable");
    assert.deepEqual(result.reviewUnits[0].documents, []);
    assert.equal(result.reviewUnits[0].inheritedDocumentIds.length, 2);
    const documents = sourceIndex.sourceChanges[0].documentEvidence.documents;
    assert.deepEqual(documents.map((item) => [item.qualifiedName, item.after.doc]).sort(),
      [["M", "An external model."], ["read", "Another external operation."]]);
    for (const document of documents) {
      assert.equal(document.after.documentationOrigin, "inherited");
      assert.equal(document.before.documentationOrigin, "inherited");
      assert.ok(!document.after.declaration.includes("/**"));
    }
    assert.equal(documents.find((item) => item.qualifiedName === "read").before.doc, "An external operation.");
  });

  await t.test("local descriptions on template-derived operations override inherited documentation", async () => {
    const imported = '@doc("Inherited template description.") op Template<T>(value: T): T;';
    for (const local of [
      "/** Updates the supplied value. */",
      '@doc("Updates the supplied value.")',
      '/** Ignored comment. */\n@doc("Updates the supplied value.")',
    ]) {
      const before = `import "./unrelated.tsp";\n${local}\nop update is Template<string>;`;
      const after = before.replace("Template<string>", "Template<int32>");
      const { result } = await collect(before, after, undefined, imported);
      assert.equal(result.status, "ready", JSON.stringify(result));
      assert.equal(result.reviewUnits[0].status, "ready");
      const [document] = result.reviewUnits[0].documents;
      assert.equal(document.qualifiedName, "update");
      assert.equal(document.before.doc, "Updates the supplied value.");
      assert.equal(document.after.doc, document.before.doc);
      assert.equal(document.after.declaration, `${local}\nop update is Template<int32>;`);
      assert.equal(document.after.documentationOrigin, undefined);
      assert.ok(!document.after.doc.includes("Inherited"));
    }
  });

  await t.test("inherited formatted @doc is resolved for the concrete template argument", async () => {
    const imported = '@doc("Updates a {name}.", T) op ChangeValue<T extends {}>(value: T): T;';
    const before = `import "./unrelated.tsp";
model Widget {}
model Other {}
op update is ChangeValue<Widget>;`;
    const after = before.replace("ChangeValue<Widget>", "ChangeValue<Other>");
    const { sourceIndex, result } = await collect(before, after, undefined, imported);
    assert.equal(result.status, "ready", JSON.stringify(result));
    assert.equal(result.reviewUnits[0].status, "not-applicable");
    const [document] = sourceIndex.sourceChanges[0].documentEvidence.documents;
    assert.equal(document.before.doc, "Updates a Widget.");
    assert.equal(document.after.doc, "Updates a Other.");
    assert.equal(document.after.documentationOrigin, "inherited");
    assert.equal(document.after.declaration, "op update is ChangeValue<Other>;");
  });

  await t.test("tag-only local comments do not mask an inherited main description", async () => {
    const imported = '@doc("Updates the supplied value.") op ChangeValue<T>(value: T): T;';
    const before = `import "./unrelated.tsp";
/**
 * @param value The input value.
 * @returns The updated value.
 */
op update is ChangeValue<string>;`;
    const { sourceIndex, result } = await collect(before, before.replace("ChangeValue<string>", "ChangeValue<int32>"), undefined, imported);
    assert.equal(result.status, "ready", JSON.stringify(result));
    assert.equal(result.reviewUnits[0].status, "not-applicable");
    const [document] = sourceIndex.sourceChanges[0].documentEvidence.documents;
    assert.equal(document.after.doc, "Updates the supplied value.");
    assert.equal(document.after.documentationOrigin, "inherited");
    assert.ok(document.after.declaration.includes("@param"));
  });

  await t.test("empty local @doc overrides inherited text instead of silently falling back", async () => {
    const imported = '@doc("Inherited description.") op ChangeValue<T>(value: T): T;';
    const before = 'import "./unrelated.tsp";\n@doc("") op update is ChangeValue<string>;';
    const { result } = await collect(before, before.replace("ChangeValue<string>", "ChangeValue<int32>"), undefined, imported);
    assert.equal(result.status, "ready", JSON.stringify(result));
    assert.equal(result.reviewUnits[0].status, "not-applicable");
  });

  await t.test("only changed inheriting declarations are assessed and undocumented templates remain empty", async () => {
    for (const documentation of ["", '@doc("Inherited description.")']) {
      const imported = `${documentation}\nop ChangeValue<T>(value: T): T;`;
      const before = `import "./unrelated.tsp";
op changed is ChangeValue<string>;
op unchanged is ChangeValue<string>;`;
      const { sourceIndex, result } = await collect(before, before.replace("changed is ChangeValue<string>", "changed is ChangeValue<int32>"), undefined, imported);
      assert.equal(result.status, "ready", JSON.stringify(result));
      assert.deepEqual(result.reviewUnits[0].documents, []);
      assert.equal(result.reviewUnits[0].inheritedDocumentIds?.length ?? 0, documentation ? 1 : 0);
      assert.deepEqual(sourceIndex.sourceChanges[0].documentEvidence.documents.map((item) => item.qualifiedName), documentation ? ["changed"] : []);
    }
  });

  await t.test("template ancestor chains retain local main descriptions, not tag bodies", async () => {
    const imported = '@doc("Template docs.") op Template<T>(value: T): T;\nop Intermediate<T> is Template<T>;';
    const before = `import "./unrelated.tsp";
/**
 * Updates the supplied value.
 * @param value A template argument.
 * @returns The result.
 */
op update is Intermediate<string>;`;
    const { result } = await collect(before, before.replace("Intermediate<string>", "Intermediate<int32>"), undefined, imported);
    assert.equal(result.status, "ready", JSON.stringify(result));
    assert.equal(result.reviewUnits[0].documents[0].after.doc, "Updates the supplied value.");
  });

  await t.test("local augmentation and dynamic docs on template-derived operations still block", async () => {
    const imported = 'model M {}\nop Template<T>(value: T): T;';
    for (const local of [
      '/** Local description. */\nop update is Template<string>;\n@@doc(update, "Augmented description.");',
      '@doc("Description for {name}.", M)\nop update is Template<string>;',
    ]) {
      const before = `import "./unrelated.tsp";\n${local}`;
      const { result } = await collect(before, before.replace("Template<string>", "Template<int32>"), undefined, imported);
      assert.equal(result.status, "blocked", JSON.stringify(result));
      assert.match(result.reviewUnits[0].reason, /augmented @doc|dynamic or formatted/);
    }
  });

  await t.test("real compiler diagnostics explicitly block unresolved context", async () => {
    const { result } = await collect('@doc("A model.") model M {}',
      '@doc("A model.") model M { x: MissingType; }');
    assert.equal(result.status, "blocked");
    assert.match(result.reviewUnits[0].reason, /successfully compiled/);
  });

  await t.test("new literal and comment docs have no fabricated baseline snapshot", async () => {
    for (const after of [
      '@doc("Carries widget settings.") model M {}',
      '/** Carries widget settings. */ model M {}',
    ]) {
      const { result } = await collect("model M {}", after);
      assert.equal(result.status, "ready", JSON.stringify(result));
      const [document] = result.reviewUnits[0].documents;
      assert.equal(document.before, null);
      assert.equal(document.after.doc, "Carries widget settings.");
    }
  });

  await t.test("no docs, removed docs, and deleted targets are out of scope", async () => {
    for (const [before, after] of [
      ["model M { x?: string; }", "model M { x: string; }"],
      ['@doc("A model.") model M {}', "model M {}"],
      ['@doc("A model.") model M {}', null],
      [null, "model M {}"],
      ["/* Ordinary comment. */ model M { x?: string; }", "/* Ordinary comment. */ model M { x: string; }"],
      ["// Ordinary comment.\nmodel M { x?: string; }", "// Ordinary comment.\nmodel M { x: string; }"],
      ["/** A model. */ model M {}", "model M {}"],
    ]) {
      const { result } = await collect(before, after);
      assert.equal(result.reviewUnits[0].status, "not-applicable", JSON.stringify(result));
    }
  });

  await t.test("unchanged imported declarations are never audited", async () => {
    const before = 'import "./unrelated.tsp";\nmodel M { x?: Other; }';
    const { result } = await collect(before, before.replace("x?:", "x:"), undefined,
      '@doc("Unrelated imported documentation.") model Other { @doc("A property.") x: string; }');
    assert.equal(result.reviewUnits[0].status, "not-applicable", JSON.stringify(result));
  });

  await t.test("unrelated hunks exclude otherwise changed docs", async () => {
    const before = '@doc("Old.") model M {}\n\nmodel N { x?: string; }';
    const after = '@doc("New.") model M {}\n\nmodel N { x: string; }';
    const { sourceIndex } = await collect(before, after,
      '@@ -1 +1 @@\n-@doc("Old.") model M {}\n+@doc("New.") model M {}\n@@ -3 +3 @@\n-model N { x?: string; }\n+model N { x: string; }');
    const semantic = semanticFor(sourceIndex);
    semantic.reviewUnits[0].hunkIds = [sourceIndex.sourceChanges[0].hunks[1].id];
    const result = buildDocumentQualityInput({ sourceIndex, semantic });
    assert.equal(result.reviewUnits[0].status, "not-applicable");
  });
});
