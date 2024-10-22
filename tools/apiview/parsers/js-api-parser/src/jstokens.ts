// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import jsTokens from "js-tokens";
import { ReviewLine, type ReviewToken, TokenKind } from "./models";
import { ExcerptToken, ExcerptTokenKind } from "@microsoft/api-extractor-model";

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
 * Returns true if the id represents module function; false otherwise.
 * @param id
 * @returns
 */
function isFunction(id: string): boolean {
  return id.includes(":function");
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
 * @param reviewTokens - {@link ReviewToken} array to add the built token
 * @param s
 * @param currentTypeid
 * @param currentTypeName
 * @param memberKind
 * @returns
 */
export function splitAndBuild(
  reviewTokens: ReviewToken[],
  s: string,
  currentTypeid: string,
  currentTypeName: string,
  memberKind: string,
) {
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
          if (!isFunction(currentTypeid)) {
            reviewToken.NavigateToId = currentTypeid;
          }
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
        }
        if (reviewTokens.length > 0) {
          // Make previous non-whitespace token to have a space
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

/**
 * Builds review line for excerpt tokens that contains multi-line code (includes("\n") === true).
 *   Api Extractor doesn't support in-line types well. All we have from api.json is just a list of
 *   excerpt tokens whose values contain multi-line code, so we have to parse the code and stitch tokens
 *   together to form the review line.
 * @param line the {@link ReviewLine} to add tokens and children if any
 * @param excerptTokens {@link ExcerptToken}s of the api item.
 * @param currentTypeid
 * @param currentTypeName
 * @param memberKind
 */
export function splitAndBuildMultipleLine(
  line: ReviewLine,
  excerptTokens: readonly ExcerptToken[],
  currentTypeid: string,
  currentTypeName: string,
  memberKind: string,
) {
  let firstReviewLine: boolean = true;
  for (const excerpt of excerptTokens) {
    if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
      const token = buildToken({
        Kind: TokenKind.TypeName,
        NavigateToId: excerpt.canonicalReference.toString(),
        Value: excerpt.text,
      });
      if (line.Children.length > 0) {
        line.Children[line.Children.length - 1].Tokens.push(token);
      } else {
        line.Tokens.push(token);
      }
    } else {
      const codeLines = excerpt.text.split("\n");
      let firstCodeLine: boolean = true;
      for (const l of codeLines) {
        const reviewTokens: ReviewToken[] = [];
        if (l.match(/\/\*\*|\s\*/)) {
          const commentToken = buildToken({
            Kind: TokenKind.Comment,
            IsDocumentation: true,
            Value: l,
          });
          reviewTokens.push(commentToken);
        } else {
          splitAndBuild(reviewTokens, l, currentTypeid, currentTypeName, memberKind);
        }

        if (firstReviewLine) {
          line.Tokens.push(...reviewTokens);
          firstCodeLine = false;
          firstReviewLine = false;
        } else if (firstCodeLine) {
          // code before first "\n" should be in last review line
          if (line.Children.length > 0) {
            if (hasLeadingSpace(reviewTokens[0])) {
              line.Children[line.Children.length - 1].Tokens[
                line.Children[line.Children.length - 1].Tokens.length - 1
              ].HasSuffixSpace = true;
            }
            line.Children[line.Children.length - 1].Tokens.push(...reviewTokens);
          } else {
            if (line.Tokens.length > 0 && hasLeadingSpace(reviewTokens[0])) {
              line.Tokens[line.Tokens.length - 1].HasSuffixSpace = true;
            }
            line.Tokens.push(...reviewTokens);
          }
          firstCodeLine = false;
        } else {
          const childLine: ReviewLine = {
            Tokens: reviewTokens,
          };
          line.Children.push(childLine);
        }
      }
    }
  }
}

/**
 * Whether a {@link ReviewToken} needs a leading whitespace.
 * @param reviewToken
 * @returns
 */
function hasLeadingSpace(reviewToken?: ReviewToken) {
  return (
    reviewToken?.Kind === TokenKind.Punctuation &&
    (reviewToken?.Value === "|" || reviewToken?.Value === "&")
  );
}
