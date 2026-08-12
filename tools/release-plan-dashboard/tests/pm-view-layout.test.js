import { describe, expect, test } from "vitest";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const indexHtml = readFileSync(
  path.join(__dirname, "../public/index.html"),
  "utf8",
);
const appJs = readFileSync(path.join(__dirname, "../public/app.js"), "utf8");

function expectOrdered(block, labels) {
  let lastIndex = -1;
  for (const label of labels) {
    const index = block.indexOf(label);
    expect(index, `Expected to find "${label}"`).toBeGreaterThanOrEqual(0);
    expect(index, `Expected "${label}" after prior sections`).toBeGreaterThan(
      lastIndex,
    );
    lastIndex = index;
  }
}

describe("PM View Attention Required layout", () => {
  test("applies the search filter to Attention Needed plans", () => {
    expect(appJs).toMatch(
      /function renderPMView\(plans\)[\s\S]*?const filter = store\(\)\.filters\.search\.toLowerCase\(\);[\s\S]*?plans\.filter\(\(p\) => matchesFilter\(p, filter\)\)/,
    );
  });

  test("links release plan IDs to the dashboard and ADO work item", () => {
    expect(appJs).toContain(
      "const dashboardUrl = `/?releasePlan=${encodeURIComponent(planId)}`;",
    );
    expect(appJs).toContain(
      '<a href="${esc(dashboardUrl)}" target="_blank" rel="noopener">${label}</a>',
    );
    expect(appJs).toContain(
      "const wiUrl = `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${p.id}`;",
    );
    expect(appJs).toContain(
      '<a href="${esc(wiUrl)}" target="_blank" rel="noopener">ADO work item ${esc(String(p.id))}</a>',
    );
  });

  test("groups informational and action sections in the expected order", () => {
    const informationalIndex = indexHtml.indexOf('id="pm-informational"');
    const needsAttentionIndex = indexHtml.indexOf('id="pm-needs-attention"');

    expect(needsAttentionIndex).toBeGreaterThanOrEqual(0);
    expect(indexHtml).toMatch(
      /aria-labelledby="pm-needs-attention"[\s\S]*class="pm-section-group-header section-header"[\s\S]*data-target="list-pm-needs-attention"[\s\S]*id="pm-needs-attention"[\s\S]*>\s*Needs Attention\s*</,
    );
    expect(informationalIndex).toBeGreaterThanOrEqual(0);
    expect(indexHtml).toMatch(
      /aria-labelledby="pm-informational"[\s\S]*class="pm-section-group-header section-header"[\s\S]*data-target="list-pm-informational"[\s\S]*id="pm-informational"[\s\S]*>\s*Informational\s*</,
    );
    expect(informationalIndex).toBeGreaterThan(needsAttentionIndex);

    expectOrdered(indexHtml.slice(needsAttentionIndex, informationalIndex), [
      "Ready to Complete Private",
      "Past Due Release Plans",
      "Stale Release Plans",
      "Release Plans Without SDK",
      "Partially Released",
      "Missing Namespace Approval",
      "Missing Product Details",
    ]);

    expectOrdered(indexHtml.slice(informationalIndex), [
      "Recently Finished (Last 2",
      "Approaching SDK Release",
    ]);
  });

  test("keeps each PM section present exactly once", () => {
    const sectionIds = [
      "section-pm-finished",
      "section-pm-approaching",
      "section-pm-pp-ready",
      "section-pm-pastdue",
      "section-pm-inactive",
      "section-pm-tier1",
      "section-pm-partial",
      "section-pm-ns-missing",
      "section-pm-product-missing",
    ];

    for (const sectionId of sectionIds) {
      const matches =
        indexHtml.match(new RegExp(`id="${sectionId}"`, "g")) || [];
      expect(matches).toHaveLength(1);
    }
  });
});
