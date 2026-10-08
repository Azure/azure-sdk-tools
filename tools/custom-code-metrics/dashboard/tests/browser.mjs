import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import { readFile, writeFile, rm, mkdir } from "node:fs/promises";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { createServer } from "node:http";
import { createHash } from "node:crypto";
import { parseSnapshot, REPOSITORIES } from "../generated/data.mjs";
import { compactObservation } from "../generated/report.mjs";
import { browserSnapshot, library, snapshot } from "./fixtures.mjs";

const pageUrl = new URL("../dist/index.html", import.meta.url).href;
const source = process.env.CUSTOM_CODE_METRICS_SNAPSHOT ?
  parseSnapshot(await readFile(process.env.CUSTOM_CODE_METRICS_SNAPSHOT, "utf8")) : browserSnapshot();
const snapshotPath = fileURLToPath(new URL("../generated/browser-snapshot.json", import.meta.url));
await writeFile(snapshotPath, JSON.stringify(source));
const build = spawnSync(process.execPath, [
  fileURLToPath(new URL("../build.mjs", import.meta.url)), "--snapshot", snapshotPath,
], { stdio: "inherit" });
if (build.error) throw build.error;
if (build.status !== 0) throw new Error("Browser test dashboard build failed.");
const expectedCount = String(source.summary.libraryCount);
const number = new Intl.NumberFormat("en-US");
const percentage = (ratio) => `${(ratio * 100).toFixed(2)}%`;
const luminance = (color) => {
  const channels = color.match(/[\d.]+/g).slice(0, 3).map((value) => {
    const channel = Number(value) / 255;
    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
};
const contrast = (first, second) => {
  const values = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (values[0] + 0.05) / (values[1] + 0.05);
};
async function assertKeyboardScroller(page, region) {
  assert.equal(await region.getAttribute("tabindex"), "0");
  assert.equal(await region.getAttribute("role"), "region");
  assert.ok(await region.getAttribute("aria-label"));
  await region.evaluate((element) => { element.scrollLeft = 0; });
  await page.keyboard.press("Tab");
  await region.focus();
  assert.equal(await region.evaluate((element) => element.matches(":focus-visible")), true);
  await page.keyboard.press("ArrowRight");
  await page.waitForFunction((label) => document.querySelector(`[aria-label="${label}"]`).scrollLeft > 0,
    await region.getAttribute("aria-label"));
}
let browser;
let server;
try {
  browser = await chromium.launch({ channel: "msedge", headless: true });
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
  const errors = [];
  const network = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => network.push(request.url()));
  await page.goto(pageUrl);
  await page.waitForFunction((count) => document.getElementById("library-count")?.textContent === count, expectedCount);
  assert.equal(await page.locator("#custom-percent").textContent(), percentage(source.summary.customRatio));
  assert.match(await page.locator("#contract-info").textContent(), /Schema 3\.0/);
  if (process.env.CUSTOM_CODE_METRICS_SNAPSHOT) {
    await page.locator("#library-search").fill("Azure.Provisioning.CostManagement");
    await page.getByRole("button", { name: "Azure.Provisioning.CostManagement", exact: true }).click();
    assert.equal(await page.locator("#custom-percent").textContent(), "1.36%");
    assert.equal(await page.locator("#total-lines").textContent(), "8,817");
    assert.equal(await page.locator("#detail-heading").textContent(), "Azure.Provisioning.CostManagement");
    assert.match(await page.locator("#detail-stats").textContent(), /1\.36%.*120.*8,697.*8,817/);
    await page.locator("#close-detail").click();
    await page.locator("#history-scope").selectOption("");
    await page.locator("#library-search").fill("");
  }
  assert.equal(await page.locator("#generated-lines").textContent(), number.format(source.summary.generatedLines));
  assert.equal(await page.locator("#preview-notice").count(), 0);
  assert.equal(await page.locator("#freshness").isVisible(), false);
  assert.equal(await page.getByRole("combobox", { name: "Repository", exact: true }).inputValue(), "Azure/azure-sdk-for-net");
  assert.deepEqual(await page.locator("#repository-select option").evaluateAll((options) => options.map((option) => option.value)),
    REPOSITORIES.map((repository) => repository.name));
  assert.deepEqual(await page.locator("#repository-select option").allTextContents(), REPOSITORIES.map((repository) => repository.language));
  assert.equal(await page.locator("#repository-branding, #sort-key, #sort-descending").count(), 0);
  assert.doesNotMatch(await page.locator(".page-header").textContent(), /AZURE SDK\s*\/\s*\.NET\s*\/|Azure\/azure-sdk-for-net/);
  assert.equal(await page.locator("#snapshot-files, #clear-data, input[type=file], .source-actions").count(), 0);
  assert.equal(await page.getByText("Load JSON snapshots", { exact: true }).count(), 0);
  assert.equal(await page.getByRole("button", { name: "Clear data", exact: true }).count(), 0);
  assert.equal(await page.getByText("Unknown", { exact: true }).count(), 0);
  assert.equal(await page.locator("#library-values tr").count(), source.libraries.length);
  assert.equal(await page.locator("#library-table thead button[data-sort]").count(), 6);
  assert.equal(await page.locator("#library-table th[aria-sort='descending']").count(), 1);
  assert.equal(await page.locator("button[data-sort='customLines']").evaluate((button) => button.closest("th").getAttribute("aria-sort")), "descending");
  assert.equal(await page.locator("button[data-sort='customLines'] .sort-indicator").evaluate((element) => {
    const style = getComputedStyle(element);
    return parseFloat(style.borderTopWidth) > 0 && style.opacity === "1";
  }), true);
  assert.deepEqual(await page.locator("#library-values button").allTextContents(), [...source.libraries]
    .sort((a, b) => b.metrics.customLines - a.metrics.customLines || a.library.localeCompare(b.library)).map((library) => library.library));
  assert.match(await page.locator("#history-note").textContent(), /Baseline only/);
  assert.equal(await page.locator("#history-values tr").count(), 1);
  assert.ok(await page.evaluate(() => [...document.querySelectorAll("canvas")].every((canvas) =>
    canvas.getContext("2d").getImageData(0, 0, canvas.width, canvas.height).data.some((value, index) => index % 4 === 3 && value > 0))),
  "Charts did not paint.");
  await page.screenshot({ path: fileURLToPath(new URL("../generated/browser-desktop.png", import.meta.url)) });
  await page.locator("#category-filter").selectOption("management");
  const management = source.categories.find((row) => row.category === "management").metrics;
  assert.equal(await page.locator("#library-count").textContent(), String(management.libraryCount));
  assert.equal(await page.locator("#custom-percent").textContent(), percentage(management.customRatio));
  await page.locator("#category-filter").selectOption("");
  await page.locator("#category-filter").selectOption("provisioning");
  assert.equal(await page.locator("#library-count").textContent(), String(source.categories.find((row) => row.category === "provisioning").metrics.libraryCount));
  await page.locator("#category-filter").selectOption("");
  const service = source.libraries.find((item) => item.library === "Azure.Identity").service;
  await page.locator("#service-filter").selectOption(service);
  assert.equal(await page.locator("#library-count").textContent(), String(source.services.find((item) => item.service === service).metrics.libraryCount));
  await page.locator("#service-filter").selectOption("");
  await page.locator("button[data-sort='library']").click();
  const names = await page.locator("#library-values button").allTextContents();
  assert.deepEqual(names, [...names].sort((a, b) => a.localeCompare(b)));
  assert.equal(await page.locator("button[data-sort='library']").evaluate((button) => button.closest("th").getAttribute("aria-sort")), "ascending");
  assert.match(await page.locator("button[data-sort='library']").getAttribute("aria-label"), /sort descending/);
  await page.locator("button[data-sort='library']").click();
  assert.deepEqual(await page.locator("#library-values button").allTextContents(), [...names].sort((a, b) => b.localeCompare(a)));
  await page.locator("button[data-sort='service']").click();
  assert.deepEqual(await page.locator("#library-values button").allTextContents(), [...source.libraries]
    .sort((a, b) => a.service.localeCompare(b.service) || a.library.localeCompare(b.library)).map((library) => library.library));
  const numericHeader = page.locator("button[data-sort='totalLines']");
  await numericHeader.click();
  assert.deepEqual(await page.locator("#library-values button").allTextContents(), [...source.libraries]
    .sort((a, b) => a.metrics.totalLines - b.metrics.totalLines || a.library.localeCompare(b.library)).map((library) => library.library));
  await numericHeader.focus();
  await page.keyboard.press("Enter");
  assert.equal(await numericHeader.evaluate((button) => button.closest("th").getAttribute("aria-sort")), "descending");
  await page.keyboard.press("Space");
  assert.equal(await numericHeader.evaluate((button) => button.closest("th").getAttribute("aria-sort")), "ascending");
  assert.equal(await page.locator("#library-table th[aria-sort='ascending']").count(), 1);
  assert.equal(await page.locator("#library-table th[aria-sort='none']").count(), 5);
  await page.locator("#breakdown-group").selectOption("service");
  assert.equal(await page.locator("#breakdown-values tr").count(), Math.min(12, source.services.length));
  await page.locator("#breakdown-measure").selectOption("lines");
  await page.locator("#breakdown-group").selectOption("category");
  await page.locator("#library-search").fill("Azure.Identity");
  assert.ok(Number(await page.locator("#library-count").textContent()) > 0);
  await page.getByRole("button", { name: "Azure.Identity", exact: true }).click();
  assert.equal(await page.locator("#detail-heading").textContent(), "Azure.Identity");
  assert.match(await page.locator("#detail-source").textContent(), /No file-level evidence/);
  await page.locator("#library-search").fill("not-an-existing-library");
  assert.equal(await page.locator("#custom-percent").textContent(), "N/A");
  assert.match(await page.locator("#library-values").textContent(), /No libraries match/);
  await page.locator("#library-search").fill("");
  assert.equal(await page.locator("#library-count").textContent(), expectedCount);
  assert.equal(await page.locator("#snapshot-select option").count(), 1);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator("#library-search").fill("Azure.Identity");
  await page.getByRole("button", { name: "Azure.Identity", exact: true }).click();
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  assert.deepEqual(errors, []);
  assert.ok(network.every((url) => url.startsWith("file:")), "The offline dashboard requested an external asset.");
  await page.emulateMedia({ colorScheme: "dark" });
  assert.equal(await page.evaluate(() => matchMedia("(prefers-color-scheme: dark)").matches), true);

  server = createServer(async (request, response) => {
    const name = new URL(request.url, "http://127.0.0.1").pathname.slice(1) || "index.html";
    if (!["index.html", "styles.css", "app.js", "snapshots.js"].includes(name)) {
      response.writeHead(404); response.end(); return;
    }
    response.setHeader("Content-Type", name.endsWith(".html") ? "text/html" : name.endsWith(".css") ? "text/css" : "application/javascript");
    response.end(await readFile(new URL(`../dist/${name}`, import.meta.url)));
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const localUrl = `http://127.0.0.1:${server.address().port}/`;
  const seedConfig = (seeds, indexUrl = "") =>
    `globalThis.customCodeMetricsSeed=${JSON.stringify(seeds)};globalThis.customCodeMetricsIndexUrl=${JSON.stringify(indexUrl)};globalThis.customCodeMetricsPreview=false;`;
  const evidencePage = await browser.newPage({ viewport: { width: 320, height: 844 } });
  evidencePage.on("pageerror", (error) => errors.push(error.message));
  const audited = browserSnapshot();
  audited.libraries[0].files = [
    { path: "sdk/alpha/Azure.Identity/src/Custom.cs", lines: 10, provenance: "custom", evidence: "no-generated-signal" },
    { path: "sdk/alpha/Azure.Identity/src/Generated.cs", lines: 35, provenance: "generated", evidence: "auto-generated-header" },
  ];
  await evidencePage.route(`${localUrl}snapshots.js`, (route) => route.fulfill({
    status: 200, contentType: "application/javascript", body: seedConfig([audited]),
  }));
  await evidencePage.goto(localUrl);
  await evidencePage.getByRole("button", { name: "Azure.Identity", exact: true }).click();
  await evidencePage.locator("#file-evidence summary").click();
  await assertKeyboardScroller(evidencePage, evidencePage.getByRole("region", { name: "Scrollable file provenance evidence" }));
  const naPage = await browser.newPage();
  naPage.on("pageerror", (error) => errors.push(error.message));
  const nullable = snapshot([...browserSnapshot().libraries, library("Azure.Empty", 0)]);
  await naPage.route(`${localUrl}snapshots.js`, (route) => route.fulfill({
    status: 200, contentType: "application/javascript", body: seedConfig([nullable]),
  }));
  await naPage.goto(localUrl);
  await naPage.waitForFunction(() => document.querySelectorAll("#library-values button").length === 4);
  await naPage.locator("button[data-sort='customRatio']").click();
  assert.equal(await naPage.locator("#library-values button").last().textContent(), "Azure.Empty");
  await naPage.locator("button[data-sort='customRatio']").click();
  assert.equal(await naPage.locator("#library-values button").last().textContent(), "Azure.Empty", "Descending sorting moved N/A ahead of measured ratios.");
  await naPage.locator("button[data-sort='service']").click();
  await naPage.locator("button[data-sort='service']").click();
  assert.deepEqual(await naPage.locator("#library-values button").allTextContents(), [...nullable.libraries]
    .sort((a, b) => b.service.localeCompare(a.service) || a.library.localeCompare(b.library)).map((library) => library.library));
  const first = browserSnapshot();
  const second = snapshot([
    library("Azure.Identity", 8, 37),
    library("Azure.ResourceManager.Sample", 12, 88, "beta", "management"),
  ], "2026-10-04T12:00:00Z", "2");
  const fixturePage = await browser.newPage();
  fixturePage.on("pageerror", (error) => errors.push(error.message));
  await fixturePage.route(`${localUrl}snapshots.js`, (route) => route.fulfill({
    status: 200, contentType: "application/javascript", body: seedConfig([first, second]),
  }));
  await fixturePage.goto(localUrl);
  await fixturePage.waitForFunction(() => document.querySelectorAll("#history-values tr").length === 2);
  assert.match(await fixturePage.locator("#history-note").textContent(), /2 measured revisions.*Fixed cohort: 2/);
  await fixturePage.locator("#fixed-cohort").uncheck();
  assert.match(await fixturePage.locator("#history-values tr").last().textContent(), /\+0 \/ -1/);
  await fixturePage.locator("#history-scope").selectOption("Azure.Identity");
  assert.equal(await fixturePage.locator("#history-values tr").last().locator("td").nth(2).textContent(), "1");
  const dirty = snapshot(first.libraries, "2026-10-05T12:00:00Z", "3", true);
  const partial = snapshot(first.libraries, "2026-10-06T12:00:00Z", "5");
  await fixturePage.route("https://metrics.invalid/**", async (route) => {
    const url = route.request().url();
    assert.equal(route.request().method(), "GET");
    assert.equal(route.request().headers().cookie, undefined);
    let value = first;
    if (url.endsWith("/dirty/index.json")) value = { snapshots: ["dirty.json"] };
    else if (url.endsWith("/dirty.json")) value = dirty;
    else if (url.endsWith("/broken/index.json")) value = { snapshots: ["valid.json", "broken.json"] };
    else if (url.endsWith("/valid.json")) value = partial;
    else if (url.endsWith("/v2/index.json")) value = { snapshots: ["v2.json"] };
    else if (url.endsWith("/v2.json")) value = { ...first, schemaVersion: "2.0" };
    else if (url.endsWith("index.json")) value = { snapshots: ["one.json"] };
    await route.fulfill({
      status: 200, contentType: "application/json", headers: { "Access-Control-Allow-Origin": "*" },
      body: url.endsWith("/broken.json") ? "{broken" : JSON.stringify(value),
    });
  });
  await fixturePage.locator(".index-loader summary").click();
  await fixturePage.locator("#index-url").fill("https://metrics.invalid/dirty/index.json");
  await fixturePage.locator("#load-index").click();
  await fixturePage.waitForFunction(() => document.getElementById("checkout-badge")?.textContent === "DIRTY CHECKOUT");
  assert.match(await fixturePage.locator("#history-note").textContent(), /1 dirty observations excluded/);
  await fixturePage.locator("#include-dirty").check();
  assert.equal(await fixturePage.locator("#history-values tr").count(), 3);
  await fixturePage.locator("#index-url").fill("https://metrics.invalid/broken/index.json");
  await fixturePage.locator("#load-index").click();
  await fixturePage.waitForFunction(() => document.getElementById("status")?.classList.contains("error"));
  assert.equal(await fixturePage.locator("#status").isVisible(), true);
  assert.equal(await fixturePage.locator("#snapshot-select option").count(), 3, "A partially valid index changed existing observations.");
  await fixturePage.locator("#index-url").fill("https://metrics.invalid/v2/index.json");
  await fixturePage.locator("#load-index").click();
  await fixturePage.waitForFunction(() => document.getElementById("status")?.textContent?.includes("Invalid snapshot"));
  assert.equal(await fixturePage.locator("#status").isVisible(), true);
  assert.equal(await fixturePage.locator("#snapshot-select option").count(), 3);
  assert.equal(await fixturePage.locator("#history-values tr").count(), 3, "Old v2 data became a false v3 trend point.");
  await fixturePage.locator("#index-url").fill("https://metrics.invalid/history/index.json");
  await fixturePage.locator("#load-index").click();
  await fixturePage.waitForFunction(() => document.getElementById("status")?.textContent?.startsWith("Published index:"));
  assert.equal(await fixturePage.locator("#snapshot-select option").count(), 3);
  await fixturePage.locator("#repository-select").selectOption("Azure/azure-sdk-for-java");
  await fixturePage.locator("#index-form").evaluate((form) => form.requestSubmit());
  await fixturePage.waitForFunction(() => document.getElementById("status")?.classList.contains("error"));
  assert.match(await fixturePage.locator("#status").textContent(), /repository does not match/);
  assert.equal(await fixturePage.locator("#measurements").isVisible(), false);
  await fixturePage.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
  assert.equal(await fixturePage.locator("#snapshot-select option").count(), 3);
  assert.deepEqual(errors, []);
  const older = snapshot(first.libraries, "2026-05-01T12:00:00Z", "4");
  const monthDocument = (observations) => {
    const month = observations[0].collectedAt.slice(0, 7);
    const body = JSON.stringify({ schemaVersion: "1.0", month, observations: observations.map(compactObservation) });
    return { body, month, path: `history/${createHash("sha256").update(body).digest("hex")}/${month}.json` };
  };
  const october = monthDocument([first, second]);
  const may = monthDocument([older]);
  const hostedIndex = { schemaVersion: "1.0", latest: `snapshots/${second.snapshotId}.json`,
    history: [may, october].map(({ month, path }) => ({ month, path })) };
  const publicIndex = "https://metrics.invalid/hosted/index.json";
  const hosted = await browser.newPage();
  await hosted.clock.setFixedTime(new Date("2026-10-07T12:00:00Z"));
  hosted.on("pageerror", (error) => errors.push(error.message));
  const hostedRequests = [];
  let failOlder = true;
  await hosted.route(`${localUrl}snapshots.js`, (route) => route.fulfill({
    status: 200, contentType: "application/javascript", body: seedConfig([], publicIndex),
  }));
  await hosted.route("https://metrics.invalid/**", async (route) => {
    const url = route.request().url();
    hostedRequests.push(url);
    assert.equal(route.request().headers().cookie, undefined);
    assert.equal(route.request().headers().authorization, undefined);
    let body; let status = 200;
    if (url === publicIndex) body = JSON.stringify(hostedIndex);
    else if (url.endsWith(hostedIndex.latest)) body = JSON.stringify(second);
    else if (url.endsWith(october.path)) body = october.body;
    else if (url.endsWith(may.path) && !failOlder) body = may.body;
    else if (url === "https://metrics.invalid/older/index.json") body = JSON.stringify({ snapshots: ["older.json"] });
    else if (url.endsWith("/older.json")) body = JSON.stringify(older);
    else { status = 503; body = "Unavailable"; }
    await route.fulfill({ status, body, contentType: "application/json", headers: { "Access-Control-Allow-Origin": "*" } });
  });
  await hosted.goto(localUrl);
  await hosted.waitForFunction(() => document.getElementById("status")?.textContent?.startsWith("Published index:"));
  assert.equal(await hosted.locator("#library-count").textContent(), "2");
  assert.equal(await hosted.locator("#snapshot-select option").count(), 1);
  assert.equal(await hosted.locator("#history-values tr").count(), 2);
  assert.equal(await hosted.locator("#history-range").inputValue(), "90");
  assert.equal(hostedRequests.length, 3, "Startup downloaded the full archive.");
  assert.match(await hosted.locator("#freshness").textContent(), /STALE/);
  assert.equal(await hosted.locator("#history-scope option[value='Azure.Provisioning.Sample']").count(), 1);
  await hosted.locator("#history-range").selectOption("365");
  await hosted.waitForFunction(() => document.getElementById("status")?.classList.contains("error"));
  assert.equal(await hosted.locator("#history-range").inputValue(), "90");
  assert.equal(await hosted.locator("#history-values tr").count(), 2);
  assert.equal(await hosted.locator("#library-count").textContent(), "2");
  failOlder = false;
  await hosted.locator("#history-range").selectOption("365");
  await hosted.waitForFunction(() => document.querySelectorAll("#history-values tr").length === 3);
  const beforeCached = hostedRequests.length;
  await hosted.locator("#history-range").selectOption("30");
  await hosted.waitForFunction(() => document.getElementById("status")?.textContent?.includes("30-day history loaded"));
  assert.equal(await hosted.locator("#history-values tr").count(), 2);
  assert.equal(hostedRequests.length, beforeCached, "Cached history was fetched again.");
  await hosted.locator(".index-loader summary").click();
  await hosted.locator("#index-url").fill("https://metrics.invalid/older/index.json");
  await hosted.locator("#load-index").click();
  await hosted.waitForFunction(() => document.querySelectorAll("#snapshot-select option").length === 2);
  const beforeSelection = hostedRequests.length;
  await hosted.locator("#snapshot-select").selectOption(older.snapshotId);
  await hosted.waitForFunction(() => document.getElementById("status")?.textContent?.includes("selected observation's UTC date"));
  assert.equal(await hosted.locator("#history-values tr").count(), 1);
  assert.match(await hosted.locator("#history-values tr").textContent(), /2026-05-01/);
  assert.equal(await hosted.locator("#library-count").textContent(), "3");
  assert.equal(hostedRequests.length, beforeSelection, "Historical selection did not reuse its cached month.");
  await hosted.locator("#index-url").fill("https://metrics.invalid/unavailable/index.json");
  await hosted.locator("#load-index").click();
  await hosted.waitForFunction(() => document.getElementById("status")?.classList.contains("error"));
  assert.equal(await hosted.locator("#snapshot-select option").count(), 2);
  assert.equal(await hosted.locator("#snapshot-select").inputValue(), older.snapshotId);
  assert.equal(await hosted.locator("#library-count").textContent(), "3");
  assert.equal(await hosted.locator("#freshness").isVisible(), true);
  assert.equal(await hosted.locator("#history-range option[value='all']").evaluate((option) => option.disabled), true);
  const beforeRepositorySwitch = hostedRequests.length;
  await hosted.locator("#repository-select").selectOption("Azure/azure-sdk-for-go");
  assert.equal(await hosted.locator("#measurements").isVisible(), false);
  assert.equal(await hosted.locator("#freshness").isVisible(), false);
  await hosted.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
  assert.equal(await hosted.locator("#snapshot-select").inputValue(), older.snapshotId);
  assert.equal(await hosted.locator("#library-count").textContent(), "3");
  assert.equal(await hosted.locator("#history-range").inputValue(), "30");
  assert.equal(hostedRequests.length, beforeRepositorySwitch, "Repository selection fetched an unconfigured feed.");
  assert.deepEqual(errors, []);
  const previewBuild = spawnSync(process.execPath, [
    fileURLToPath(new URL("../build.mjs", import.meta.url)), "--preview", "--snapshot", snapshotPath,
  ], { stdio: "inherit" });
  if (previewBuild.error) throw previewBuild.error;
  assert.equal(previewBuild.status, 0, "Static preview build failed.");
  const previewPage = await browser.newPage();
  const previewRequests = [];
  previewPage.on("pageerror", (error) => errors.push(error.message));
  previewPage.on("request", (request) => previewRequests.push(request.url()));
  const boundary = Date.parse(source.collectedAt) + 36 * 3600000;
  await previewPage.clock.setFixedTime(new Date(boundary));
  await previewPage.goto(pageUrl);
  await previewPage.waitForFunction((count) => document.getElementById("library-count")?.textContent === count, expectedCount);
  assert.equal(await previewPage.locator("#custom-percent").textContent(), percentage(source.summary.customRatio));
  assert.equal(await previewPage.locator("#service-count").textContent(), `${source.services.length} services`);
  assert.equal(await previewPage.locator("#generated-lines").textContent(), number.format(source.summary.generatedLines));
  assert.equal(await previewPage.locator("#preview-notice").count(), 0);
  const deploymentMetadata = await previewPage.locator("#deployment-info").textContent();
  assert.match(deploymentMetadata, /Embedded baseline.*No automatic feed/);
  assert.ok((await previewPage.locator("#observation-info").textContent()).includes(new Date(source.collectedAt).toISOString()));
  assert.equal(await previewPage.locator("#freshness").isVisible(), false);
  await previewPage.clock.setFixedTime(new Date(boundary + 1));
  await previewPage.reload();
  await previewPage.waitForFunction((count) => document.getElementById("library-count")?.textContent === count, expectedCount);
  assert.equal(await previewPage.locator("#freshness").isVisible(), false);
  assert.doesNotMatch(await previewPage.locator("body").textContent(),
    /Static preview - embedded baseline|Static preview baseline:|Static preview selected observation|STALE: embedded data/);
  assert.equal(await previewPage.locator("#status").textContent(), "");
  assert.match(await previewPage.locator("#history-note").textContent(), /Baseline only.*No code-change trend yet/);
  assert.equal(await previewPage.locator("#history-values tr").count(), 1);
  await previewPage.locator("button[data-sort='totalLines']").click();
  await previewPage.locator("#category-filter").selectOption("management");
  await previewPage.locator("#history-range").selectOption("365");
  const selectedLibrary = source.libraries.find((library) => library.category === "management").library;
  await previewPage.getByRole("button", { name: selectedLibrary, exact: true }).click();
  const beforeRepositoryRequests = previewRequests.length;
  for (const repository of REPOSITORIES.slice(1)) {
    await previewPage.locator("#repository-select").selectOption(repository.name);
    assert.equal(await previewPage.getByRole("combobox", { name: "Repository", exact: true }).inputValue(), repository.name);
    assert.equal(await previewPage.locator("#repository-state").isVisible(), true);
    const state = await previewPage.locator("#repository-state").textContent();
    assert.ok(state.includes(repository.name));
    assert.match(state, /No observations collected.*collector is not implemented/);
    assert.equal(await previewPage.locator("#repository-branding").count(), 0);
    assert.equal(await previewPage.locator("#measurements").isVisible(), false);
    for (const id of ["custom-percent", "library-count", "history-chart", "library-values", "library-detail",
      "observation-info", "deployment-info", "freshness", "index-loader"]) {
      assert.equal(await previewPage.locator(`#${id}`).isVisible(), false, `${repository.name} exposed .NET ${id}.`);
    }
    assert.equal(previewRequests.length, beforeRepositoryRequests, `${repository.name} requested a nonexistent feed.`);
  }
  await previewPage.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
  assert.equal(await previewPage.locator("#category-filter").inputValue(), "management");
  assert.equal(await previewPage.locator("#history-range").inputValue(), "365");
  assert.equal(await previewPage.locator("#history-scope").inputValue(), selectedLibrary);
  assert.equal(await previewPage.locator("#detail-heading").textContent(), selectedLibrary);
  assert.equal(await previewPage.locator("#library-detail").isVisible(), true);
  assert.equal(await previewPage.locator("#measurements").isVisible(), true);
  assert.equal(await previewPage.locator("#repository-state").isVisible(), false);
  assert.equal(await previewPage.locator("#library-count").textContent(), String(management.libraryCount));
  assert.equal(await previewPage.locator("#snapshot-select").inputValue(), source.snapshotId);
  assert.equal(await previewPage.locator("#deployment-info").textContent(), deploymentMetadata);
  assert.equal(await previewPage.locator("#freshness").isVisible(), false);
  assert.equal(await previewPage.locator("button[data-sort='totalLines']").evaluate((button) => button.closest("th").getAttribute("aria-sort")), "ascending");
  assert.deepEqual(await previewPage.locator("#library-values button").allTextContents(), source.libraries
    .filter((library) => library.category === "management")
    .sort((a, b) => a.metrics.totalLines - b.metrics.totalLines || a.library.localeCompare(b.library)).map((library) => library.library));
  assert.equal(previewRequests.length, beforeRepositoryRequests);
  await previewPage.locator("#category-filter").selectOption("");
  await previewPage.locator("#history-scope").selectOption("");
  await previewPage.locator("#close-detail").click();
  assert.equal(await previewPage.locator("#library-count").textContent(), expectedCount);
  const mobilePreview = await browser.newPage({ viewport: { width: 390, height: 844 } });
  mobilePreview.on("pageerror", (error) => errors.push(error.message));
  await mobilePreview.goto(pageUrl);
  await mobilePreview.waitForFunction((count) => document.getElementById("library-count")?.textContent === count, expectedCount);
  assert.equal(await mobilePreview.locator("#library-values tr").count(), source.libraries.length);
  assert.equal(await mobilePreview.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true,
    "The full portfolio overflowed an initially mobile viewport.");
  await previewPage.locator("#history-range").selectOption("365");
  await previewPage.locator("#library-search").fill("Azure.Identity");
  await previewPage.getByRole("button", { name: "Azure.Identity", exact: true }).click();
  assert.match(await previewPage.locator("#detail-source").textContent(), /No file-level evidence/);
  await previewPage.locator("#library-search").fill("");
  assert.equal(await previewPage.locator("#library-values tr").count(), source.libraries.length);
  assert.equal(await previewPage.locator("#library-detail").isVisible(), true);
  assert.equal(await previewPage.locator("button[data-sort='totalLines']").evaluate((button) => button.closest("th").getAttribute("aria-sort")), "ascending");
  for (let retry = 0; retry < 3; retry++) {
    await previewPage.setViewportSize({ width: 390, height: 844 });
    assert.equal(await previewPage.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true,
      "The full portfolio with open library detail overflowed immediately after resizing.");
    assert.equal(await previewPage.evaluate(() => [...document.querySelectorAll(".chart-container canvas")].every((element) =>
      element.getBoundingClientRect().width <= element.parentElement.getBoundingClientRect().width)), true,
    "A chart retained its desktop width outside the mobile container.");
    await previewPage.setViewportSize({ width: 1280, height: 1000 });
    await previewPage.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
  }
  assert.equal(await previewPage.locator("#deployment-info").textContent(), deploymentMetadata);
  assert.equal(await previewPage.locator("#freshness").isVisible(), false);
  assert.equal(await previewPage.locator("#snapshot-files, #clear-data, input[type=file]").count(), 0);
  assert.equal(await previewPage.getByText("Load JSON snapshots", { exact: true }).count(), 0);
  assert.equal(await previewPage.getByRole("button", { name: "Clear data", exact: true }).count(), 0);
  assert.equal(await previewPage.locator("#snapshot-select option").count(), 1);
  assert.ok(previewRequests.every((url) => url.startsWith("file:")), "Static preview requested a feed or external asset.");
  const visual = await browser.newPage({ viewport: { width: 1440, height: 1100 }, colorScheme: "light" });
  const visualRequests = [];
  visual.on("pageerror", (error) => errors.push(error.message));
  visual.on("request", (request) => visualRequests.push(request.url()));
  await visual.goto(pageUrl);
  await visual.waitForFunction((count) => document.getElementById("library-count")?.textContent === count, expectedCount);
  await visual.keyboard.press("Tab");
  assert.equal(await visual.evaluate(() => document.activeElement.id), "repository-select");
  assert.equal(await visual.locator("#repository-select").evaluate((element) => {
    const style = getComputedStyle(element);
    return element.matches(":focus-visible") && parseFloat(style.outlineWidth) >= 3 && style.outlineStyle === "solid";
  }), true, "Keyboard repository selection has no visible focus ring.");
  const focus = await visual.locator("#repository-select").evaluate((element) => ({
    outline: getComputedStyle(element).outlineColor, background: getComputedStyle(element).backgroundColor,
  }));
  assert.ok(contrast(focus.outline, focus.background) >= 3, "Keyboard focus has insufficient contrast.");
  const screenshots = process.env.CUSTOM_CODE_METRICS_SCREENSHOT_DIRECTORY;
  if (screenshots) await mkdir(screenshots, { recursive: true });
  for (const theme of ["light", "dark"]) {
    await visual.emulateMedia({ colorScheme: theme });
    for (const width of [1440, 1280, 390, 320]) {
      await visual.setViewportSize({ width, height: 1000 });
      const layout = await visual.evaluate(() => {
        const cards = [...document.querySelectorAll(".kpi")].map((card) => card.getBoundingClientRect());
        const scroller = document.querySelector(".library-scroll");
        return {
          bodyFont: parseFloat(getComputedStyle(document.body).fontSize),
          pageFits: document.documentElement.scrollWidth <= innerWidth,
          widths: cards.map((card) => card.width),
          heights: cards.map((card) => card.height),
          metricTop: document.getElementById("custom-percent").getBoundingClientRect().top,
          tableFont: parseFloat(getComputedStyle(document.querySelector("#library-values td")).fontSize),
          tabularNumbers: getComputedStyle(document.getElementById("custom-percent")).fontVariantNumeric,
          packageWrap: getComputedStyle(document.querySelector(".library-link")).whiteSpace,
          firstColumn: document.querySelector("#library-values td").getBoundingClientRect().width,
          scrollsInternally: scroller.scrollWidth > scroller.clientWidth && getComputedStyle(scroller).overflowX === "auto",
          statusHidden: document.getElementById("status").hidden,
          foreground: getComputedStyle(document.body).color,
          background: getComputedStyle(document.body).backgroundColor,
          secondary: getComputedStyle(document.getElementById("observation-info")).color,
          surface: getComputedStyle(document.querySelector(".kpi")).backgroundColor,
          custom: getComputedStyle(document.getElementById("custom-percent")).color,
          generated: getComputedStyle(document.getElementById("generated-lines")).color,
        };
      });
      assert.ok(layout.bodyFont >= 14 && layout.tableFont >= 14, "Dashboard or table typography is too small.");
      assert.ok(contrast(layout.foreground, layout.background) >= 4.5 && contrast(layout.secondary, layout.background) >= 4.5,
        `${theme} body or secondary text has insufficient contrast.`);
      assert.ok(contrast(layout.custom, layout.surface) >= 3 && contrast(layout.generated, layout.surface) >= 3,
        `${theme} large metric values have insufficient contrast.`);
      assert.equal(layout.pageFits, true, `${theme} ${width}px page overflowed.`);
      assert.equal(layout.tabularNumbers, "tabular-nums");
      assert.equal(layout.packageWrap, "nowrap", "Package names fragment into tiny wrapped lines.");
      assert.ok(layout.firstColumn >= 300);
      assert.equal(layout.statusHidden, true, "Normal loaded status duplicates the collection/freshness context.");
      if (width >= 1280) {
        assert.ok(Math.max(...layout.widths) - Math.min(...layout.widths) <= 1, "Summary cards have uneven widths.");
        assert.ok(Math.max(...layout.heights) - Math.min(...layout.heights) <= 1, "Summary cards have uneven heights.");
        assert.ok(layout.metricTop < 450, "Repeated context pushes the overview below the first desktop screen.");
      } else {
        assert.equal(layout.scrollsInternally, true, "Narrow library tables must scroll inside their container.");
      }
      assert.equal(await visual.locator("#custom-percent").textContent(), percentage(source.summary.customRatio));
      assert.equal(await visual.locator("#snapshot-files, #clear-data, input[type=file]").count(), 0);
      await visual.waitForFunction(() => [...document.querySelectorAll("canvas")].every((canvas) =>
        canvas.getContext("2d").getImageData(0, 0, canvas.width, canvas.height).data.some((value, index) => index % 4 === 3 && value > 0)));
      if (screenshots) await visual.screenshot({ path: join(screenshots, `visual-${theme}-${width}.png`), fullPage: true });
    }
    await visual.locator("#repository-select").selectOption("Azure/azure-sdk-for-cpp");
    assert.equal(await visual.locator("#repository-empty").isVisible(), true);
    assert.equal(await visual.locator("#measurements").isVisible(), false);
    assert.equal(await visual.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    if (screenshots) await visual.screenshot({ path: join(screenshots, `visual-empty-${theme}.png`) });
    await visual.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
    assert.equal(await visual.locator("#library-count").textContent(), expectedCount);
  }
  for (const name of ["Scrollable library measurements", "Scrollable provenance breakdown", "Scrollable historical observations"]) {
    await assertKeyboardScroller(visual, visual.getByRole("region", { name }));
  }
  assert.ok(visualRequests.every((url) => url.startsWith("file:")), "The visual refresh introduced external assets or telemetry.");
  assert.deepEqual(errors, []);
  console.log("Browser checks passed: baseline, category/service filters, sorting, charts, library details, seeded cohorts, atomic index error retention, dirty exclusion, responsive and dark layouts, no file-import/clear controls or unsolicited external requests.");
  console.log("Hosted checks passed: automatic latest, anonymous bounded month loading, historical membership, stale warning, failed range/index rollback and cache reuse.");
  console.log("Preview checks passed: actual seed counts/ratio/date, quiet baseline metadata, all three banners removed, no feed requests/fake trend, full-portfolio initial mobile and immediate open-detail resize.");
  console.log("Repository checks passed: seven choices, six honest uncollected states without .NET measurements/fetches, .NET filter/history/detail restoration and mismatched-index rejection.");
  console.log("Visual checks passed: aligned desktop cards, compact context, 15px body/14px tables, light/dark 1440/1280/390/320px, painted charts, keyboard focus/scrolling, unfragmented package names and bounded page widths.");
  console.log("Sort-header checks passed: six native column controls, default custom-lines descending, lexical/numeric sorting, stable ties, Enter/Space, aria-sort/direction, N/A last both ways and filter/repository/detail state preservation.");
} finally {
  await browser?.close();
  if (server) await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
  await rm(snapshotPath, { force: true });
  await rm(new URL("../generated/browser-desktop.png", import.meta.url), { force: true });
}
