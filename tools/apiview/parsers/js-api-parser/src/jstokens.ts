// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import jsTokens from "js-tokens";
import { type ReviewLine, type ReviewToken, TokenKind } from "./models";
import {
  ApiItem,
  ApiItemKind,
  type ExcerptToken,
  ExcerptTokenKind,
  TypeParameter,
} from "@microsoft/api-extractor-model";

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
 * Returns true if the kind represents member of a type; false otherwise.
 * @param kind
 * @returns
 */
function isTypeMember(kind: ApiItemKind): boolean {
  return (
    kind === ApiItemKind.CallSignature ||
    kind === ApiItemKind.Constructor ||
    kind === ApiItemKind.ConstructSignature ||
    kind === ApiItemKind.IndexSignature ||
    kind === ApiItemKind.Method ||
    kind === ApiItemKind.MethodSignature ||
    kind === ApiItemKind.Property ||
    kind === ApiItemKind.PropertySignature
  );
}

/**
 * Returns true if the kind represents module function; false otherwise.
 * @param kind
 * @returns
 */
function isFunction(kind: ApiItemKind): boolean {
  return kind === ApiItemKind.Function;
}

/**
 * Returns true if the kind represents an enum member; false otherwise.
 * @param kind
 * @returns
 */
function isEnumMember(kind: ApiItemKind): boolean {
  return kind === ApiItemKind.EnumMember;
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

function isPropertySignature(memberKind: ApiItemKind, tokens: jsTokens.Token[]): boolean {
  return (
    (memberKind === ApiItemKind.PropertySignature &&
      tokens.length > 2 &&
      tokens[0].type === "IdentifierName" &&
      (tokens[1].value === ":" || (tokens[1].value === "?" && tokens[2].value === ":"))) ||
    (tokens.length > 3 &&
      tokens[0].value === "readonly" &&
      tokens[1].type === "IdentifierName" &&
      (tokens[2].value === ":" || (tokens[2].value === "?" && tokens[3].value === ":")))
  );
}

function isMethodSignature(memberKind: ApiItemKind, tokens: jsTokens.Token[]): boolean {
  return (
    memberKind === ApiItemKind.MethodSignature &&
    tokens.length > 1 &&
    tokens[0].type === "IdentifierName" &&
    (tokens[1].value === "(" || tokens[1].value === "<")
  );
}
/**
 * Returns render class string of an {@link ApiItemKind}
 * @param kind
 * @returns
 */
function getRenderClass(kind: ApiItemKind) {
  let result: string = "";
  if (
    kind === ApiItemKind.Interface ||
    kind === ApiItemKind.Class ||
    kind === ApiItemKind.Namespace ||
    kind === ApiItemKind.Enum
  ) {
    result = kind.toLowerCase();
  } else if (kind === ApiItemKind.Function) {
    result = "method";
  } else if (kind === ApiItemKind.TypeAlias) {
    result = "struct";
  }
  return result;
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
 * @param item
 * @returns
 */
export function splitAndBuild(reviewTokens: ReviewToken[], s: string, item: ApiItem) {
  // Not sure why api.json uses "export declare function", while api.md uses "export function".
  // Use the latter because that's how we normally define it in the TypeScript source code.
  const lines = s
    .replace(/export declare function/g, "export function")
    .replace(/export declare enum/g, "export enum")
    .split("\n");
  const { kind: memberKind, displayName: currentTypeName } = item;
  const currentTypeid = item.canonicalReference.toString();
  for (const l of lines) {
    const tokens: jsTokens.Token[] = Array.from(jsTokens(l));
    for (const token of tokens) {
      let reviewToken: ReviewToken | undefined;
      if (
        isKeyword(token.value) &&
        !isPropertySignature(memberKind, tokens) &&
        !isMethodSignature(memberKind, tokens)
      ) {
        reviewToken = buildToken({
          Kind: TokenKind.Keyword,
          Value: token.value,
        });
      } else if (token.value === currentTypeName) {
        reviewToken = buildToken({
          Kind: TokenKind.MemberName,
          Value: token.value,
        });
        const renderClass = getRenderClass(memberKind);
        if (renderClass !== "") {
          reviewToken.RenderClasses = [renderClass];
        }
        if (!isTypeMember(memberKind)) {
          reviewToken.NavigateToId = currentTypeid;
          if (!isEnumMember(memberKind)) {
            reviewToken.NavigationDisplayName = token.value;
          }
          if (!isFunction(memberKind)) {
            reviewToken.Kind = TokenKind.TypeName;
          }
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
        const typeParameters = isTypeMember(item.kind)
          ? (item.parent as unknown as { readonly typeParameters: ReadonlyArray<TypeParameter> })
              .typeParameters
          : (item as unknown as { readonly typeParameters: ReadonlyArray<TypeParameter> })
              .typeParameters;
        if (typeParameters?.some((tp) => tp.name === token.value)) {
          reviewToken.Kind = TokenKind.TypeName;
        }
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
  item: ApiItem,
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
          splitAndBuild(reviewTokens, l, item);
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
    (reviewToken?.Kind === TokenKind.Punctuation &&
      (reviewToken?.Value === "|" || reviewToken?.Value === "&")) ||
    (reviewToken?.Kind === TokenKind.Keyword && reviewToken?.Value === "is")
  );
}
