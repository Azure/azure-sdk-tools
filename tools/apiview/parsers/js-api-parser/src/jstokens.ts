// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import jsTokens from "js-tokens";
import { type ReviewToken, TokenKind } from "./models";

// The list of keywords is derived from https://github.com/microsoft/TypeScript/blob/aa9df4d68795052d1681ac7dc5f66d6362c3f3cb/src/compiler/scanner.ts#L135
const TS_KEYWORDS = new Set<string>([
  "abstract",
  "accessor",
  "any",
  "as",
  "asserts",
  "assert",
  "bigint",
  "boolean",
  "break",
  "case",
  "catch",
  "class",
  "continue",
  "const",
  "constructor",
  "debugger",
  "declare",
  "default",
  "delete",
  "do",
  "else",
  "enum",
  "export",
  "extends",
  "false",
  "finally",
  "for",
  "from",
  "function",
  "get",
  "global",
  "if",
  "implements",
  "import",
  "in",
  "infer",
  "instanceof",
  "interface",
  "intrinsic",
  "is",
  "keyof",
  "let",
  "module",
  "namespace",
  "never",
  "new",
  "null",
  "number",
  "object",
  "package",
  "private",
  "protected",
  "public",
  "override",
  "out",
  "readonly",
  "require",
  "return",
  "satisfies",
  "set",
  "static",
  "string",
  "super",
  "switch",
  "this",
  "throw",
  "true",
  "try",
  "typeof",
  "undefined",
  "unique",
  "unknown",
  "using",
  "var",
  "void",
  "while",
  "with",
  "yield",
]);

function isKeyword(s: string): boolean {
  return TS_KEYWORDS.has(s);
}

function isTypeMember(id: string): boolean {
  return id.endsWith(":member");
}

export function buildToken(options: ReviewToken): ReviewToken {
  return {
    ...options,
    HasSuffixSpace: options.HasSuffixSpace ?? false,
  };
}

export function splitAndBuild(
  s: string,
  currentTypeid: string,
  currentTypeName: string,
  memberKind: string,
) {
  const reviewTokens: ReviewToken[] = [];
  const lines = s.split("\n");
  for (const l of lines) {
    const tokens: jsTokens.Token[] = Array.from(jsTokens(l));
    for (const token of tokens) {
      let reviewToken: ReviewToken | undefined;
      if (isKeyword(token.value)) {
        reviewToken = buildToken({
          Kind: TokenKind.Keyword,
          Value: token.value,
        });
      } else if (token.value === currentTypeName) {
        reviewToken = buildToken({
          Kind: TokenKind.TypeName,
          Value: token.value,
        });
        if (memberKind !== "") {
          reviewToken.RenderClasses = [memberKind];
        }
        if (!isTypeMember(currentTypeid)) {
          reviewToken.NavigateToId = currentTypeid;
          reviewToken.NavigationDisplayName = token.value;
        }
      } else if (token.type === "StringLiteral") {
        reviewToken = buildToken({
          Kind: TokenKind.StringLiteral,
          Value: token.value,
        });
      } else if (token.type === "Punctuator") {
        reviewToken = buildToken({
          Kind: TokenKind.Punctuation,
          Value: token.value,
        });
      } else if (token.type === "WhiteSpace" && reviewTokens.length > 0) {
        reviewTokens[reviewTokens.length - 1].HasSuffixSpace = true;
      } else if (token.type !== "WhiteSpace") {
        reviewToken = buildToken({
          Kind: TokenKind.Text,
          Value: token.value,
        });
      }

      if (token.type !== "WhiteSpace") {
        reviewTokens.push(reviewToken);
      }
    }
  }
  return reviewTokens;
}
