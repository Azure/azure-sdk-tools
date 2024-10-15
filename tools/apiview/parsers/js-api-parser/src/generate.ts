// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { CodeFile, ReviewLine, ReviewToken, TokenKind } from "./models";
import {
  ApiDeclaredItem,
  ApiDocumentedItem,
  ApiEntryPoint,
  ApiItem,
  ApiItemKind,
  ApiModel,
  ExcerptTokenKind,
  ReleaseTag,
} from "@microsoft/api-extractor-model";
import { buildToken, splitAndBuild, splitAndBuildMultipleLine } from "./jstokens";

interface Metadata {
  Name: string;
  PackageName: string;
  PackageVersion: string;
  ParserVersion: string;
  Language: "JavaScript";
}

// key: item's canonical reference, value: sub paths that include it
const exported = new Map<string, Set<string>>();

/**
 * Builds a blank line for the review
 * @param relatedToLine line id to associate the result with
 * @returns
 */
function emptyLine(relatedToLine?: string): ReviewLine {
  return { RelatedToLine: relatedToLine, Tokens: [] };
}

/**
 * Builds review for the package's direct dependencies
 * @param reviewLines The result array to push {@link ReviewLine}s to
 * @param dependencies dependencies of name and version pairs
 * @returns
 */
function buildDependencies(reviewLines: ReviewLine[], dependencies: Record<string, string>) {
  if (!dependencies) {
    return;
  }
  const keys = Object.keys(dependencies);
  if (keys.length === 0) {
    return;
  }

  const header: ReviewLine = {
    LineId: "Dependencies",
    Tokens: [
      buildToken({
        Kind: TokenKind.StringLiteral,
        Value: "Dependencies:",
        NavigationDisplayName: "Dependencies",
        RenderClasses: ["dependencies"],
      }),
    ],
  };

  const dependencyLines: ReviewLine[] = [];
  for (const dependency of Object.keys(dependencies)) {
    const nameToken: ReviewToken = buildToken({
      Kind: TokenKind.StringLiteral,
      Value: dependency,
    });
    const versionToken: ReviewToken = buildToken({
      Kind: TokenKind.StringLiteral,
      Value: ` ${dependencies[dependency]}`,
      SkipDiff: true,
    });
    const dependencyLine: ReviewLine = {
      Tokens: [nameToken, buildToken({ Kind: TokenKind.Punctuation, Value: ":" }), versionToken],
    };
    dependencyLines.push(dependencyLine);
  }
  header.Children = dependencyLines;
  reviewLines.push(header);
  reviewLines.push(emptyLine(header.LineId));
}

/**
 * Gets the name of the subpath export given an entrypoint
 * @param entryPoint
 * @returns
 */
function getSubPathName(entryPoint: ApiEntryPoint): string {
  return entryPoint.name.length > 0 ? `./${entryPoint.name}` : ".";
}

/**
 * Builds review for all the entrypoints.  Each entrypoint represents a subpath export.
 * The regular output api.json file from api-extractor currently only contains one single entrypoint.
 * The dev-tool in azure-sdk-for-js repository augments the api to have multiple entrypoints,
 * one for each subpath export.
 * @param reviewLines The result array to push {@link ReviewLine}s to
 * @param apiModel {@link ApiModel} object loaded from the .api.json file
 */
function buildSubpathExports(reviewLines: ReviewLine[], apiModel: ApiModel) {
  for (const modelPackage of apiModel.packages) {
    for (const entryPoint of modelPackage.entryPoints) {
      const subpath = getSubPathName(entryPoint);
      const exportLine: ReviewLine = {
        LineId: `Subpath-export-${subpath}`,
        Tokens: [
          buildToken({
            Kind: TokenKind.StringLiteral,
            Value: ` "${subpath}"`,
            NavigationDisplayName: `"${subpath}"`,
          }),
        ],
        Children: [],
      };

      for (const member of entryPoint.members) {
        const canonicalRef = member.canonicalReference.toString();
        const containingExport = exported.get(canonicalRef) ?? new Set<string>();
        if (!containingExport.has(subpath)) {
          containingExport.add(subpath);
          exported.set(canonicalRef, containingExport);
        }
        buildMember(exportLine.Children!, member);
      }
      reviewLines.push(exportLine);
      reviewLines.push(emptyLine(exportLine.LineId));
    }
  }
}

/**
 * Builds reference doc comments
 * @param reviewLines The result array to push {@link ReviewLine}s to
 * @param item {@link ApiItem} instance
 * @param relatedTo Line id of the line with which the documentation is associated
 */
function buildDocumentation(reviewLines: ReviewLine[], item: ApiItem, relatedTo: string) {
  if (item instanceof ApiDocumentedItem) {
    const lines = item.tsdocComment
      ?.emitAsTsdoc()
      .split("\n")
      .filter((l) => l.trim() !== "");
    for (const l of lines ?? []) {
      const docToken: ReviewToken = buildToken({
        Kind: TokenKind.Comment,
        IsDocumentation: true,
        Value: l,
      });
      reviewLines.push({
        Tokens: [docToken],
        RelatedToLine: relatedTo,
      });
    }
  }
}

/**
 * Returns release tag string corresponding to the tag
 * @param item {@link ApiItem} instance with releaseTag
 * @returns release tag string
 */
function getReleaseTag(item: ApiItem & { releaseTag?: ReleaseTag }): string | undefined {
  switch (item.releaseTag) {
    case ReleaseTag.Beta:
      return "beta";
    case ReleaseTag.Alpha:
      return "alpha";
    default:
      return undefined;
  }
}

const ANNOTATION_TOKEN = "@";
/**
 * Builds review line for a release tag
 * @param reviewLines The result array to push {@link ReviewLine}s to
 * @param tag release tag
 */
function buildReleaseTag(reviewLines: ReviewLine[], tag: string): void {
  const tagToken = buildToken({
    Kind: TokenKind.StringLiteral,
    Value: `${ANNOTATION_TOKEN}${tag}`,
  });
  reviewLines.push({ Tokens: [tagToken] });
}

/**
 * Checks whether a {@link @ApiItem} instance may have children or not
 * @param item The result array to push {@link ReviewLine}s to
 * @returns
 */
function mayHaveChildren(item: ApiItem): boolean {
  return (
    item.kind === ApiItemKind.Interface ||
    item.kind === ApiItemKind.Class ||
    item.kind === ApiItemKind.Namespace ||
    item.kind === ApiItemKind.Enum
  );
}

/**
 * Returns normalized item kind string of an {@link ApiDeclaredItem}
 * @param item {@link ApiDeclaredItem} instance
 * @returns
 */
function getItemKindString(item: ApiDeclaredItem) {
  let itemKind: string = "";
  if (mayHaveChildren(item)) {
    itemKind = item.kind.toLowerCase();
  } else if (item.kind === ApiItemKind.Function) {
    itemKind = "method";
  } else if (item.kind === ApiItemKind.TypeAlias) {
    itemKind = "struct";
  }
  return itemKind;
}

/**
 * Builds the token list for an Api and pushes to the review line that is passed in
 * @param line The {@link ReviewLine} to push {@link ReviewToken}s to
 * @param item {@link ApiItem} instance
 */
function buildMemberLineTokens(line: ReviewLine, item: ApiItem) {
  const itemId = item.canonicalReference.toString();
  if (item instanceof ApiDeclaredItem) {
    const itemKind: string = getItemKindString(item);

    if (item.kind === ApiItemKind.Namespace) {
      line.Tokens.push(
        ...splitAndBuild(
          `declare namespace ${item.displayName} `,
          itemId,
          item.displayName,
          itemKind,
        ),
      );
    } else {
      if (!item.excerptTokens.some((except) => except.text.includes("\n"))) {
        for (const excerpt of item.excerptTokens) {
          if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
            const token = buildToken({
              Kind: TokenKind.TypeName,
              NavigateToId: excerpt.canonicalReference.toString(),
              Value: excerpt.text,
            });
            line.Tokens.push(token);
          } else {
            line.Tokens.push(...splitAndBuild(excerpt.text, itemId, item.displayName, itemKind));
          }
        }
      } else {
        splitAndBuildMultipleLine(line, item.excerptTokens, itemId, item.displayName, itemKind);
      }
    }
  }
}

/**
 * Builds review for an {@link ApiItem}.
 * @param reviewLines The result array to push {@link ReviewLine}s to
 * @param item the Api to build review for
 */
function buildMember(reviewLines: ReviewLine[], item: ApiItem) {
  const itemId = item.canonicalReference.toString();
  const line: ReviewLine = {
    LineId: itemId,
    Children: [],
    Tokens: [],
  };

  buildDocumentation(reviewLines, item, itemId);

  const releaseTag = getReleaseTag(item);
  const parentReleaseTag = getReleaseTag(item.parent);
  if (releaseTag && releaseTag !== parentReleaseTag) {
    buildReleaseTag(reviewLines, releaseTag);
  }

  buildMemberLineTokens(line, item);

  if (mayHaveChildren(item)) {
    if (line.Tokens.length > 0) {
      line.Tokens[line.Tokens.length - 1].HasSuffixSpace = true;
    }
    if (item.members.length > 0) {
      line.Tokens.push(buildToken({ Kind: TokenKind.Punctuation, Value: "{" }));
      for (const member of item.members) {
        buildMember(line.Children!, member);
      }
      reviewLines.push(line);
      reviewLines.push({
        Tokens: [buildToken({ Kind: TokenKind.Punctuation, Value: "}" })],
        RelatedToLine: line.LineId,
        IsContextEndLine: true,
      });
    } else {
      line.Tokens.push(
        buildToken({ Kind: TokenKind.Punctuation, Value: "{", HasSuffixSpace: true }),
        buildToken({ Kind: TokenKind.Punctuation, Value: "}" }),
      );
      reviewLines.push(line);
    }
  } else {
    reviewLines.push(line);
  }

  // add blank line between types or functions
  if (
    mayHaveChildren(item) ||
    item.kind === ApiItemKind.TypeAlias ||
    item.kind === ApiItemKind.Function
  ) {
    reviewLines.push(emptyLine(line.LineId));
  }
}

function buildReview(
  review: ReviewLine[],
  dependencies: Record<string, string>,
  apiModel: ApiModel,
) {
  buildDependencies(review, dependencies);
  buildSubpathExports(review, apiModel);
}

export function generateApiview(options: {
  meta: Metadata;
  dependencies: Record<string, string>;
  apiModel: ApiModel;
}): CodeFile {
  const { meta, dependencies, apiModel } = options;
  const review: ReviewLine[] = [];
  buildReview(review, dependencies, apiModel);

  return {
    ...meta,
    ReviewLines: review,
  };
}
