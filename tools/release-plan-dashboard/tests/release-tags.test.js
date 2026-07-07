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
});
