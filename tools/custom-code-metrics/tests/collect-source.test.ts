import { test } from "node:test";
import assert from "node:assert/strict";
import { collectSource, physicalLines, type SourceProvider, type ObservationContext, type NativeLibraryInput } from "../collect-source.ts";

const context: ObservationContext = {
  repository: { name: "Azure/azure-sdk-for-java", commit: "a".repeat(40), isDirty: false },
  collectedAt: "2026-10-09T12:34:56.1234567Z",
};
const member: NativeLibraryInput = {
  library: "native-package", service: "example", category: "data-plane",
  projectPath: "sdk/example/native-package/pom.xml",
  sourcePaths: ["sdk/example/native-package/src/main/java/Custom.java", "sdk/example/shared/src/main/scala/Generated.scala"],
};
const texts = new Map([
  [member.projectPath, "<project/>"],
  [member.sourcePaths[0], "first\n\nlast\n"],
  [member.sourcePaths[1], "generated\r\nline"],
]);
function source(values = texts): SourceProvider {
  return {
    paths: [...values.keys()],
    async readText(path) {
      const text = values.get(path);
      if (text === undefined) throw new Error(`Missing source text: ${path}`);
      return text;
    },
  };
}
const classify = (path: string) => path.endsWith("Generated.scala") ?
  { provenance: "generated" as const, evidence: "explicit-test-generated" } :
  { provenance: "custom" as const, evidence: "explicit-test-custom" };

test("physical lines count blank lines and line terminators without an invented trailing line", () => {
  for (const [text, lines] of [["", 0], ["a", 1], ["\n", 1], ["a\n", 1], ["a\n\n", 2],
    ["a\r\nb\r\n", 2], ["a\rb", 2], ["a\r\n\nb", 3]] as const) {
    assert.equal(physicalLines(text), lines);
  }
});
test("native counting records exact input identity, physical counts and evidence without fake framework fields", async () => {
  const before = structuredClone(member);
  const result = await collectSource(source(), context, [member], [], classify);
  assert.equal(result.snapshotId, `20261009T1234561234567Z-${context.repository.commit}`);
  assert.equal(result.collectedAt, context.collectedAt);
  assert.equal(result.summary.customLines, 3);
  assert.equal(result.summary.generatedLines, 2);
  assert.equal(result.summary.totalLines, 5);
  assert.equal(result.summary.customRatio, 3 / 5);
  assert.equal(result.libraries[0].metrics.libraryCount, 1);
  assert.equal("targetFrameworks" in result.libraries[0], false);
  assert.deepEqual(result.libraries[0].files.map(({ path, lines }) => ({ path, lines })), [
    { path: member.sourcePaths[0], lines: 3 }, { path: member.sourcePaths[1], lines: 2 },
  ]);
  assert.deepEqual(member, before);
});
test("shared committed origins are weighted once per consuming package, not deduplicated across packages", async () => {
  const second = { ...member, library: "second-package" };
  const result = await collectSource(source(), context, [member, second], [], classify);
  assert.equal(result.summary.libraryCount, 2);
  assert.equal(result.summary.totalFiles, 4);
  assert.equal(result.summary.customLines, 6);
  assert.equal(result.summary.generatedLines, 4);
  assert.equal(result.summary.customRatio, 3 / 5);
});
test("genuine metadata-only packages produce null ratios, while no selected packages fail", async () => {
  const result = await collectSource(source(), context, [{ ...member, sourcePaths: [] }], [], classify);
  assert.equal(result.summary.libraryCount, 1);
  assert.equal(result.summary.totalFiles, 0);
  assert.equal(result.summary.customRatio, null);
  assert.equal(result.categories.find((row) => row.category === "management")?.metrics.libraryCount, 0);
  await assert.rejects(collectSource(source(), context, [], [], classify), /No shipping packages/);
});
test("invalid source identity and duplicate package/file inventories fail explicitly", async () => {
  const duplicateSource = source();
  for (const invalid of [
    { ...context, repository: { ...context.repository, commit: "fake" } },
    { ...context, collectedAt: "not-a-time" },
    { ...context, collectedAt: "2026-10-09T12:34:56.12345678Z" },
  ]) {
    await assert.rejects(collectSource(source(), invalid, [member], [], classify), /requires/);
  }
  await assert.rejects(collectSource({ ...duplicateSource, paths: [...duplicateSource.paths, duplicateSource.paths[0]] },
    context, [member], [], classify), /Duplicate source inventory/);
  await assert.rejects(collectSource(source(), context, [member, member], [], classify), /duplicate shipping package/);
  await assert.rejects(collectSource(source(), context, [{ ...member, sourcePaths: [member.sourcePaths[0], member.sourcePaths[0]] }],
    [], classify), /Duplicate counted file/);
});
test("missing metadata/source, traversal and read/classification errors never become empty-source success", async () => {
  await assert.rejects(collectSource(source(), context, [{ ...member, projectPath: "sdk/missing/pom.xml" }], [], classify),
    /metadata is absent/);
  await assert.rejects(collectSource(source(), context, [{ ...member, sourcePaths: ["sdk/missing.java"] }], [], classify),
    /Counted source is absent/);
  for (const path of ["../outside.java", "/absolute.java", "sdk\\windows.java", "sdk//empty.java"]) {
    await assert.rejects(collectSource(source(), context, [{ ...member, sourcePaths: [path] }], [], classify), /Invalid repository-relative/);
  }
  await assert.rejects(collectSource({ ...source(), readText: async () => { throw new Error("Source cache mismatch."); } },
    context, [member], [], classify), /Source cache mismatch/);
  await assert.rejects(collectSource(source(), context, [member], [], () => ({ provenance: "custom", evidence: "" })),
    /Missing source classification/);
});
