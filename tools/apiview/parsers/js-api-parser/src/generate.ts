// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { CodeFile, ReviewLine, ReviewToken, TokenKind } from "./models";
import {
  ApiDeclaredItem,
  ApiDocumentedItem,
  ApiItem,
  ApiItemKind,
  ApiModel,
  ExcerptTokenKind,
  ReleaseTag,
} from "@microsoft/api-extractor-model";
import { buildToken, splitAndBuild } from "./jstokens";

interface Metadata {
  Name: string;
  PackageName: string;
  PackageVersion: string;
  ParserVersion: string;
  Language: "JavaScript";
}

// key: item's canonical reference, value: sub paths that include it
const exported = new Map<string, Set<string>>();

function emptyLine(relatedToLine?: string): ReviewLine {
  return { RelatedToLine: relatedToLine, Tokens: [] };
}

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

function buildSubpathExports(reviewLines: ReviewLine[], apiModel: ApiModel) {
  for (const modelPackage of apiModel.packages) {
    for (const entryPoint of modelPackage.entryPoints) {
      const subpath = entryPoint.name.length > 0 ? entryPoint.name : "<default>";
      const exportLine: ReviewLine = {
        LineId: `Subpath-export-${subpath}`,
        Tokens: [
          buildToken({
            Kind: TokenKind.StringLiteral,
            Value: ` "${subpath}"`,
            NavigationDisplayName: `"${subpath}" subpath export`,
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
    }
  }
}

function buildDocumentation(reviewLines: ReviewLine[], member: ApiItem, relatedTo: string) {
  if (member instanceof ApiDocumentedItem) {
    const lines = member.tsdocComment?.emitAsTsdoc().split("\n");
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
function buildReleaseTag(reviewLines: ReviewLine[], tag: string): void {
  const tagToken = buildToken({
    Kind: TokenKind.StringLiteral,
    Value: `${ANNOTATION_TOKEN}${tag}`,
  });
  reviewLines.push({ Tokens: [tagToken] });
}

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

  if (item instanceof ApiDeclaredItem) {
    if (item.kind === ApiItemKind.Namespace) {
      line.Tokens.push(
        ...splitAndBuild(
          `declare namespace ${item.displayName} `,
          itemId,
          item.displayName,
          "namespace",
        ),
      );
    }

    let itemKind: string = "";
    switch (item.kind) {
      case ApiItemKind.Interface:
      case ApiItemKind.Class:
      case ApiItemKind.Namespace:
      case ApiItemKind.Enum:
        itemKind = item.kind.toLowerCase();
        break;
      case ApiItemKind.Function:
        itemKind = "method";
        break;
      case ApiItemKind.TypeAlias:
        itemKind = "struct";
        break;
    }

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
  }

  if (
    item.kind === ApiItemKind.Interface ||
    item.kind === ApiItemKind.Class ||
    item.kind === ApiItemKind.Namespace ||
    item.kind === ApiItemKind.Enum
  ) {
    if (item.members.length > 0) {
      line.Tokens.push(buildToken({ Kind: TokenKind.Punctuation, Value: `{` }));
      for (const member of item.members) {
        buildMember(line.Children!, member);
      }
      reviewLines.push(line);
      reviewLines.push({
        Tokens: [buildToken({ Kind: TokenKind.Punctuation, Value: `}` })],
        RelatedToLine: line.LineId,
      });
    } else {
      reviewLines.push(line);
      reviewLines.push({
        Tokens: [
          buildToken({ Kind: TokenKind.Punctuation, Value: `{`, HasSuffixSpace: true }),
          buildToken({ Kind: TokenKind.Punctuation, Value: `}` }),
        ],
      });
    }
  } else {
    reviewLines.push(line);
  }

  if (item instanceof ApiDeclaredItem && item.kind === ApiItemKind.Namespace) {
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

export function GenerateApiview(options: {
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
