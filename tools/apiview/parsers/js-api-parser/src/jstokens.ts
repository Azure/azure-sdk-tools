// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import jsTokens from "js-tokens";
import { ReviewLine, type ReviewToken, TokenKind } from "./models";
import { ExcerptToken } from "@microsoft/api-extractor-model";

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
  "type",
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

/**
 * Returns true if the string is one of the TS keywords; false otherwise.
 * @param s
 * @returns
 */
function isKeyword(s: string): boolean {
  return TS_KEYWORDS.has(s);
}

/**
 * Returns true if the id represents member of a type; false otherwise.
 * @param id
 * @returns
 */
function isTypeMember(id: string): boolean {
  return id.includes(":member");
}

/**
 * Returns a {@link ReviewToken} with HasSuffixSpace of false by default
 * @param options
 * @returns
 */
export function buildToken(options: ReviewToken): ReviewToken {
  return {
    ...options,
    HasSuffixSpace: options.HasSuffixSpace ?? false,
  };
}

/**
 * Builds a list of tokens for the input string of TypeScript code. The string is split
 * into jsTokens then ReviewToken is generated for each jsToken:
 *   - keywords => keywords
 *   - Type -> Type token
 *     - if its value is the same as defining type and it is not a member, add navigation
 *   - string literals => string literals
 *   - punctuators => punctuators
 *   - whitespace: set hasSuffixSpace = true on previous {@link ReviewToken}
 *   - otherwise add a normal Text token
 * @param line the {@link ReviewLine} to add tokens and children if any
 * @param s
 * @param currentTypeid
 * @param currentTypeName
 * @param memberKind
 * @returns
 */
export function splitAndBuild(
  s: string,
  currentTypeid: string,
  currentTypeName: string,
  memberKind: string,
) {
  const reviewTokens: ReviewToken[] = [];
  // Not sure why api.json uses "export declare function", while api.md uses "export function".
  // Use the latter because that's how we normally define it in the TypeScript source code.
  const lines = s.replace(/export declare function/g, "export function").split("\n");
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
          Kind: TokenKind.MemberName,
          Value: token.value,
        });
        if (memberKind !== "") {
          reviewToken.RenderClasses = [memberKind];
        }
        if (!isTypeMember(currentTypeid)) {
          reviewToken.NavigateToId = currentTypeid;
          reviewToken.NavigationDisplayName = token.value;
          reviewToken.Kind = TokenKind.TypeName;
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
      } else if (token.type === "WhiteSpace") {
        if (token.value.length > 1) {
          // very likely it's indentation, added it
          reviewToken = buildToken({
            Kind: TokenKind.Text,
            Value: token.value,
          });
        } else if (reviewTokens.length > 0) {
          // Make previous token to have a space
          reviewTokens[reviewTokens.length - 1].HasSuffixSpace = true;
        }
      } else {
        reviewToken = buildToken({
          Kind: TokenKind.Text,
          Value: token.value,
        });
      }

      if (token.type !== "WhiteSpace" || (token.type === "WhiteSpace" && token.value.length > 1)) {
        reviewTokens.push(reviewToken);
      }
    }
  }

  return reviewTokens;
}

export function splitAndBuildMultipleLine(
  line: ReviewLine,
  excerptTokens: readonly ExcerptToken[],
  currentTypeid: string,
  currentTypeName: string,
  memberKind: string,
) {
  let firstLine: boolean = true;
  const code = excerptTokens.map((e) => e.text).join("");
  const lines = code.split("\n").filter((l) => l.trim() !== "");
  for (const l of lines) {
    const reviewTokens: ReviewToken[] = [];
    if (l.match(/\/\*\*|\s\*/)) {
      reviewTokens.push(
        buildToken({
          Kind: TokenKind.Comment,
          IsDocumentation: true,
          Value: l,
        }),
      );
    } else {
      reviewTokens.push(...splitAndBuild(l, currentTypeid, currentTypeName, memberKind));
    }

    if (firstLine) {
      line.Tokens.push(...reviewTokens);
      firstLine = false;
    } else {
      const childLine: ReviewLine = {
        Tokens: reviewTokens,
      };
      line.Children.push(childLine);
    }
  }
}
