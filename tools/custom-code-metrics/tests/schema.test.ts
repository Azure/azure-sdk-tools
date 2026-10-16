import { test, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import { access, copyFile, mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
const names = ["CustomCodeMetrics", "RepositoryCodeMetrics", "ReportIndex", "HistoryMonth"];
let directory: string;
let packageRoot: string;
let destination: string;
beforeEach(async () => {
  directory = await mkdtemp(join(tmpdir(), "metrics-schema-"));
  packageRoot = join(directory, "tools", "custom-code-metrics");
  destination = join(directory, "producer", "CustomCodeMetrics.schema.json");
  await mkdir(join(packageRoot, "schemas"), { recursive: true });
  await copyFile(join(root, "schema.ts"), join(packageRoot, "schema.ts"));
  await writeFile(join(packageRoot, "package.json"), '{"type":"module"}');
  for (const name of names) {
    await copyFile(join(root, "schemas", `${name}.schema.json`), join(packageRoot, "schemas", `${name}.schema.json`));
  }
});
afterEach(async () => {
  await rm(directory, { recursive: true, force: true });
});
const invoke = (...args: string[]) => {
  const result = spawnSync(process.execPath, ["--experimental-strip-types", join(packageRoot, "schema.ts"), ...args], {
    cwd: directory, encoding: "utf8",
  });
  if (result.error) throw result.error;
  return { ...result, text: result.stdout + result.stderr };
};
const canonical = () => readFile(join(packageRoot, "schemas", "CustomCodeMetrics.schema.json"));

test("sync-copy creates a producer mirror byte-for-byte from script-relative schemas, from another cwd", async () => {
  await assert.rejects(access(join(packageRoot, "node_modules")), { code: "ENOENT" });
  await assert.rejects(access(join(packageRoot, "schema.js")), { code: "ENOENT" });
  assert.equal(invoke("sync-copy", destination).status, 0);
  assert.deepEqual(await readFile(destination), await canonical());
  assert.equal(invoke("check-copy", destination).status, 0);
});
test("copy destination paths are resolved against the caller cwd", async () => {
  assert.equal(invoke("sync-copy", join("producer", "CustomCodeMetrics.schema.json")).status, 0);
  assert.deepEqual(await readFile(destination), await canonical());
});
test("check-copy detects byte drift without changing either file, and sync-copy repairs it", async () => {
  assert.equal(invoke("sync-copy", destination).status, 0);
  const source = await canonical();
  const drift = Buffer.concat([source, Buffer.from("\n")]);
  await writeFile(destination, drift);
  const result = invoke("check-copy", destination);
  assert.notEqual(result.status, 0);
  assert.match(result.text, /Snapshot schema copy is stale/);
  assert.deepEqual(await readFile(destination), drift);
  assert.deepEqual(await canonical(), source);
  assert.equal(invoke("sync-copy", destination).status, 0);
  assert.deepEqual(await readFile(destination), source);
});
test("check-copy fails for a missing destination without creating one", async () => {
  const result = invoke("check-copy", destination);
  assert.notEqual(result.status, 0);
  assert.match(result.text, /ENOENT/);
  await assert.rejects(readFile(destination), { code: "ENOENT" });
});
test("copy modes fail for missing canonical source without modifying the destination", async () => {
  assert.equal(invoke("sync-copy", destination).status, 0);
  const prior = await readFile(destination);
  await rm(join(packageRoot, "schemas", "CustomCodeMetrics.schema.json"));
  for (const mode of ["check-copy", "sync-copy"]) {
    const result = invoke(mode, destination);
    assert.notEqual(result.status, 0);
    assert.match(result.text, /ENOENT/);
    assert.deepEqual(await readFile(destination), prior);
  }
});
test("schema helper rejects unknown modes and missing or extra arguments without writing", async () => {
  for (const args of [[], ["invalid"], ["check", destination], ["check-copy"], ["sync-copy"], ["sync-copy", destination, "extra"]]) {
    const result = invoke(...args);
    assert.notEqual(result.status, 0);
    assert.match(result.text, /Usage: node --experimental-strip-types schema.ts/);
    await assert.rejects(readFile(destination), { code: "ENOENT" });
  }
});
test("generation and freshness checks use the relocated canonical folder from any cwd", async () => {
  const emitted = join(packageRoot, "tsp-output", "@typespec", "json-schema");
  await mkdir(emitted, { recursive: true });
  for (const name of names) {
    await copyFile(join(packageRoot, "schemas", `${name}.schema.json`), join(emitted, `${name}.json`));
  }
  assert.equal(invoke("check").status, 0);
  for (const name of names) {
    const generated = Buffer.concat([await readFile(join(emitted, `${name}.json`)), Buffer.from("\n")]);
    await writeFile(join(emitted, `${name}.json`), generated);
    const result = invoke("check");
    assert.notEqual(result.status, 0);
    assert.match(result.text, new RegExp(`${name} JSON Schema is stale`));
    assert.notDeepEqual(await readFile(join(packageRoot, "schemas", `${name}.schema.json`)), generated);
    assert.equal(invoke("generate").status, 0);
    assert.deepEqual(await readFile(join(packageRoot, "schemas", `${name}.schema.json`)), generated);
    assert.equal(invoke("check").status, 0);
  }
});
test("clean deletes emitter outputs but preserves all checked-in schemas", async () => {
  const emitted = join(packageRoot, "tsp-output", "@typespec", "json-schema");
  await mkdir(emitted, { recursive: true });
  const before = await Promise.all(names.map((name) => readFile(join(packageRoot, "schemas", `${name}.schema.json`))));
  for (let index = 0; index < names.length; index++) {
    await writeFile(join(emitted, `${names[index]}.json`), before[index]);
  }
  assert.equal(invoke("clean").status, 0);
  for (let index = 0; index < names.length; index++) {
    await assert.rejects(readFile(join(emitted, `${names[index]}.json`)), { code: "ENOENT" });
    assert.deepEqual(await readFile(join(packageRoot, "schemas", `${names[index]}.schema.json`)), before[index]);
  }
});
test("freshness checks fail explicitly when an emitted schema is missing", () => {
  const result = invoke("check");
  assert.notEqual(result.status, 0);
  assert.match(result.text, /ENOENT/);
});
test("canonical schema IDs identify tools and the initial observation and reporting formats", async () => {
  for (const name of names) {
    const schema = JSON.parse(await readFile(join(packageRoot, "schemas", `${name}.schema.json`), "utf8"));
    assert.equal(schema.$id, `https://raw.githubusercontent.com/Azure/azure-sdk-tools/main/tools/custom-code-metrics/schemas/${name}.schema.json`);
    assert.match(schema.$comment, /tools\/custom-code-metrics\/main.tsp in Azure\/azure-sdk-tools/);
    if (name === "RepositoryCodeMetrics") {
      assert.equal(schema.oneOf.length, 3);
      continue;
    }
    assert.equal(schema.properties.schemaVersion.const, "1.0");
    if (name === "HistoryMonth") assert.equal(schema.$defs.HistoryObservation.properties.schemaVersion.const, "1.0");
  }
});
