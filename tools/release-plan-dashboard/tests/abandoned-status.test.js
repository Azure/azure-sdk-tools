import { describe, expect, test } from "vitest";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const appJs = readFileSync(path.join(__dirname, "../public/app.js"), "utf8");
const styleCss = readFileSync(
  path.join(__dirname, "../public/style.css"),
  "utf8",
);

describe("Abandoned release plan status", () => {
  test("detects abandoned state from the work item System.State", () => {
    expect(appJs).toContain('const isAbandoned = p.state === "Abandoned";');
  });

  test("renders an Abandoned badge on the card when abandoned", () => {
    expect(appJs).toContain("const abandonedBadge = isAbandoned");
    expect(appJs).toContain('badge badge-abandoned">Abandoned');
    // Badge is placed in the card meta row.
    expect(appJs).toContain(
      "${abandonedBadge}${stepHTML}${actionHTML}${finishedBadge}${dupHTML}",
    );
  });

  test("adds the abandoned modifier class to the card", () => {
    expect(appJs).toContain('(isAbandoned ? " abandoned" : "")');
  });

  test("suppresses step and action badges for abandoned plans", () => {
    expect(appJs).toContain("step.status && !isTerminal && !isAbandoned");
    expect(appJs).toContain("!isTerminal &&\n      !isAbandoned &&");
  });

  test("defines abandoned badge and card CSS", () => {
    expect(styleCss).toContain(".badge-abandoned");
    expect(styleCss).toContain(".plan-card.abandoned");
  });
});
