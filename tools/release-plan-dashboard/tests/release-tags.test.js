import { describe, expect, test } from "vitest";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const appJs = readFileSync(path.join(__dirname, "../public/app.js"), "utf8");
const indexHtml = readFileSync(
  path.join(__dirname, "../public/index.html"),
  "utf8",
);
const styleCss = readFileSync(
  path.join(__dirname, "../public/style.css"),
  "utf8",
);

describe("SDK ready-to-release tag and status", () => {
  test("uses renamed SDK status text", () => {
    expect(appJs).toContain("SDK Ready To Release");
    expect(appJs).not.toContain("SDK Ready To Be Released");
  });

  test("includes SDK ready-to-release tag in global tag filter", () => {
    expect(indexHtml).toContain('value="sdk-ready-to-release"');
    expect(indexHtml).toContain("SDK Ready To Release");
    expect(appJs).toContain('tagFilter === "sdk-ready-to-release"');
  });

  test("shows SDK Ready To Release badge in card title using isSdkReadyToReleasePlan", () => {
    // The badge is rendered in the card title (releaseTagBadge) when isSdkReadyToReleasePlan
    // returns true AND the step status is not already "SDK Ready To Release" (avoiding duplicate).
    expect(appJs).toContain("badge-sdk-ready-to-release");
    expect(appJs).toContain("isSdkReadyToReleasePlan(p)");
    // The releaseTagBadge block should include the sdk-ready-to-release badge
    const releaseTagBadgeBlock = appJs.slice(
      appJs.indexOf("releaseTagBadge"),
      appJs.indexOf("missingProductBadge"),
    );
    expect(releaseTagBadgeBlock).toContain("badge-sdk-ready-to-release");
    expect(releaseTagBadgeBlock).toContain("isSdkReadyToReleasePlan(p)");
    // Guard against double-display: the badge must not appear when step already shows it
    expect(releaseTagBadgeBlock).toContain(
      'step.status !== "SDK Ready To Release"',
    );
  });

  test("badge-sdk-ready-to-release CSS class is defined in style.css", () => {
    expect(styleCss).toContain(".badge-sdk-ready-to-release");
  });
});

describe("Auto-release label (lazy, from PR details)", () => {
  test("does not offer an auto-release option in the tag filter", () => {
    expect(indexHtml).not.toContain('value="auto-release"');
    expect(appJs).not.toContain('tagFilter === "auto-release"');
    expect(appJs).not.toContain("isAutoReleasePlan");
  });

  test("renders auto-release label pills from lazily-loaded PR details", () => {
    expect(appJs).toContain("function autoReleaseLabelPills(");
    expect(appJs).toContain("autoReleaseLabels");
    expect(appJs).toContain("pr-label-auto-release");
  });

  test("hides the auto-release pill once the language is released", () => {
    const fn = appJs.slice(
      appJs.indexOf("function autoReleaseLabelPills("),
      appJs.indexOf("function prDetailLabels("),
    );
    expect(fn).toContain('rel === "released"');
    expect(fn).toContain('rel === "completed"');
  });

  test("auto-release pill CSS class is defined but the badge class is not", () => {
    expect(styleCss).toContain(".pr-label-auto-release");
    expect(styleCss).not.toContain(".badge-auto-release");
  });
});
