// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import jsTokens from "js-tokens";
import { type ReviewToken, TokenKind } from "./models";

const JS_KEYWORDS = new Set<string>([
  "break",
  "case",
  "catch",
  "class",
  "const",
  "continue",
  "debugger",
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
  "function",
  "if",
  "import",
  "in",
  "instanceof",
  "namespace",
  "new",
  "null",
  "return",
  "super",
  "switch",
  "this",
  "throw",
  "true",
  "try",
  "typeof",
  "var",
  "void",
  "while",
  "with",
  "as",
  "implements",
  "interface",
  "let",
  "package",
  "private",
  "protected",
  "public",
  "static",
  "yield",
  "any",
  "boolean",
  "constructor",
  "declare",
  "get",
  "module",
  "require",
  "number",
  "set",
  "string",
  "symbol",
  "type",
  "from",
  "of",
  "keyof",
  "readonly",
]);

function isKeyword(s: string): boolean {
  return JS_KEYWORDS.has(s);
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
