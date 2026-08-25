import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  extractTypeSpecSymbols,
  parseDocumentationCatalog,
  prepareComplianceEvidence,
  routeDocumentation,
} from "./prepare-compliance-evidence.mjs";

const catalog = `# Reference Document Links

## Long-Running Operations (LRO)

- [ARM long-running operations](https://example.test/lro): Define ARM LROs and final-state behavior.

## Paging

- [TypeSpec pagination](https://example.test/paging): Model paging with TypeSpec decorators.
`;

const armCatalog = `# Reference Document Links

## Add ARM Resource Type

- [ARM resource types and modeling](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/): Define child resources with standard ARM resource templates.

## Add ARM Resource Operation

- [ARM resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): Use standard ARM templates for CRUDL and custom actions.

## Decorators

- [Azure.ResourceManager decorators](https://azure.github.io/typespec-azure/docs/libraries/azure-resource-manager/reference/decorators/): Look up every ARM decorator.
`;

const evidence = {
  typeSpecDiffs: [
    {
      lines: [
        "+@Azure.ResourceManager.armResourceOperations",
        "+@pollingOperation",
        "+op beginUpdate(): Widget;",
      ],
    },
  ],
};

test("TypeSpec symbols deterministically route official documentation", () => {
  const symbols = extractTypeSpecSymbols(evidence.typeSpecDiffs);
  assert.ok(symbols.includes("@pollingOperation"));
  assert.ok(symbols.includes("Azure.ResourceManager"));
  const routed = routeDocumentation(
    symbols,
    parseDocumentationCatalog(catalog),
  );
  assert.equal(routed[0].url, "https://example.test/lro");
});

test("compliance document fetches are cached by canonical URL", async () => {
  const cacheRoot = mkdtempSync(join(tmpdir(), "assessment-doc-cache-"));
  let fetchCount = 0;
  const fetchImpl = async () => {
    fetchCount += 1;
    return {
      ok: true,
      status: 200,
      text: async () =>
        '<main>Use pollingOperation for documented long-running operations.<pre><code class="language-typespec">@pollingOperation\\nop beginUpdate(): Widget;</code></pre></main>',
    };
  };
  try {
    const first = await prepareComplianceEvidence({
      evidence,
      catalogText: catalog,
      cacheRoot,
      fetchImpl,
      now: Date.parse("2026-08-24T00:00:00Z"),
    });

    test("legacy ARM resources route resource type and operation guidance", () => {
      const symbols = [
        "@Azure.ResourceManager.Legacy.customAzureResource",
        "ConnectionAnalyzerOps",
        "Azure.ResourceManager.Legacy.RoutedOperations",
      ];
      const routed = routeDocumentation(
        symbols,
        parseDocumentationCatalog(armCatalog),
      );
      assert.deepEqual(
        new Set(routed.slice(0, 2).map((document) => document.title)),
        new Set(["ARM resource operations", "ARM resource types and modeling"]),
      );
    });
    const second = await prepareComplianceEvidence({
      evidence,
      catalogText: catalog,
      cacheRoot,
      fetchImpl,
      now: Date.parse("2026-08-24T00:05:00Z"),
    });
    assert.equal(fetchCount, first.documents.length);
    assert.equal(second.documents[0].cache, "hit");
    assert.match(second.documents[0].matchingExcerpt, /pollingOperation/);
    assert.deepEqual(second.documents[0].candidateCodeBlocks, [
      "@pollingOperation\\nop beginUpdate(): Widget;",
    ]);
  } finally {
    rmSync(cacheRoot, { recursive: true, force: true });
  }
});
