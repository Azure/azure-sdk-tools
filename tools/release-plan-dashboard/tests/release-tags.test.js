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

describe("Auto-release tag and label", () => {
  test("includes auto-release tag in global tag filter", () => {
    expect(indexHtml).toContain('value="auto-release"');
    expect(indexHtml).toContain("Auto Release");
    expect(appJs).toContain('tagFilter === "auto-release"');
  });

  test("defines isAutoReleasePlan helper based on langData.autoRelease", () => {
    expect(appJs).toContain("function isAutoReleasePlan(p)");
    expect(appJs).toContain("l.autoRelease");
  });

  test("shows Auto Release badge in card title using isAutoReleasePlan", () => {
    const releaseTagBadgeBlock = appJs.slice(
      appJs.indexOf("releaseTagBadge"),
      appJs.indexOf("missingProductBadge"),
    );
    expect(releaseTagBadgeBlock).toContain("badge-auto-release");
    expect(releaseTagBadgeBlock).toContain("isAutoReleasePlan(p)");
  });

  test("renders auto-release PR label pills from sdkPrLabels", () => {
    expect(appJs).toContain("l.sdkPrLabels");
    expect(appJs).toContain("pr-label-auto-release");
  });

  test("auto-release CSS classes are defined in style.css", () => {
    expect(styleCss).toContain(".badge-auto-release");
    expect(styleCss).toContain(".pr-label-auto-release");
  });
});
