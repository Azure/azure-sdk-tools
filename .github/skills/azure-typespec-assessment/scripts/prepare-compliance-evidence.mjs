#!/usr/bin/env node

import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { extractTypeSpecSymbols } from "./source-index.mjs";

export { extractTypeSpecSymbols } from "./source-index.mjs";

function unique(values) {
  return [...new Set(values)];
}

function hash(value) {
  return createHash("sha256").update(value).digest("hex");
}

export function parseDocumentationCatalog(markdown) {
  let category = "";
  const documents = [];
  for (const line of markdown.split(/\r?\n/)) {
    const heading = line.match(/^##\s+(.+)/);
    if (heading) {
      category = heading[1].trim();
      continue;
    }
    const link = line.match(/^-\s+\[([^\]]+)\]\((https:\/\/[^)]+)\):\s*(.+)$/);
    if (link) {
      documents.push({
        category,
        title: link[1],
        url: link[2],
        description: link[3],
      });
    }
  }
  return documents;
}

function searchTerms(symbols) {
  const terms = unique(
    symbols.flatMap((symbol) =>
      symbol
        .replace(/^@@?/, "")
        .replace(/([a-z])([A-Z])/g, "$1 $2")
        .split(/[^A-Za-z0-9]+/)
        .map((term) => term.toLowerCase())
        .filter((term) => term.length >= 3),
    ),
  );
  if (terms.some((term) => /^(?:lro|poll|polling|async)/.test(term))) {
    terms.push("lro", "long", "running");
  }
  if (terms.some((term) => /^(?:page|paging|nextlink|list)/.test(term))) {
    terms.push("paging", "pagination");
  }
  if (terms.some((term) => /^(?:version|added|removed|renamed)/.test(term))) {
    terms.push("versioning");
  }
  return unique(terms);
}

export function routeDocumentation(symbols, catalog, limit = 4) {
  const terms = searchTerms(symbols);
  const normalizedSymbols = symbols.join(" ").toLowerCase();
  return catalog
    .map((document) => {
      const title = `${document.category} ${document.title}`.toLowerCase();
      const text = `${title} ${document.description}`.toLowerCase();
      let score = terms.reduce(
        (total, term) =>
          total +
          (title.includes(term) ? 4 : 0) +
          (text.includes(term) ? 1 : 0),
        0,
      );
      if (
        document.url.includes("/howtos/arm/resource-type/") &&
        /customazureresource|parentresource|proxyresource|trackedresource/.test(
          normalizedSymbols,
        )
      ) {
        score += 100;
      }
      if (
        document.url.includes("/howtos/arm/resource-operations/") &&
        /routedoperations|armresourceoperations|actionasync|actionsync/.test(
          normalizedSymbols,
        )
      ) {
        score += 100;
      }
      return { ...document, score };
    })
    .filter((document) => document.score > 0)
    .sort(
      (left, right) =>
        right.score - left.score || left.url.localeCompare(right.url),
    )
    .slice(0, limit);
}

function documentSearchSymbols(document, symbols) {
  if (document.url.includes("/howtos/arm/resource-type/")) {
    return [...symbols, "ProxyResource", "parentResource"];
  }
  if (document.url.includes("/howtos/arm/resource-operations/")) {
    return [
      ...symbols,
      "ArmResourceRead",
      "ArmResourceCreateOrReplace",
      "custom actions",
    ];
  }
  return symbols;
}

function plainText(content) {
  return content
    .replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, " ")
    .replace(/<style\b[^>]*>[\s\S]*?<\/style>/gi, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/\s+/g, " ")
    .trim();
}

function decodeHtml(value) {
  return value
    .replace(/<[^>]+>/g, "")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .trim();
}

export function matchingCodeBlocks(content, symbols, limit = 4) {
  const terms = symbols
    .map((symbol) => symbol.replace(/^@@?/, "").toLowerCase())
    .filter((symbol) => symbol.length >= 3);
  return [
    ...content.matchAll(
      /<pre\b[^>]*>[\s\S]*?<code\b[^>]*>([\s\S]*?)<\/code>[\s\S]*?<\/pre>/gi,
    ),
  ]
    .map((match) => decodeHtml(match[1]))
    .filter(
      (block) =>
        block.length > 0 &&
        block.length <= 2000 &&
        terms.some((term) => block.toLowerCase().includes(term)),
    )
    .slice(0, limit);
}

function matchingExcerpt(content, symbols) {
  const text = plainText(content);
  const normalized = text.toLowerCase();
  const matches = symbols
    .map((symbol) => symbol.replace(/^@@?/, "").toLowerCase())
    .filter((symbol) => symbol.length >= 3)
    .map((symbol) => ({ symbol, index: normalized.indexOf(symbol) }))
    .filter((match) => match.index >= 0)
    .sort((left, right) => left.index - right.index);
  if (matches.length === 0) return "";
  const start = Math.max(0, matches[0].index - 180);
  const end = Math.min(text.length, matches[0].index + 420);
  return text.slice(start, end).trim();
}

async function fetchDocument(
  document,
  { cacheRoot, cacheTtlMs, fetchImpl, now },
) {
  const cachePath = join(cacheRoot, `${hash(document.url)}.json`);
  if (existsSync(cachePath)) {
    const cached = JSON.parse(readFileSync(cachePath, "utf8"));
    if (
      cached.url === document.url &&
      now - Date.parse(cached.fetchedAt) <= cacheTtlMs
    ) {
      return { ...cached, cache: "hit" };
    }
  }

  const response = await fetchImpl(document.url);
  if (!response.ok) {
    throw new Error(`Failed to fetch ${document.url}: HTTP ${response.status}`);
  }
  const content = await response.text();
  const fetched = {
    url: document.url,
    fetchedAt: new Date(now).toISOString(),
    contentHash: hash(content),
    content,
  };
  mkdirSync(cacheRoot, { recursive: true });
  writeFileSync(cachePath, `${JSON.stringify(fetched, null, 2)}\n`);
  return { ...fetched, cache: "miss" };
}

export async function prepareComplianceEvidence({
  evidence,
  catalogText,
  cacheRoot,
  fetchImpl = fetch,
  cacheTtlMs = 24 * 60 * 60 * 1000,
  now = Date.now(),
  maxDocuments = 4,
}) {
  const startedAt = process.hrtime.bigint();
  const symbols = extractTypeSpecSymbols(evidence.typeSpecDiffs ?? []);
  const routed = routeDocumentation(
    symbols,
    parseDocumentationCatalog(catalogText),
    maxDocuments,
  );
  const fetched = await Promise.all(
    routed.map(async (document) => {
      const result = await fetchDocument(document, {
        cacheRoot,
        cacheTtlMs,
        fetchImpl,
        now,
      });
      const matchingSymbols = documentSearchSymbols(document, symbols);
      return {
        category: document.category,
        title: document.title,
        url: document.url,
        routingScore: document.score,
        fetchedAt: result.fetchedAt,
        contentHash: result.contentHash,
        cache: result.cache,
        matchingExcerpt: matchingExcerpt(result.content, matchingSymbols),
        candidateCodeBlocks: matchingCodeBlocks(
          result.content,
          matchingSymbols,
        ),
      };
    }),
  );
  return {
    schemaVersion: 1,
    durationMs: Math.round(
      Number(process.hrtime.bigint() - startedAt) / 1_000_000,
    ),
    symbols,
    documents: fetched,
    reviewRequired: true,
    instructions:
      "Compare each fetched excerpt with the exact changed declaration before assigning compliance status.",
  };
}

async function main() {
  const [evidencePathValue, catalogPathValue, outputPathValue, cacheRootValue] =
    process.argv.slice(2);
  if (
    !evidencePathValue ||
    !catalogPathValue ||
    !outputPathValue ||
    !cacheRootValue
  ) {
    throw new Error(
      "Usage: prepare-compliance-evidence.mjs <evidence.json> <catalog.md> <output.json> <cache-dir>",
    );
  }
  const evidence = JSON.parse(readFileSync(resolve(evidencePathValue), "utf8"));
  const catalogText = readFileSync(resolve(catalogPathValue), "utf8");
  const result = await prepareComplianceEvidence({
    evidence,
    catalogText,
    cacheRoot: resolve(cacheRootValue),
  });
  mkdirSync(dirname(resolve(outputPathValue)), { recursive: true });
  writeFileSync(
    resolve(outputPathValue),
    `${JSON.stringify(result, null, 2)}\n`,
  );
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  main().catch((error) => {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  });
}
