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
    // The badge should be rendered in the card title (releaseTagBadge) whenever
    // isSdkReadyToReleasePlan returns true, not only when all languages are merged.
    expect(appJs).toContain("badge-sdk-ready-to-release");
    expect(appJs).toContain("isSdkReadyToReleasePlan(p)");
    // The releaseTagBadge block should include the sdk-ready-to-release badge
    const releaseTagBadgeBlock = appJs.slice(
      appJs.indexOf("releaseTagBadge"),
      appJs.indexOf("missingProductBadge"),
    );
    expect(releaseTagBadgeBlock).toContain("badge-sdk-ready-to-release");
    expect(releaseTagBadgeBlock).toContain("isSdkReadyToReleasePlan(p)");
  });

  test("badge-sdk-ready-to-release CSS class is defined in style.css", () => {
    expect(styleCss).toContain(".badge-sdk-ready-to-release");
  });
});
