// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import jsTokens from "js-tokens";
import { ReviewToken, TokenKind } from "./models";

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
      if (isKeyword(token.value)) {
        reviewTokens.push({
          Kind: TokenKind.Keyword,
          Value: token.value,
        });
      } else if (token.value === currentTypeName) {
        const t: ReviewToken = {
          Kind: TokenKind.TypeName,
          Value: token.value,
        };
        if (memberKind !== "") {
          t.RenderClasses = [memberKind];
        }
        if (!isTypeMember(currentTypeid)) {
          t.NavigateToId = currentTypeid;
          t.NavigationDisplayName = token.value;
        }
        reviewTokens.push(t);
      } else if (token.type === "StringLiteral") {
        reviewTokens.push({
          Kind: TokenKind.StringLiteral,
          Value: token.value,
        });
      } else if (token.type === "Punctuator") {
        reviewTokens.push({
          Kind: TokenKind.Punctuation,
          Value: token.value,
        });
      } else if (token.type === "WhiteSpace" && reviewTokens.length > 0) {
        reviewTokens[reviewTokens.length - 1].HasSuffixSpace = true;
      } else if (token.type !== "WhiteSpace") {
        reviewTokens.push({
          Kind: TokenKind.Text,
          Value: token.value,
        });
      }
    }
  }
  return reviewTokens;
}
