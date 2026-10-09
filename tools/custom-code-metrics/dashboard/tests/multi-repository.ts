import { chromium, type Page } from "@playwright/test";
import assert from "node:assert/strict";
import { readFile, writeFile, rm } from "node:fs/promises";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { acceptSnapshot, aggregate, parseSnapshot, REPOSITORIES, type Snapshot } from "../data.ts";
import { collectSource, type NativeRepositoryName } from "../../collect-source.ts";
import { browserSnapshot } from "./fixtures.ts";

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
} finally {
  await browser?.close();
  for (const path of sourceFiles) await rm(path, { force: true });
}
