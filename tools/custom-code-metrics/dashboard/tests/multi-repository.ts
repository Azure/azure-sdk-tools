import { chromium, type Page } from "@playwright/test";
import assert from "node:assert/strict";
import { readFile, writeFile, rm } from "node:fs/promises";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { createHash } from "node:crypto";
import { acceptSnapshot, aggregate, parseSnapshot, REPOSITORIES, type Snapshot } from "../data.ts";
import { acceptPreviewMetadata, type PreviewMetadata } from "../preview.ts";
import { collectSource, type NativeRepositoryName } from "../../collect-source.ts";
import { browserSnapshot, library, snapshot } from "./fixtures.ts";

async function nativeFixture(repository: NativeRepositoryName): Promise<Snapshot> {
  const java = repository.endsWith("java");
  const root = "sdk/sample/azure-sample";
  const metadata = `${root}/${java ? "pom.xml" : "pyproject.toml"}`;
  const path = `${root}/${java ? "src/main/java/Sample.java" : "azure/sample/client.py"}`;
  return acceptSnapshot(await collectSource({
    paths: [metadata, path], async readText() { return "first\nsecond\n"; },
  }, { repository: { name: repository, commit: "1".repeat(40), isDirty: false }, collectedAt: "2026-10-01T12:00:00Z" },
  [{ library: java ? "com.azure:azure-sample" : "azure-sample", service: "sample", category: "data-plane",
    projectPath: metadata, sourcePaths: [path] }], [], () => ({ provenance: "custom", evidence: "no-generated-signal" })));
}
const inputs = [process.env.CUSTOM_CODE_METRICS_SNAPSHOT, process.env.CUSTOM_CODE_METRICS_JAVA_SNAPSHOT,
  process.env.CUSTOM_CODE_METRICS_PYTHON_SNAPSHOT];
const fixtures = [browserSnapshot(), await nativeFixture("Azure/azure-sdk-for-java"), await nativeFixture("Azure/azure-sdk-for-python")];
const sources = await Promise.all(inputs.map(async (path, index) => path ? parseSnapshot(await readFile(path, "utf8")) : fixtures[index]));
const sourceFiles = sources.map((_, index) => fileURLToPath(new URL(`../generated/multi-repository-${index}.json`, import.meta.url)));
const temporaryFiles = [...sourceFiles];
const percentage = (ratio: number | null) => ratio === null ? "N/A" : `${(ratio * 100).toFixed(2)}%`;
const number = new Intl.NumberFormat("en-US");
const pageUrl = new URL("../dist/index.html", import.meta.url).href;
async function presentation(page: Page): Promise<void> {
  assert.equal(await page.locator("#checkout-badge,#include-dirty,#index-loader,#index-form,#index-url,#load-index,#snapshot-files,#clear-data,#sort-key,#sort-descending,#preview-notice,#repository-branding,input[type=file],input[type=url]").count(), 0);
  assert.doesNotMatch(await page.locator("body").textContent() || "", /\binferred\b|(?:clean|dirty) checkout|Include dirty|Load a published snapshot index/i);
}
let browser;
try {
  for (let index = 0; index < sources.length; index++) await writeFile(sourceFiles[index], JSON.stringify(sources[index]));
  const build = spawnSync(process.execPath, ["--experimental-strip-types", fileURLToPath(new URL("../build.ts", import.meta.url)),
    "--preview", ...sourceFiles.flatMap((path) => ["--snapshot", path])], { stdio: "inherit" });
  if (build.error) throw build.error;
  assert.equal(build.status, 0);
  browser = await chromium.launch({ channel: "msedge", headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const errors: string[] = [];
  const requests: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => requests.push(request.url()));
  await page.goto(pageUrl);
  assert.equal(await page.locator("#repository-select").inputValue(), "Azure/azure-sdk-for-net");
  const views = new Map<string, { library: string; range: string; filteredCount: number }>();
  for (const [index, source] of sources.entries()) {
    await page.locator("#repository-select").selectOption(source.repository.name);
    await page.waitForFunction((count) => document.getElementById("library-count")?.textContent === count, String(source.summary.libraryCount));
    await presentation(page);
    assert.equal(await page.locator("#custom-percent").textContent(), percentage(source.summary.customRatio));
    assert.equal(await page.locator("#total-lines").textContent(), number.format(source.summary.totalLines));
    assert.equal(await page.locator("#service-count").textContent(), `${source.services.length} services`);
    assert.equal(await page.locator("#snapshot-select option").count(), 1);
    assert.equal(await page.locator("#snapshot-select").inputValue(), source.snapshotId);
    assert.equal(await page.locator("#history-values tr").count(), 1);
    assert.ok((await page.locator("#observation-info").textContent())?.includes(new Date(source.collectedAt).toISOString()));
    assert.deepEqual(await page.locator("#service-filter option").allTextContents(),
      ["All services", ...source.services.map((service) => service.service).sort()]);
    assert.match(await page.locator("#contract-info").textContent() || "", source.repository.name.endsWith("net") ? /C#/ :
      source.repository.name.endsWith("java") ? /Java\/Scala/ : /Python/);
    if (!source.repository.name.endsWith("net")) assert.doesNotMatch(await page.locator("#measurement-description").textContent() || "", /sdk\/core\/|C#/);
    assert.equal(await page.locator("#deployment-info").textContent(), "Embedded baseline. No automatic feed.");
    const name = source.libraries[0].library;
    await page.locator("#library-search").fill(name);
    await page.getByRole("button", { name, exact: true }).click();
    const filtered = source.libraries.filter((member) => member.library.toLowerCase().includes(name.toLowerCase()));
    assert.equal(await page.locator("#custom-percent").textContent(), percentage(aggregate(filtered).customRatio));
    if (!source.repository.name.endsWith("net")) assert.doesNotMatch(await page.locator("#detail-meta").textContent() || "", /net8\.0|Target framework/);
    const range = ["365", "30", "90"][index];
    await page.locator("#history-range").selectOption(range);
    await page.locator("button[data-sort='totalLines']").focus();
    await page.keyboard.press("Space");
    assert.equal(await page.locator("button[data-sort='totalLines']").evaluate((button) => button.closest("th")?.getAttribute("aria-sort")), "ascending");
    views.set(source.repository.name, { library: name, range, filteredCount: filtered.length });
    await page.locator("#library-search").fill("");
    for (const width of [390, 320]) {
      await page.setViewportSize({ width, height: 900 });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      await presentation(page);
    }
    await page.emulateMedia({ colorScheme: "dark" });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.emulateMedia({ colorScheme: "light" });
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.locator("#library-search").fill(name);
  }
  for (const source of sources) {
    await page.locator("#repository-select").selectOption(source.repository.name);
    const view = views.get(source.repository.name)!;
    assert.equal(await page.locator("#library-search").inputValue(), view.library);
    assert.equal(await page.locator("#history-range").inputValue(), view.range);
    assert.equal(await page.locator("#history-scope").inputValue(), view.library);
    assert.equal(await page.locator("#detail-heading").textContent(), view.library);
    assert.equal(await page.locator("#library-count").textContent(), String(view.filteredCount));
    assert.equal(await page.locator("button[data-sort='totalLines']").evaluate((button) => button.closest("th")?.getAttribute("aria-sort")), "ascending");
  }
  const before = requests.length;
  for (const repository of REPOSITORIES.filter((repository) => !repository.implemented)) {
    await page.locator("#repository-select").selectOption(repository.name);
    assert.equal(await page.locator("#measurements").isVisible(), false);
    assert.match(await page.locator("#repository-state").textContent() || "", /collector is not implemented/);
    await presentation(page);
    assert.equal(requests.length, before);
  }
  await page.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
  await page.locator("#library-search").fill("");
  if (inputs[0]) {
    await page.locator("#library-search").fill("Azure.Provisioning.CostManagement");
    assert.equal(await page.locator("#custom-percent").textContent(), "1.36%");
    assert.equal(await page.locator("#total-lines").textContent(), "8,817");
  }
  assert.deepEqual(errors, []);
  assert.ok(requests.every((url) => url.startsWith("file:")));
  assert.equal(await page.evaluate(() => (globalThis.customCodeMetricsSeed as unknown[]).length), 3);
  console.log(`Three-repository Edge checks passed: ${sources.map((source) =>
    `${source.repository.name} ${source.summary.libraryCount} libraries/${percentage(source.summary.customRatio)}`).join("; ")}.`);
  console.log("Isolated observation/service/history/filter/detail/sort/range restoration, four unavailable states, previous UI removals, keyboard sorting, mobile320/390/dark and zero external requests/page errors passed.");
  const asOfDates = ["2026-09-06", "2026-09-13", "2026-09-20", "2026-09-27", "2026-10-04", "2026-10-11"];
  let backfillPaths: string[];
  let metadataPath: string;
  if (process.env.CUSTOM_CODE_METRICS_BACKFILL_SNAPSHOTS || process.env.CUSTOM_CODE_METRICS_PREVIEW_METADATA) {
    const paths: unknown = JSON.parse(process.env.CUSTOM_CODE_METRICS_BACKFILL_SNAPSHOTS || "null");
    if (!Array.isArray(paths) || paths.length !== 6 || !paths.every((path) => typeof path === "string") ||
      !process.env.CUSTOM_CODE_METRICS_PREVIEW_METADATA) {
      throw new Error("Actual backfill checks require six snapshot paths as a JSON array and the matching preview metadata path.");
    }
    backfillPaths = paths;
    metadataPath = process.env.CUSTOM_CODE_METRICS_PREVIEW_METADATA;
  } else {
    backfillPaths = asOfDates.map((_, index) => fileURLToPath(new URL(`../generated/backfill-${index}.json`, import.meta.url)));
    metadataPath = fileURLToPath(new URL("../generated/backfill-metadata.json", import.meta.url));
    temporaryFiles.push(...backfillPaths, metadataPath);
    const fixtureMetadata: PreviewMetadata = { schemaVersion: "1.0", observations: [] };
    for (let index = 0; index < backfillPaths.length; index++) {
      const fixture = snapshot([
        library("Azure.Identity", index < 3 ? 10 : 20, 35),
        library("Azure.ResourceManager.Sample", 12, 88, "beta", "management"),
        library("Azure.Provisioning.Sample", 5, 5, "beta", "provisioning"),
      ], `2026-10-14T12:0${index}:00Z`, ["2", "3", "3", "4", "5", "6"][index]);
      const bytes = JSON.stringify(fixture);
      await writeFile(backfillPaths[index], bytes);
      fixtureMetadata.observations.push({
        repository: "Azure/azure-sdk-for-net", snapshotId: fixture.snapshotId,
        snapshotSha256: createHash("sha256").update(bytes).digest("hex"), asOf: `${asOfDates[index]}T23:59:59.999Z`,
      });
    }
    await writeFile(metadataPath, JSON.stringify(fixtureMetadata));
  }
  const sourceBytes = await Promise.all(backfillPaths.map((path) => readFile(path)));
  const backfills = sourceBytes.map((bytes) => parseSnapshot(bytes.toString("utf8")));
  assert.ok(backfills.every((value) => value.repository.name === "Azure/azure-sdk-for-net"));
  const metadata = acceptPreviewMetadata(JSON.parse(await readFile(metadataPath, "utf8")), backfills);
  assert.equal(metadata.observations.length, 6);
  const mapped = new Map(metadata.observations.map((entry) => [entry.snapshotId, entry]));
  assert.deepEqual(metadata.observations.map((entry) => entry.asOf.slice(0, 10)).sort(), asOfDates);
  const expectedDates = [...asOfDates, new Date(sources[0].collectedAt).toISOString().slice(0, 10)].sort();
  const buildBackfill = spawnSync(process.execPath, ["--experimental-strip-types", fileURLToPath(new URL("../build.ts", import.meta.url)),
    "--preview", ...[...sourceFiles, ...backfillPaths].flatMap((path) => ["--snapshot", path]), "--preview-metadata", metadataPath],
  { stdio: "inherit" });
  if (buildBackfill.error) throw buildBackfill.error;
  assert.equal(buildBackfill.status, 0);
  await page.goto(pageUrl);
  const latest = backfills.find((value) => mapped.get(value.snapshotId)?.asOf.startsWith("2026-10-11"));
  assert.ok(latest);
  assert.equal(await page.locator("#snapshot-select option").count(), 7);
  assert.equal(await page.locator("#snapshot-select").inputValue(), latest.snapshotId);
  assert.equal(await page.locator("#custom-percent").textContent(), percentage(latest.summary.customRatio));
  assert.equal(await page.locator("#total-lines").textContent(), number.format(latest.summary.totalLines));
  assert.equal(await page.locator("#history-values tr").count(), 7);
  assert.deepEqual((await page.locator("#history-values tr td:first-child").allTextContents()).map((value) => value.slice(0, 10)), expectedDates);
  assert.equal((await page.locator("#history-values tr td:first-child").allTextContents()).filter((value) => value.includes("(reconstructed)")).length, 6);
  assert.match(await page.locator("#history-note").textContent() || "", /6 historical source observations reconstructed using current collector rules, not measurements recorded/);
  for (const source of backfills) {
    await page.locator("#snapshot-select").selectOption(source.snapshotId);
    const entry = mapped.get(source.snapshotId);
    assert.ok(entry);
    assert.equal(await page.locator("#custom-percent").textContent(), percentage(source.summary.customRatio));
    assert.equal(await page.locator("#total-lines").textContent(), number.format(source.summary.totalLines));
    assert.match(await page.locator("#observation-info").textContent() || "", /Source as of.*; reconstructed.*using current collector rules/);
    assert.ok((await page.locator("#observation-info").textContent())?.includes(new Date(source.collectedAt).toISOString()));
    assert.ok((await page.locator("#snapshot-select option:checked").textContent())?.includes(entry.asOf.slice(0, 10)));
    assert.match(await page.locator("#snapshot-select option:checked").textContent() || "", /\(reconstructed\)/);
  }
  await page.locator("#snapshot-select").selectOption(latest.snapshotId);
  for (const range of ["30", "90", "365"]) {
    await page.locator("#history-range").selectOption(range);
    assert.equal(await page.locator("#history-values tr").count(), range === "30" ? 6 : 7);
  }
  const older = backfills.find((value) => mapped.get(value.snapshotId)?.asOf.startsWith("2026-09-20"));
  assert.ok(older);
  await page.locator("#snapshot-select").selectOption(older.snapshotId);
  await page.locator("#history-range").selectOption("30");
  assert.deepEqual((await page.locator("#history-values tr td:first-child").allTextContents()).map((value) => value.slice(0, 10)), asOfDates.slice(0, 3));
  await page.locator("#snapshot-select").selectOption(sources[0].snapshotId);
  await page.locator("#history-range").selectOption("all");
  assert.equal(await page.locator("#custom-percent").textContent(), percentage(sources[0].summary.customRatio));
  assert.equal(await page.locator("#total-lines").textContent(), number.format(sources[0].summary.totalLines));
  assert.doesNotMatch(await page.locator("#observation-info").textContent() || "", /reconstructed|Source as of/);
  assert.doesNotMatch(await page.locator("#snapshot-select option:checked").textContent() || "", /reconstructed/);
  const requestsBeforeSwitch = requests.length;
  for (const source of sources.slice(1)) {
    await page.locator("#repository-select").selectOption(source.repository.name);
    assert.equal(await page.locator("#snapshot-select option").count(), 1);
    assert.equal(await page.locator("#snapshot-select").inputValue(), source.snapshotId);
    assert.equal(await page.locator("#custom-percent").textContent(), percentage(source.summary.customRatio));
    assert.equal(await page.locator("#history-values tr").count(), 1);
    assert.doesNotMatch(await page.locator("#observation-info").textContent() || "", /reconstructed|Source as of/);
    assert.doesNotMatch(await page.locator("#history-note").textContent() || "", /reconstructed/);
  }
  await page.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
  assert.equal(await page.locator("#snapshot-select").inputValue(), sources[0].snapshotId);
  await page.locator("#snapshot-select").selectOption(latest.snapshotId);
  for (const width of [390, 320]) {
    await page.setViewportSize({ width, height: 900 });
    for (const scheme of ["light", "dark"] as const) {
      await page.emulateMedia({ colorScheme: scheme });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      await presentation(page);
    }
  }
  assert.equal(requests.length, requestsBeforeSwitch);
  assert.deepEqual(errors, []);
  const embedded: unknown = await page.evaluate(() => globalThis.customCodeMetricsSeed);
  assert.ok(Array.isArray(embedded));
  assert.equal(embedded.length, 9);
  assert.equal(embedded.filter((value) => acceptSnapshot(value).repository.name === "Azure/azure-sdk-for-net").length, 7);
  for (let index = 0; index < backfillPaths.length; index++) assert.deepEqual(await readFile(backfillPaths[index]), sourceBytes[index]);
  console.log(`${process.env.CUSTOM_CODE_METRICS_BACKFILL_SNAPSHOTS ? "Actual" : "Fixture-only"} six-week reconstruction Edge checks passed: nine seeds, seven .NET points, exact source-date ordering/latest/ranges, six honest labels, original/native baselines unchanged, mobile/dark/removals and zero external requests.`);
} finally {
  await browser?.close();
  for (const path of temporaryFiles) await rm(path, { force: true });
}
