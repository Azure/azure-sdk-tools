import { chromium, type Page, type Locator, type Browser } from "@playwright/test";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import { readFile, writeFile, rm, mkdir } from "node:fs/promises";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { createServer, type Server } from "node:http";
import { createHash } from "node:crypto";
import { parseSnapshot, REPOSITORIES, type Snapshot, type Observation } from "../data.ts";
import { compactObservation } from "../report.ts";
import { browserSnapshot, legacySnapshot, library, snapshot } from "./fixtures.ts";

const pageUrl = new URL("../dist/index.html", import.meta.url).href;
const source = process.env.CUSTOM_CODE_METRICS_SNAPSHOT ?
  parseSnapshot(await readFile(process.env.CUSTOM_CODE_METRICS_SNAPSHOT, "utf8")) : browserSnapshot();
const snapshotPath = fileURLToPath(new URL("../generated/browser-snapshot.json", import.meta.url));
await writeFile(snapshotPath, JSON.stringify(source));
const build = spawnSync(process.execPath, [
  "--experimental-strip-types", fileURLToPath(new URL("../build.ts", import.meta.url)), "--snapshot", snapshotPath,
], { stdio: "inherit" });
if (build.error) throw build.error;
if (build.status !== 0) throw new Error("Browser test dashboard build failed.");
const expectedCount = String(source.summary.libraryCount);
const number = new Intl.NumberFormat("en-US");
const percentage = (ratio: number | null) => ratio === null ? "N/A" : `${(ratio * 100).toFixed(2)}%`;
const luminance = (color: string) => {
  const values = color.match(/[\d.]+/g);
  assert.ok(values, `Invalid CSS color: ${color}`);
  const channels = values.slice(0, 3).map((value) => {
    const channel = Number(value) / 255;
    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
};
const contrast = (first: string, second: string) => {
  const values = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (values[0] + 0.05) / (values[1] + 0.05);
};
async function text(locator: Locator): Promise<string> {
  const value = await locator.textContent();
  assert.notEqual(value, null);
  return value!;
}
async function attribute(locator: Locator, name: string): Promise<string> {
  const value = await locator.getAttribute(name);
  assert.notEqual(value, null);
  return value!;
}
async function assertSourcePresentation(page: Page): Promise<void> {
  assert.equal(await page.locator("#checkout-badge,#include-dirty,#index-loader,#index-form,#index-url,#load-index,#data-context,input[type=url]").count(), 0);
  const labels = await page.locator("[title], [aria-label]").evaluateAll((elements) =>
    elements.map((element) => `${element.getAttribute("title") || ""} ${element.getAttribute("aria-label") || ""}`).join("\n"));
  assert.doesNotMatch(`${await text(page.locator("body"))}\n${labels}`,
    /\binferred\b|\b(?:clean|dirty) checkouts?\b|\binclude\s*dirty\b|\(dirty\)|\bdirty observations\b|Load a published snapshot index|Index URL|Load index/i);
}
async function assertKeyboardScroller(page: Page, region: Locator): Promise<void> {
  assert.equal(await attribute(region, "tabindex"), "0");
  assert.equal(await attribute(region, "role"), "region");
  assert.ok(await attribute(region, "aria-label"));
  await region.evaluate((element) => { element.scrollLeft = 0; });
  await page.keyboard.press("Tab");
  await region.focus();
  assert.equal(await region.evaluate((element) => element.matches(":focus-visible")), true);
  await page.keyboard.press("ArrowRight");
  await page.waitForFunction((label) => document.querySelector<HTMLElement>(`[aria-label="${label}"]`)!.scrollLeft > 0,
    await attribute(region, "aria-label"));
}
let browser: Browser | undefined;
let server: Server | undefined;
try {
  browser = await chromium.launch({ channel: "msedge", headless: true });
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
  const errors: string[] = [];
  const network: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => network.push(request.url()));
  await page.goto(pageUrl);
  await page.waitForFunction((count) => document.querySelector<HTMLElement>("#library-count")!?.textContent === count, expectedCount);
  await assertSourcePresentation(page);
  assert.equal(await text(page.locator(".kpi.featured > p")), "Custom source");
  assert.equal(await text(page.locator(".chart-panel[aria-labelledby='history-heading'] .legend")), "Custom source");
  await page.locator(".measurement-note summary").click();
  assert.match(await text(page.locator(".measurement-note p")), /Source classification uses file-level signals.*not a measure of debt or API impact/);
  await assertSourcePresentation(page);
  await page.locator(".measurement-note summary").click();
  assert.equal(await text(page.locator("#custom-percent")), percentage(source.summary.customRatio));
  assert.match(await text(page.locator("#contract-info")), /Schema 1\.0/);
  if (process.env.CUSTOM_CODE_METRICS_SNAPSHOT) {
    await page.locator("#library-search").fill("Azure.Provisioning.CostManagement");
    await page.getByRole("button", { name: "Azure.Provisioning.CostManagement", exact: true }).click();
    assert.equal(await text(page.locator("#custom-percent")), "1.36%");
    assert.equal(await text(page.locator("#total-lines")), "8,817");
    assert.equal(await text(page.locator("#detail-heading")), "Azure.Provisioning.CostManagement");
    assert.match(await text(page.locator("#detail-stats")), /1\.36%.*120.*8,697.*8,817/);
    await page.locator("#close-detail").click();
    await page.locator("#history-scope").selectOption("");
    await page.locator("#library-search").fill("");
  }
  assert.equal(await text(page.locator("#generated-lines")), number.format(source.summary.generatedLines));
  assert.equal(await page.locator("#preview-notice").count(), 0);
  assert.equal(await page.locator("#freshness").isVisible(), false);
  assert.equal(await page.getByRole("combobox", { name: "Repository", exact: true }).inputValue(), "Azure/azure-sdk-for-net");
  assert.deepEqual(await page.locator("#repository-select option").evaluateAll((options) => options.map((option) => {
    if (!(option instanceof HTMLOptionElement)) throw new Error("Repository choices must be options.");
    return option.value;
  })),
    REPOSITORIES.map((repository) => repository.name));
  assert.deepEqual(await page.locator("#repository-select option").allTextContents(), REPOSITORIES.map((repository) => repository.language));
  assert.equal(await page.locator("#repository-branding, #sort-key, #sort-descending").count(), 0);
  assert.doesNotMatch(await text(page.locator(".page-header")), /AZURE SDK\s*\/\s*\.NET\s*\/|Azure\/azure-sdk-for-net/);
  assert.equal(await page.locator("#snapshot-files, #clear-data, input[type=file], .source-actions").count(), 0);
  assert.equal(await page.getByText("Load JSON snapshots", { exact: true }).count(), 0);
  assert.equal(await page.getByRole("button", { name: "Clear data", exact: true }).count(), 0);
  assert.equal(await page.getByText("Unknown", { exact: true }).count(), 0);
  assert.equal(await page.locator("#library-values tr").count(), source.libraries.length);
  assert.equal(await page.locator("#library-table thead button[data-sort]").count(), 6);
  assert.equal(await page.locator("#library-table th[aria-sort='descending']").count(), 1);
  assert.equal(await page.locator("button[data-sort='customLines']").evaluate((button) => button.closest("th")!.getAttribute("aria-sort")), "descending");
  assert.equal(await page.locator("button[data-sort='customLines'] .sort-indicator").evaluate((element) => {
    const style = getComputedStyle(element);
    return parseFloat(style.borderTopWidth) > 0 && style.opacity === "1";
  }), true);
  assert.deepEqual(await page.locator("#library-values button").allTextContents(), [...source.libraries]
    .sort((a, b) => b.metrics.customLines - a.metrics.customLines || a.library.localeCompare(b.library)).map((library) => library.library));
  assert.match(await text(page.locator("#history-note")), /Baseline only/);
  assert.equal(await page.locator("#history-values tr").count(), 1);
  assert.ok(await page.evaluate(() => [...document.querySelectorAll<HTMLCanvasElement>("canvas")].every((canvas) =>
    canvas.getContext("2d")!.getImageData(0, 0, canvas.width, canvas.height).data.some((value, index) => index % 4 === 3 && value > 0))),
  "Charts did not paint.");
  await page.screenshot({ path: fileURLToPath(new URL("../generated/browser-desktop.png", import.meta.url)) });
  await page.locator("#category-filter").selectOption("management");
  await assertSourcePresentation(page);
  const management = source.categories.find((row) => row.category === "management")!.metrics;
  assert.equal(await text(page.locator("#library-count")), String(management.libraryCount));
  assert.equal(await text(page.locator("#custom-percent")), percentage(management.customRatio));
  await page.locator("#category-filter").selectOption("");
  await page.locator("#category-filter").selectOption("provisioning");
  assert.equal(await text(page.locator("#library-count")), String(source.categories.find((row) => row.category === "provisioning")!.metrics.libraryCount));
  await page.locator("#category-filter").selectOption("");
  const service = source.libraries.find((item) => item.library === "Azure.Identity")!.service;
  await page.locator("#service-filter").selectOption(service);
  assert.equal(await text(page.locator("#library-count")), String(source.services.find((item) => item.service === service)!.metrics.libraryCount));
  await page.locator("#service-filter").selectOption("");
  await page.locator("button[data-sort='library']").click();
  const names = await page.locator("#library-values button").allTextContents();
  assert.deepEqual(names, [...names].sort((a, b) => a.localeCompare(b)));
  assert.equal(await page.locator("button[data-sort='library']").evaluate((button) => button.closest("th")!.getAttribute("aria-sort")), "ascending");
  assert.match(await attribute(page.locator("button[data-sort='library']"), "aria-label"), /sort descending/);
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
  assert.equal(await numericHeader.evaluate((button) => button.closest("th")!.getAttribute("aria-sort")), "descending");
  await page.keyboard.press("Space");
  assert.equal(await numericHeader.evaluate((button) => button.closest("th")!.getAttribute("aria-sort")), "ascending");
  assert.equal(await page.locator("#library-table th[aria-sort='ascending']").count(), 1);
  assert.equal(await page.locator("#library-table th[aria-sort='none']").count(), 5);
  await page.locator("#breakdown-group").selectOption("service");
  assert.equal(await page.locator("#breakdown-values tr").count(), Math.min(12, source.services.length));
  await page.locator("#breakdown-measure").selectOption("lines");
  await page.locator("#breakdown-group").selectOption("category");
  await page.locator("#library-search").fill("Azure.Identity");
  assert.ok(Number(await text(page.locator("#library-count"))) > 0);
  await page.getByRole("button", { name: "Azure.Identity", exact: true }).click();
  assert.equal(await text(page.locator("#detail-heading")), "Azure.Identity");
  assert.match(await text(page.locator("#detail-source")), /No file-level evidence/);
  await assertSourcePresentation(page);
  await page.locator("#library-search").fill("not-an-existing-library");
  assert.equal(await text(page.locator("#custom-percent")), "N/A");
  assert.match(await text(page.locator("#library-values")), /No libraries match/);
  await page.locator("#library-search").fill("");
  assert.equal(await text(page.locator("#library-count")), expectedCount);
  assert.equal(await page.locator("#snapshot-select option").count(), 1);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator("#library-search").fill("Azure.Identity");
  await page.getByRole("button", { name: "Azure.Identity", exact: true }).click();
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  assert.deepEqual(errors, Array<string>());
  assert.ok(network.every((url) => url.startsWith("file:")), "The offline dashboard requested an external asset.");
  await page.emulateMedia({ colorScheme: "dark" });
  assert.equal(await page.evaluate(() => matchMedia("(prefers-color-scheme: dark)").matches), true);

  server = createServer(async (request, response) => {
    assert.ok(request.url, "The browser test server received a request without a URL.");
    const name = new URL(request.url, "http://127.0.0.1").pathname.slice(1) || "index.html";
    if (!["index.html", "styles.css", "app.js", "snapshots.js"].includes(name)) {
      response.writeHead(404); response.end(); return;
    }
    response.setHeader("Content-Type", name.endsWith(".html") ? "text/html" : name.endsWith(".css") ? "text/css" : "application/javascript");
    response.end(await readFile(new URL(`../dist/${name}`, import.meta.url)));
  });
  const activeServer = server;
  await new Promise<void>((resolve) => activeServer.listen(0, "127.0.0.1", resolve));
  const address = activeServer.address();
  assert.ok(address && typeof address !== "string");
  const localUrl = `http://127.0.0.1:${address.port}/`;
  const seedConfig = (seeds: Snapshot[], indexUrl = "") =>
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
  assert.equal(await text(naPage.locator("#library-values button").last()), "Azure.Empty");
  await naPage.locator("button[data-sort='customRatio']").click();
  assert.equal(await text(naPage.locator("#library-values button").last()), "Azure.Empty", "Descending sorting moved N/A ahead of measured ratios.");
  await naPage.locator("button[data-sort='service']").click();
  await naPage.locator("button[data-sort='service']").click();
  assert.deepEqual(await naPage.locator("#library-values button").allTextContents(), [...nullable.libraries]
    .sort((a, b) => b.service.localeCompare(a.service) || a.library.localeCompare(b.library)).map((library) => library.library));
  const first = browserSnapshot();
  const second = snapshot([
    library("Azure.Identity", 8, 37),
    library("Azure.ResourceManager.Sample", 12, 88, "beta", "management"),
  ], "2026-10-04T12:00:00Z", "2");
  const modified = snapshot(first.libraries, "2026-10-05T12:00:00Z", "3", true);
  const fixturePage = await browser.newPage();
  fixturePage.on("pageerror", (error) => errors.push(error.message));
  await fixturePage.route(`${localUrl}snapshots.js`, (route) => route.fulfill({
    status: 200, contentType: "application/javascript", body: seedConfig([first, second, modified]),
  }));
  await fixturePage.goto(localUrl);
  await fixturePage.waitForFunction(() => document.querySelectorAll("#history-values tr").length === 2);
  assert.match(await text(fixturePage.locator("#history-note")), /2 measured revisions.*Fixed cohort: 2/);
  await fixturePage.locator("#fixed-cohort").uncheck();
  assert.match(await text(fixturePage.locator("#history-values tr").last()), /\+0 \/ -1/);
  await fixturePage.locator("#history-scope").selectOption("Azure.Identity");
  assert.equal(await text(fixturePage.locator("#history-values tr").last().locator("td").nth(2)), "1");
  assert.equal(await fixturePage.locator("#snapshot-select option").count(), 3);
  assert.equal(await fixturePage.locator("#snapshot-select").inputValue(), modified.snapshotId);
  assert.equal(await fixturePage.locator("#history-values tr").count(), 2);
  assert.deepEqual(await fixturePage.locator("#history-values tr td:nth-child(2)").allTextContents(),
    [first, second].map((observation) => observation.repository.commit.slice(0, 8)));
  await assertSourcePresentation(fixturePage);
  for (const range of ["30", "365"]) {
    await fixturePage.locator("#history-range").selectOption(range);
    await fixturePage.locator("#fixed-cohort").check();
    assert.equal(await fixturePage.locator("#history-values tr").count(), 2);
    await fixturePage.locator("#fixed-cohort").uncheck();
    assert.equal(await fixturePage.locator("#history-values tr").count(), 2);
    await assertSourcePresentation(fixturePage);
  }
  await fixturePage.locator("#repository-select").selectOption("Azure/azure-sdk-for-java");
  assert.equal(await fixturePage.locator("#measurements").isVisible(), false);
  await fixturePage.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
  assert.equal(await fixturePage.locator("#snapshot-select option").count(), 3);
  assert.equal(await fixturePage.locator("#history-values tr").count(), 2);
  await assertSourcePresentation(fixturePage);
  assert.deepEqual(errors, Array<string>());
  const older = snapshot(first.libraries, "2026-05-01T12:00:00Z", "4");
  const monthDocument = (observations: Observation[]) => {
    const month = observations[0].collectedAt.slice(0, 7);
    const body = JSON.stringify({ schemaVersion: "1.0", month, observations: observations.map(compactObservation) });
    return { body, month, path: `history/${createHash("sha256").update(body).digest("hex")}/${month}.json` };
  };
  const october = monthDocument([first, second]);
  const may = monthDocument([older]);
  const modifiedMonth = monthDocument([first, second, modified]);
  const partial = snapshot(first.libraries, "2026-10-06T12:00:00Z", "5");
  const brokenBody = "{broken";
  const brokenMonth = {
    body: brokenBody, month: "2026-10",
    path: `history/${createHash("sha256").update(brokenBody).digest("hex")}/2026-10.json`,
  };
  for (const scenario of [
    { name: "uncommitted-latest", latest: modified, month: october, error: /Official observations require committed source/ },
    { name: "uncommitted-history", latest: second, month: modifiedMonth, error: /Official observations require committed source/ },
    { name: "broken-history", latest: partial, month: brokenMonth, error: /JSON|Unexpected/ },
    ...["0.0", "2.0", "3.0"].map((version) => ({
      name: `version-${version}`, latest: { ...second, schemaVersion: version }, month: october, error: /Invalid snapshot/,
    })),
    { name: "legacy-prototype", latest: legacySnapshot(), month: october, error: /Invalid snapshot/ },
    { name: "unsupported-repository", latest: { ...second, repository: { ...second.repository, name: "Azure/azure-sdk-for-java" } },
      month: october, error: /Invalid snapshot/ },
    { name: "unavailable-index", latest: second, month: october, status: 503, error: /HTTP 503/ },
  ]) {
    const fault = await browser.newPage();
    fault.on("pageerror", (error) => errors.push(error.message));
    const url = `https://metrics.invalid/${scenario.name}/index.json`;
    const configuredIndex = {
      schemaVersion: "1.0", latest: `snapshots/${scenario.latest.snapshotId}.json`,
      history: [{ month: scenario.month.month, path: scenario.month.path }],
    };
    await fault.route(`${localUrl}snapshots.js`, (route) => route.fulfill({
      status: 200, contentType: "application/javascript", body: seedConfig([first, second], url),
    }));
    await fault.route("https://metrics.invalid/**", (route) => {
      assert.equal(route.request().method(), "GET");
      assert.equal(route.request().headers().cookie, undefined);
      assert.equal(route.request().headers().authorization, undefined);
      const requested = route.request().url();
      return route.fulfill({
        status: requested === url && "status" in scenario ? scenario.status : 200, contentType: "application/json",
        headers: { "Access-Control-Allow-Origin": "*" },
        body: requested === url ? JSON.stringify(configuredIndex) :
          requested.endsWith(configuredIndex.latest) ? JSON.stringify(scenario.latest) : scenario.month.body,
      });
    });
    await fault.goto(localUrl);
    await fault.waitForFunction(() => document.querySelector<HTMLElement>("#status")!?.classList.contains("error") &&
      !document.querySelector<HTMLSelectElement>("#repository-select")!?.disabled);
    assert.equal(await fault.locator("#status").isVisible(), true);
    assert.match(await text(fault.locator("#status")), scenario.error);
    assert.match(await text(fault.locator("#status")), /Existing observations were retained/);
    assert.equal(await fault.locator("#snapshot-select option").count(), 2, `${scenario.name} changed existing observations.`);
    assert.equal(await fault.locator("#snapshot-select").inputValue(), second.snapshotId);
    assert.equal(await fault.locator("#history-values tr").count(), 2, `${scenario.name} became a false history point.`);
    assert.equal(await text(fault.locator("#custom-percent")), percentage(second.summary.customRatio));
    assert.equal(await fault.locator("#freshness").isVisible(), false, "An incomplete feed became active.");
    await assertSourcePresentation(fault);
    await fault.close();
  }
  const hostedIndex = { schemaVersion: "1.0", latest: `snapshots/${second.snapshotId}.json`,
    history: [may, october].map(({ month, path }) => ({ month, path })) };
  const publicIndex = "https://metrics.invalid/hosted/index.json";
  const hosted = await browser.newPage();
  await hosted.clock.setFixedTime(new Date("2026-10-07T12:00:00Z"));
  hosted.on("pageerror", (error) => errors.push(error.message));
  const hostedRequests: string[] = [];
  let failOlder = true;
  await hosted.route(`${localUrl}snapshots.js`, (route) => route.fulfill({
    status: 200, contentType: "application/javascript", body: seedConfig([older], publicIndex),
  }));
  await hosted.route("https://metrics.invalid/**", async (route) => {
    const url = route.request().url();
    hostedRequests.push(url);
    assert.equal(route.request().headers().cookie, undefined);
    assert.equal(route.request().headers().authorization, undefined);
    let body: string; let status = 200;
    if (url === publicIndex) body = JSON.stringify(hostedIndex);
    else if (url.endsWith(hostedIndex.latest)) body = JSON.stringify(second);
    else if (url.endsWith(october.path)) body = october.body;
    else if (url.endsWith(may.path) && !failOlder) body = may.body;
    else { status = 503; body = "Unavailable"; }
    await route.fulfill({ status, body, contentType: "application/json", headers: { "Access-Control-Allow-Origin": "*" } });
  });
  await hosted.goto(localUrl);
  await hosted.waitForFunction(() => document.querySelector<HTMLElement>("#status")!?.textContent?.startsWith("Published index:"));
  assert.equal(await text(hosted.locator("#library-count")), "2");
  await assertSourcePresentation(hosted);
  assert.equal(await hosted.locator("#snapshot-select option").count(), 2);
  assert.equal(await hosted.locator("#history-values tr").count(), 2);
  assert.equal(await hosted.locator("#history-range").inputValue(), "90");
  assert.equal(hostedRequests.length, 3, "Startup downloaded the full archive.");
  assert.match(await text(hosted.locator("#freshness")), /STALE/);
  assert.equal(await hosted.locator("#history-scope option[value='Azure.Provisioning.Sample']").count(), 1);
  await hosted.locator("#history-range").selectOption("365");
  await hosted.waitForFunction(() => document.querySelector<HTMLElement>("#status")!?.classList.contains("error"));
  assert.equal(await hosted.locator("#history-range").inputValue(), "90");
  assert.equal(await hosted.locator("#history-values tr").count(), 2);
  assert.equal(await text(hosted.locator("#library-count")), "2");
  await hosted.locator("#snapshot-select").selectOption(older.snapshotId);
  await hosted.waitForFunction((id) => document.querySelector<HTMLSelectElement>("#snapshot-select")!?.value === id &&
    !document.querySelector<HTMLSelectElement>("#snapshot-select")!?.disabled, second.snapshotId);
  assert.equal(await hosted.locator("#status").isVisible(), true);
  assert.match(await text(hosted.locator("#status")), /HTTP 503/);
  assert.equal(await hosted.locator("#history-range").inputValue(), "90");
  assert.equal(await hosted.locator("#history-values tr").count(), 2);
  assert.equal(await text(hosted.locator("#library-count")), "2", "Failed selection changed the overview.");
  failOlder = false;
  await hosted.locator("#history-range").selectOption("365");
  await hosted.waitForFunction(() => document.querySelectorAll("#history-values tr").length === 3);
  const beforeCached = hostedRequests.length;
  await hosted.locator("#history-range").selectOption("30");
  await hosted.waitForFunction(() => document.querySelector<HTMLElement>("#status")!?.textContent?.includes("30-day history loaded"));
  assert.equal(await hosted.locator("#history-values tr").count(), 2);
  assert.equal(hostedRequests.length, beforeCached, "Cached history was fetched again.");
  const beforeSelection = hostedRequests.length;
  await hosted.locator("#snapshot-select").selectOption(older.snapshotId);
  await hosted.waitForFunction(() => document.querySelector<HTMLElement>("#status")!?.textContent?.includes("selected observation's UTC date"));
  assert.equal(await hosted.locator("#history-values tr").count(), 1);
  assert.match(await text(hosted.locator("#history-values tr")), /2026-05-01/);
  assert.equal(await text(hosted.locator("#library-count")), "3");
  assert.equal(hostedRequests.length, beforeSelection, "Historical selection did not reuse its cached month.");
  assert.equal(await hosted.locator("#snapshot-select option").count(), 2);
  assert.equal(await hosted.locator("#snapshot-select").inputValue(), older.snapshotId);
  assert.equal(await text(hosted.locator("#library-count")), "3");
  assert.equal(await hosted.locator("#freshness").isVisible(), true);
  assert.equal(await hosted.locator("#history-range option[value='all']").evaluate((option) => {
    if (!(option instanceof HTMLOptionElement)) throw new Error("History range must be an option.");
    return option.disabled;
  }), true);
  const beforeRepositorySwitch = hostedRequests.length;
  await hosted.locator("#repository-select").selectOption("Azure/azure-sdk-for-go");
  assert.equal(await hosted.locator("#measurements").isVisible(), false);
  assert.equal(await hosted.locator("#freshness").isVisible(), false);
  await hosted.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
  assert.equal(await hosted.locator("#snapshot-select").inputValue(), older.snapshotId);
  assert.equal(await text(hosted.locator("#library-count")), "3");
  assert.equal(await hosted.locator("#history-range").inputValue(), "30");
  assert.equal(hostedRequests.length, beforeRepositorySwitch, "Repository selection fetched an unconfigured feed.");
  await assertSourcePresentation(hosted);
  assert.deepEqual(errors, Array<string>());
  const previewBuild = spawnSync(process.execPath, [
    "--experimental-strip-types", fileURLToPath(new URL("../build.ts", import.meta.url)), "--preview", "--snapshot", snapshotPath,
  ], { stdio: "inherit" });
  if (previewBuild.error) throw previewBuild.error;
  assert.equal(previewBuild.status, 0, "Static preview build failed.");
  const previewPage = await browser.newPage();
  const previewRequests: string[] = [];
  previewPage.on("pageerror", (error) => errors.push(error.message));
  previewPage.on("request", (request) => previewRequests.push(request.url()));
  const boundary = Date.parse(source.collectedAt) + 36 * 3600000;
  await previewPage.clock.setFixedTime(new Date(boundary));
  await previewPage.goto(pageUrl);
  await previewPage.waitForFunction((count) => document.querySelector<HTMLElement>("#library-count")!?.textContent === count, expectedCount);
  await assertSourcePresentation(previewPage);
  assert.equal(await text(previewPage.locator("#custom-percent")), percentage(source.summary.customRatio));
  assert.equal(await text(previewPage.locator("#service-count")), `${source.services.length} services`);
  assert.equal(await text(previewPage.locator("#generated-lines")), number.format(source.summary.generatedLines));
  assert.equal(await previewPage.locator("#preview-notice").count(), 0);
  const deploymentMetadata = await text(previewPage.locator("#deployment-info"));
  assert.match(deploymentMetadata, /Embedded baseline.*No automatic feed/);
  assert.ok((await text(previewPage.locator("#observation-info"))).includes(new Date(source.collectedAt).toISOString()));
  assert.equal(await previewPage.locator("#freshness").isVisible(), false);
  await previewPage.clock.setFixedTime(new Date(boundary + 1));
  await previewPage.reload();
  await previewPage.waitForFunction((count) => document.querySelector<HTMLElement>("#library-count")!?.textContent === count, expectedCount);
  assert.equal(await previewPage.locator("#freshness").isVisible(), false);
  assert.doesNotMatch(await text(previewPage.locator("body")),
    /Static preview - embedded baseline|Static preview baseline:|Static preview selected observation|STALE: embedded data/);
  assert.equal(await text(previewPage.locator("#status")), "");
  assert.match(await text(previewPage.locator("#history-note")), /Baseline only.*No code-change trend yet/);
  assert.equal(await previewPage.locator("#history-values tr").count(), 1);
  await previewPage.locator("button[data-sort='totalLines']").click();
  await previewPage.locator("#category-filter").selectOption("management");
  await previewPage.locator("#history-range").selectOption("365");
  const selectedLibrary = source.libraries.find((library) => library.category === "management")!.library;
  await previewPage.getByRole("button", { name: selectedLibrary, exact: true }).click();
  const beforeRepositoryRequests = previewRequests.length;
  for (const repository of REPOSITORIES.filter((repository) => !repository.implemented)) {
    await previewPage.locator("#repository-select").selectOption(repository.name);
    assert.equal(await previewPage.getByRole("combobox", { name: "Repository", exact: true }).inputValue(), repository.name);
    assert.equal(await previewPage.locator("#repository-state").isVisible(), true);
    const state = await text(previewPage.locator("#repository-state"));
    assert.ok(state.includes(repository.name));
    assert.match(state, /No observations collected.*collector is not implemented/);
    await assertSourcePresentation(previewPage);
    assert.equal(await previewPage.locator("#repository-branding").count(), 0);
    assert.equal(await previewPage.locator("#measurements").isVisible(), false);
    for (const id of ["custom-percent", "library-count", "history-chart", "library-values", "library-detail",
      "observation-info", "deployment-info", "freshness"]) {
      assert.equal(await previewPage.locator(`#${id}`).isVisible(), false, `${repository.name} exposed .NET ${id}.`);
    }
    assert.equal(previewRequests.length, beforeRepositoryRequests, `${repository.name} requested a nonexistent feed.`);
  }
  await previewPage.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
  assert.equal(await previewPage.locator("#category-filter").inputValue(), "management");
  assert.equal(await previewPage.locator("#history-range").inputValue(), "365");
  assert.equal(await previewPage.locator("#history-scope").inputValue(), selectedLibrary);
  assert.equal(await text(previewPage.locator("#detail-heading")), selectedLibrary);
  assert.equal(await previewPage.locator("#library-detail").isVisible(), true);
  await assertSourcePresentation(previewPage);
  assert.equal(await previewPage.locator("#measurements").isVisible(), true);
  assert.equal(await previewPage.locator("#repository-state").isVisible(), false);
  assert.equal(await text(previewPage.locator("#library-count")), String(management.libraryCount));
  assert.equal(await previewPage.locator("#snapshot-select").inputValue(), source.snapshotId);
  assert.equal(await text(previewPage.locator("#deployment-info")), deploymentMetadata);
  assert.equal(await previewPage.locator("#freshness").isVisible(), false);
  assert.equal(await previewPage.locator("button[data-sort='totalLines']").evaluate((button) => button.closest("th")!.getAttribute("aria-sort")), "ascending");
  assert.deepEqual(await previewPage.locator("#library-values button").allTextContents(), source.libraries
    .filter((library) => library.category === "management")
    .sort((a, b) => a.metrics.totalLines - b.metrics.totalLines || a.library.localeCompare(b.library)).map((library) => library.library));
  assert.equal(previewRequests.length, beforeRepositoryRequests);
  await previewPage.locator("#category-filter").selectOption("");
  await previewPage.locator("#history-scope").selectOption("");
  await previewPage.locator("#close-detail").click();
  assert.equal(await text(previewPage.locator("#library-count")), expectedCount);
  const mobilePreview = await browser.newPage({ viewport: { width: 390, height: 844 } });
  mobilePreview.on("pageerror", (error) => errors.push(error.message));
  await mobilePreview.goto(pageUrl);
  await mobilePreview.waitForFunction((count) => document.querySelector<HTMLElement>("#library-count")!?.textContent === count, expectedCount);
  assert.equal(await mobilePreview.locator("#library-values tr").count(), source.libraries.length);
  assert.equal(await mobilePreview.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true,
    "The full portfolio overflowed an initially mobile viewport.");
  await previewPage.locator("#history-range").selectOption("365");
  await previewPage.locator("#library-search").fill("Azure.Identity");
  await previewPage.getByRole("button", { name: "Azure.Identity", exact: true }).click();
  assert.match(await text(previewPage.locator("#detail-source")), /No file-level evidence/);
  await previewPage.locator("#library-search").fill("");
  assert.equal(await previewPage.locator("#library-values tr").count(), source.libraries.length);
  assert.equal(await previewPage.locator("#library-detail").isVisible(), true);
  assert.equal(await previewPage.locator("button[data-sort='totalLines']").evaluate((button) => button.closest("th")!.getAttribute("aria-sort")), "ascending");
  for (let retry = 0; retry < 3; retry++) {
    await previewPage.setViewportSize({ width: 390, height: 844 });
    assert.equal(await previewPage.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true,
      "The full portfolio with open library detail overflowed immediately after resizing.");
    assert.equal(await previewPage.evaluate(() => [...document.querySelectorAll<HTMLCanvasElement>(".chart-container canvas")].every((element) =>
      element.getBoundingClientRect().width <= element.parentElement!.getBoundingClientRect().width)), true,
    "A chart retained its desktop width outside the mobile container.");
    await previewPage.setViewportSize({ width: 1280, height: 1000 });
    await previewPage.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
  }
  assert.equal(await text(previewPage.locator("#deployment-info")), deploymentMetadata);
  assert.equal(await previewPage.locator("#freshness").isVisible(), false);
  assert.equal(await previewPage.locator("#snapshot-files, #clear-data, input[type=file]").count(), 0);
  assert.equal(await previewPage.getByText("Load JSON snapshots", { exact: true }).count(), 0);
  assert.equal(await previewPage.getByRole("button", { name: "Clear data", exact: true }).count(), 0);
  assert.equal(await previewPage.locator("#snapshot-select option").count(), 1);
  assert.ok(previewRequests.every((url) => url.startsWith("file:")), "Static preview requested a feed or external asset.");
  const visual = await browser.newPage({ viewport: { width: 1440, height: 1100 }, colorScheme: "light" });
  const visualRequests: string[] = [];
  visual.on("pageerror", (error) => errors.push(error.message));
  visual.on("request", (request) => visualRequests.push(request.url()));
  await visual.goto(pageUrl);
  await visual.waitForFunction((count) => document.querySelector<HTMLElement>("#library-count")!?.textContent === count, expectedCount);
  await visual.keyboard.press("Tab");
  assert.equal(await visual.evaluate(() => document.activeElement?.id), "repository-select");
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
  for (const theme of ["light", "dark"] as const) {
    await visual.emulateMedia({ colorScheme: theme });
    for (const width of [1440, 1280, 390, 320]) {
      await visual.setViewportSize({ width, height: 1000 });
      const layout = await visual.evaluate(() => {
        const cards = [...document.querySelectorAll(".kpi")].map((card) => card.getBoundingClientRect());
        const scroller = document.querySelector<HTMLElement>(".library-scroll")!;
        return {
          bodyFont: parseFloat(getComputedStyle(document.body).fontSize),
          pageFits: document.documentElement.scrollWidth <= innerWidth,
          widths: cards.map((card) => card.width),
          heights: cards.map((card) => card.height),
          metricTop: document.querySelector<HTMLElement>("#custom-percent")!.getBoundingClientRect().top,
          tableFont: parseFloat(getComputedStyle(document.querySelector<HTMLElement>("#library-values td")!).fontSize),
          tabularNumbers: getComputedStyle(document.querySelector<HTMLElement>("#custom-percent")!).fontVariantNumeric,
          packageWrap: getComputedStyle(document.querySelector<HTMLElement>(".library-link")!).whiteSpace,
          firstColumn: document.querySelector<HTMLElement>("#library-values td")!.getBoundingClientRect().width,
          scrollsInternally: scroller.scrollWidth > scroller.clientWidth && getComputedStyle(scroller).overflowX === "auto",
          statusHidden: document.querySelector<HTMLElement>("#status")!.hidden,
          foreground: getComputedStyle(document.body).color,
          background: getComputedStyle(document.body).backgroundColor,
          secondary: getComputedStyle(document.querySelector<HTMLElement>("#observation-info")!).color,
          surface: getComputedStyle(document.querySelector<HTMLElement>(".kpi")!).backgroundColor,
          custom: getComputedStyle(document.querySelector<HTMLElement>("#custom-percent")!).color,
          generated: getComputedStyle(document.querySelector<HTMLElement>("#generated-lines")!).color,
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
      assert.equal(await text(visual.locator("#custom-percent")), percentage(source.summary.customRatio));
      await assertSourcePresentation(visual);
      assert.equal(await visual.locator("#snapshot-files, #clear-data, input[type=file]").count(), 0);
      await visual.waitForFunction(() => [...document.querySelectorAll<HTMLCanvasElement>("canvas")].every((canvas) =>
        canvas.getContext("2d")!.getImageData(0, 0, canvas.width, canvas.height).data.some((value, index) => index % 4 === 3 && value > 0)));
      if (screenshots) await visual.screenshot({ path: join(screenshots, `visual-${theme}-${width}.png`), fullPage: true });
    }
    await visual.locator("#repository-select").selectOption("Azure/azure-sdk-for-cpp");
    assert.equal(await visual.locator("#repository-empty").isVisible(), true);
    assert.equal(await visual.locator("#measurements").isVisible(), false);
    assert.equal(await visual.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    if (screenshots) await visual.screenshot({ path: join(screenshots, `visual-empty-${theme}.png`) });
    await visual.locator("#repository-select").selectOption("Azure/azure-sdk-for-net");
    assert.equal(await text(visual.locator("#library-count")), expectedCount);
  }
  for (const name of ["Scrollable library measurements", "Scrollable provenance breakdown", "Scrollable historical observations"]) {
    await assertKeyboardScroller(visual, visual.getByRole("region", { name }));
  }
  assert.ok(visualRequests.every((url) => url.startsWith("file:")), "The visual refresh introduced external assets or telemetry.");
  assert.deepEqual(errors, Array<string>());
  console.log("Browser checks passed: baseline, category/service filters, sorting, charts, library details, seeded cohorts, atomic configured-feed error retention, committed-source-only history, responsive and dark layouts, no file-import/clear/manual-index controls or unsolicited external requests.");
  console.log("Source-label checks passed: Custom source cards/legend/disclosure, no source-state badge/override/suffix/notes, modified-source history exclusion across ranges/cohorts, plain official-source rejection with retained observations.");
  console.log("Hosted checks passed: automatic latest, anonymous bounded month loading, historical membership, stale warning, failed range/selection rollback and cache reuse without any manual loader.");
  console.log("Preview checks passed: actual seed counts/ratio/date, quiet baseline metadata, all three banners removed, no feed requests/fake trend, full-portfolio initial mobile and immediate open-detail resize.");
  console.log("Repository checks passed: seven choices, four unsupported-language states without measurements/fetches, .NET filter/history/detail restoration and configured unsupported-source rejection.");
  console.log("Visual checks passed: aligned desktop cards, compact context, 15px body/14px tables, light/dark 1440/1280/390/320px, painted charts, keyboard focus/scrolling, unfragmented package names and bounded page widths.");
  console.log("Sort-header checks passed: six native column controls, default custom-lines descending, lexical/numeric sorting, stable ties, Enter/Space, aria-sort/direction, N/A last both ways and filter/repository/detail state preservation.");
} finally {
  await browser?.close();
  const activeServer = server;
  if (activeServer) await new Promise<void>((resolve, reject) => activeServer.close((error) => error ? reject(error) : resolve()));
  await rm(snapshotPath, { force: true });
  await rm(new URL("../generated/browser-desktop.png", import.meta.url), { force: true });
}
