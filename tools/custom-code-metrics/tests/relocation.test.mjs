import { test } from "node:test";
import assert from "node:assert/strict";
import { copyFile, mkdir, mkdtemp, readFile, rm, symlink, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { tmpdir } from "node:os";
import { spawnSync } from "node:child_process";
import { fileURLToPath, pathToFileURL } from "node:url";
import { runInNewContext } from "node:vm";
import { legacySnapshot, library, snapshot } from "../dashboard/tests/fixtures.mjs";

const root = fileURLToPath(new URL("..", import.meta.url));

test("source presentation removes obsolete labels and controls without offering a source-state override", async () => {
  const [html, app, styles] = await Promise.all(["index.html", "app.ts", "styles.css"]
    .map((name) => readFile(join(root, "dashboard", name), "utf8")));
  for (const source of [html, app]) {
    assert.doesNotMatch(source, /\binferred\b|checkout-badge|include-dirty|(?:clean|dirty) checkouts?|\(dirty\)|dirty observations/i);
  }
  assert.match(html, /<p>Custom source<\/p>/);
  assert.match(html, /class="custom-key">Custom source<\/span>/);
  assert.match(html, /Source classification uses file-level signals; custom code is not a measure of debt or API impact/);
  assert.match(app, /label: "Custom source \(%\)"/);
  assert.match(app, /includeDirty: false/);
  assert.doesNotMatch(styles, /\.badge\b/);
});

test("relocated builds are script-relative; publication needs no website and leaves existing site seeds untouched", async () => {
  const directory = await mkdtemp(join(tmpdir(), "metrics-build-"));
  const target = join(directory, "tools", "custom-code-metrics");
  try {
    for (const name of [
      "publishing.mjs", "dashboard/build.mjs", "dashboard/app.ts", "dashboard/data.ts", "dashboard/report.ts",
      "dashboard/tsconfig.json", "dashboard/index.html", "dashboard/styles.css", "dashboard/staticwebapp.config.json",
      ...["CustomCodeMetrics", "ReportIndex", "HistoryMonth"].map((name) => `schemas/${name}.schema.json`),
    ]) {
      const destination = join(target, name);
      await mkdir(dirname(destination), { recursive: true });
      await copyFile(join(root, name), destination);
    }
    await symlink(join(root, "node_modules"), join(target, "node_modules"), process.platform === "win32" ? "junction" : "dir");
    const invoke = (...args) => {
      const result = spawnSync(process.execPath, [join(target, "dashboard", "build.mjs"), ...args], {
        cwd: directory, encoding: "utf8",
      });
      if (result.error) throw result.error;
      return { ...result, text: result.stdout + result.stderr };
    };
    const publishBuild = invoke("--publishing-only");
    assert.equal(publishBuild.status, 0, publishBuild.text);
    await assert.rejects(readFile(join(target, "dashboard", "dist", "index.html")), { code: "ENOENT" });
    const { publish } = await import(pathToFileURL(join(target, "publishing.mjs")));
    const operations = [];
    const store = {
      async read(container, path, optional) { assert.equal(optional, true); return null; },
      async immutable(container, path) { operations.push(`${container}/${path}`); },
      async index() { operations.push("index"); },
    };
    assert.equal((await publish(snapshot(), store)).historyMonths, 1);
    assert.equal(operations.at(-1), "index");

    const seed = snapshot();
    const seedPath = join(directory, "seed.json");
    await writeFile(seedPath, JSON.stringify(seed));
    const dashboardBuild = invoke("--snapshot", "seed.json");
    assert.equal(dashboardBuild.status, 0, dashboardBuild.text);
    for (const name of ["index.html", "styles.css", "staticwebapp.config.json", "app.js"]) {
      assert.ok((await readFile(join(target, "dashboard", "dist", name))).length > 0);
    }
    const seedFile = join(target, "dashboard", "dist", "snapshots.js");
    const before = await readFile(seedFile);
    assert.match(before.toString(), new RegExp(seed.snapshotId));
    assert.equal(invoke("--publishing-only").status, 0);
    assert.deepEqual(await readFile(seedFile), before);
    for (const args of [
      ["--publishing-only", "--snapshot", seedPath],
      ["--index-url", "https://metrics.invalid/index.json", "--publishing-only"],
      ["--publishing-only", "--publishing-only"], ["--snapshot"], ["--unknown"],
      ["--preview"], ["--preview", "--snapshot", seedPath, "--index-url", "https://metrics.invalid/index.json"],
      ["--preview", "--publishing-only"], ["--preview", "--preview", "--snapshot", seedPath],
    ]) {
      const result = invoke(...args);
      assert.notEqual(result.status, 0);
      assert.match(result.text, /Usage:|cannot be combined|requires snapshot inputs/);
      assert.deepEqual(await readFile(seedFile), before);
    }
    const audited = snapshot([library("Azure.One", 10)]);
    audited.libraries[0].files = [{
      path: "sdk/alpha/Azure.One/src/One.cs", lines: 10, provenance: "custom", evidence: "no-generated-signal",
    }];
    await writeFile(seedPath, JSON.stringify(audited));
    const preview = invoke("--preview", "--snapshot", seedPath, "--snapshot", seedPath);
    assert.equal(preview.status, 0, preview.text);
    const context = {};
    const previewBytes = await readFile(seedFile);
    runInNewContext(previewBytes.toString(), context);
    assert.equal(context.customCodeMetricsPreview, true);
    assert.equal(context.customCodeMetricsIndexUrl, "");
    const publicSeeds = JSON.parse(JSON.stringify(context.customCodeMetricsSeed));
    assert.equal(publicSeeds.length, 1);
    assert.equal(publicSeeds[0].libraries[0].files, undefined);
    const { files, ...publicLibrary } = audited.libraries[0];
    assert.deepEqual(publicSeeds[0], { ...audited, libraries: [publicLibrary] });
    assert.deepEqual(JSON.parse(await readFile(seedPath, "utf8")), audited, "Preview changed the audit source.");
    for (const invalid of [
      "{broken", "{}", JSON.stringify({ ...audited, repository: { ...audited.repository, isDirty: true } }),
      JSON.stringify({ ...audited, schemaVersion: "2.0" }),
      JSON.stringify({ ...audited, schemaVersion: "0.0" }),
      JSON.stringify({ ...audited, schemaVersion: "3.0" }),
      JSON.stringify(legacySnapshot()),
      JSON.stringify({ ...audited, repository: { ...audited.repository, commit: "a".repeat(40) } }),
      JSON.stringify({ ...audited, collectedAt: "2026-10-02T12:00:00Z" }),
      JSON.stringify({ ...audited, summary: { ...audited.summary, customLines: 11 } }),
      JSON.stringify({ ...audited, libraries: [{ ...audited.libraries[0], files: [] }] }),
    ]) {
      await writeFile(seedPath, invalid);
      const result = invoke("--preview", "--snapshot", seedPath);
      assert.notEqual(result.status, 0);
      assert.match(result.text, /SyntaxError|Invalid snapshot|Official observations require committed source|rollup|file evidence|does not add up|identity.*disagree/);
      assert.deepEqual(await readFile(seedFile), previewBytes, "Invalid preview replaced the last-good seed.");
    }
    await writeFile(seedPath, JSON.stringify(audited));
    assert.equal(invoke("--snapshot", seedPath).status, 0);
    const localContext = {};
    runInNewContext(await readFile(seedFile, "utf8"), localContext);
    assert.equal(localContext.customCodeMetricsPreview, false);
    assert.deepEqual(JSON.parse(JSON.stringify(localContext.customCodeMetricsSeed)), [audited],
      "Ordinary offline builds lost local file evidence.");
    const unseeded = invoke();
    assert.equal(unseeded.status, 0, unseeded.text);
    assert.match(await readFile(seedFile, "utf8"), /customCodeMetricsSeed = \[\];/);
    assert.match(await readFile(seedFile, "utf8"), /customCodeMetricsPreview = false;/);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});
