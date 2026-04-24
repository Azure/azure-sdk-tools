// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { readFile } from "node:fs/promises";
import path from "node:path";
import { CodeFile, ReviewLine, TokenKind } from "../models.js";
import { buildToken } from "../jstokens.js";
import { parseDtsFile, ParsedModule } from "./parser.js";

interface DtsMetadata {
  Name: string;
  PackageName: string;
  PackageVersion: string;
  ParserVersion: string;
  Language: "JavaScript";
}

interface PackageJson {
  name?: string;
  version?: string;
  dependencies?: Record<string, string>;
}

/**
 * Attempts to read a package.json adjacent to the given .d.ts file.
 * Returns an empty object if not found or not parseable.
 */
async function tryReadPackageJson(dtsFilePath: string): Promise<PackageJson> {
  try {
    const dir = path.dirname(dtsFilePath);
    const pkgPath = path.join(dir, "package.json");
    const content = await readFile(pkgPath, { encoding: "utf-8" });
    return JSON.parse(content) as PackageJson;
  } catch {
    return {};
  }
}

/**
 * Builds a "Declared Modules" summary section listing every `declare module` block
 * found in the file. Each entry is a clickable link (via NavigateToId) that jumps
 * to the corresponding subpath-export section in the review body.
 *
 * Only emitted when the file contains explicit named `declare module` blocks (i.e.
 * when at least one key in subpathMap is not ".").
 */
function buildModulesListLines(subpaths: string[]): ReviewLine[] {
  const header: ReviewLine = {
    LineId: "Modules",
    Tokens: [
      buildToken({
        Kind: TokenKind.Comment,
        Value: "// Declared Modules",
        NavigationDisplayName: "Modules",
        SkipDiff: true,
      }),
    ],
    Children: subpaths.map((subpath) => ({
      Tokens: [
        buildToken({
          Kind: TokenKind.StringLiteral,
          Value: `"${subpath}"`,
          NavigateToId: `Subpath-export-${subpath}`,
          SkipDiff: true,
        }),
      ],
    })),
  };
  return [header, { RelatedToLine: "Modules", Tokens: [] }];
}

/**
 * Builds the Dependencies section of the review from a package.json dependency map.
 * Reuses the same logic contract as the .api.json path (SkipDiff for @azure/* and tslib).
 */
function buildDependencyLines(
  dependencies: Record<string, string> | undefined,
): ReviewLine[] {
  if (!dependencies || Object.keys(dependencies).length === 0) return [];

  const knownSkip = new Set(["tslib"]);
  function shouldSkip(dep: string): boolean {
    return dep.startsWith("@azure") || knownSkip.has(dep);
  }

  const header: ReviewLine = {
    LineId: "Dependencies",
    Tokens: [
      buildToken({
        Kind: TokenKind.StringLiteral,
        Value: "Dependencies:",
        NavigationDisplayName: "Dependencies",
        RenderClasses: ["dependencies"],
        SkipDiff: true,
      }),
    ],
    Children: Object.entries(dependencies).map(([name, version]) => ({
      Tokens: [
        buildToken({ Kind: TokenKind.StringLiteral, Value: name, SkipDiff: shouldSkip(name) }),
        buildToken({ Kind: TokenKind.Punctuation, Value: ":", SkipDiff: true }),
        buildToken({ Kind: TokenKind.StringLiteral, Value: ` ${version}`, SkipDiff: true }),
      ],
    })),
  };

  return [header, { RelatedToLine: "Dependencies", Tokens: [] }];
}

export interface GenerateApiViewFromDtsOptions {
  /** Path to the .d.ts file */
  dtsFilePath: string;
  /** Override package name (defaults to name from adjacent package.json) */
  packageName?: string;
  /** Override package version (defaults to version from adjacent package.json) */
  packageVersion?: string;
  parserVersion: string;
  crossLanguageDefinitionIds?: Record<string, string>;
  crossLanguagePackageId?: string;
}

/**
 * Generates an APIView CodeFile from a TypeScript `.d.ts` declaration file.
 *
 * - If `declare module "..."` blocks are present, each becomes a subpath export.
 * - Otherwise the entire file is treated as the default `"."` entry point.
 * - Package name / version are read from an adjacent package.json unless overridden.
 * - TSDoc `@beta`, `@alpha`, and `@deprecated` tags are honoured.
 */
export async function generateApiViewFromDts(
  options: GenerateApiViewFromDtsOptions,
): Promise<CodeFile> {
  const { dtsFilePath, parserVersion, crossLanguageDefinitionIds, crossLanguagePackageId } = options;

  const pkg = await tryReadPackageJson(dtsFilePath);

  const packageName = options.packageName ?? pkg.name ?? path.basename(dtsFilePath, ".d.ts");
  const packageVersion = options.packageVersion ?? pkg.version ?? "";
  const dependencies = pkg.dependencies;

  const meta: DtsMetadata = {
    Name: packageName + (packageVersion ? `(${packageVersion})` : ""),
    PackageName: packageName,
    PackageVersion: packageVersion,
    ParserVersion: parserVersion,
    Language: "JavaScript",
  };

  // Parse the .d.ts file into per-subpath ReviewLine arrays.
  // ALL declared modules are included — both package-owned modules and any
  // third-party modules declared inline (e.g. "openai", "@azure/core-paging").
  const subpathMap = parseDtsFile({ filePath: dtsFilePath, packageName });

  const reviewLines: ReviewLine[] = [];

  // Dependencies (same structure as .api.json path)
  reviewLines.push(...buildDependencyLines(dependencies));

  // Declared Modules summary — only when explicit `declare module` blocks exist.
  // The "." fallback (no declare module blocks in file) is not meaningful to list.
  const moduleKeys = [...subpathMap.keys()];
  if (moduleKeys.some((k) => k !== ".")) {
    reviewLines.push(...buildModulesListLines(moduleKeys));
  }

  // One subpath-export entry per entry point (all modules, including third-party)

  // Hoist the cross-language injector so it is not recreated on every iteration.
  // Safe to call unconditionally; guards internally against missing mapping.
  function injectCrossLang(reviewLine: ReviewLine): void {
    if (crossLanguageDefinitionIds && reviewLine.LineId) {
      const crossId = crossLanguageDefinitionIds[reviewLine.LineId];
      if (crossId) reviewLine.CrossLanguageId = crossId;
    }
    reviewLine.Children?.forEach(injectCrossLang);
  }

  for (const [subpath, parsed] of subpathMap) {
    const tokens = [
      buildToken({
        Kind: TokenKind.StringLiteral,
        Value: ` "${subpath}"`,
        NavigationDisplayName: `"${subpath}"`,
      }),
    ];

    // Include version comment if present (e.g. "// 2.0.2")
    if (parsed.versionComment) {
      tokens.push(
        buildToken({
          Kind: TokenKind.Comment,
          Value: ` ${parsed.versionComment}`,
          SkipDiff: true,
        }),
      );
    }

    const exportLine: ReviewLine = {
      LineId: `Subpath-export-${subpath}`,
      Tokens: tokens,
      Children: parsed.lines,
    };

    parsed.lines.forEach(injectCrossLang);

    reviewLines.push(exportLine);
    reviewLines.push({ RelatedToLine: exportLine.LineId, Tokens: [] });
  }

  const codeFile: CodeFile = {
    ...meta,
    ReviewLines: reviewLines,
  };

  if (crossLanguagePackageId !== undefined || crossLanguageDefinitionIds !== undefined) {
    codeFile.CrossLanguageMetadata = {
      CrossLanguagePackageId: crossLanguagePackageId ?? "",
      CrossLanguageDefinitionId: crossLanguageDefinitionIds ?? {},
    };
  }

  return codeFile;
}
