import { ExcerptToken, ExcerptTokenKind } from "@microsoft/api-extractor-model";
import * as ts from "typescript";
import { ReviewToken, ReviewLine, TokenKind } from "../models";

// TypeScript keyword type SyntaxKinds
const KEYWORD_TYPE_KINDS = new Set<ts.SyntaxKind>([
  ts.SyntaxKind.StringKeyword,
  ts.SyntaxKind.NumberKeyword,
  ts.SyntaxKind.BooleanKeyword,
  ts.SyntaxKind.VoidKeyword,
  ts.SyntaxKind.NullKeyword,
  ts.SyntaxKind.UndefinedKeyword,
  ts.SyntaxKind.NeverKeyword,
  ts.SyntaxKind.AnyKeyword,
  ts.SyntaxKind.UnknownKeyword,
  ts.SyntaxKind.ObjectKeyword,
  ts.SyntaxKind.SymbolKeyword,
  ts.SyntaxKind.BigIntKeyword,
]);

const PUNCTUATION_CHARS = new Set([
  "|",
  "&",
  ":",
  ";",
  "?",
  "(",
  ")",
  "[",
  "]",
  "{",
  "}",
  "<",
  ">",
  ",",
  ".",
  "...",
  "=>",
]);

// Build a reverse lookup from string to SyntaxKind for keyword types
const BUILTIN_TYPE_MAP: Map<string, ts.SyntaxKind> = new Map();
for (const kind of KEYWORD_TYPE_KINDS) {
  const text = ts.tokenToString(kind);
  if (text) {
    BUILTIN_TYPE_MAP.set(text, kind);
  }
}

function isBuiltInType(text: string): boolean {
  return BUILTIN_TYPE_MAP.has(text);
}

/** Helper to create a token with common properties */
export function createToken(
  kind: TokenKind,
  value: string,
  options?: {
    hasSuffixSpace?: boolean;
    hasPrefixSpace?: boolean;
    navigateToId?: string;
    deprecated?: boolean;
  },
): ReviewToken {
  return {
    Kind: kind,
    Value: value,
    HasSuffixSpace: options?.hasSuffixSpace ?? false,
    HasPrefixSpace: options?.hasPrefixSpace ?? false,
    NavigateToId: options?.navigateToId,
    IsDeprecated: options?.deprecated,
  };
}

export function needsLeadingSpace(value: string): boolean {
  return value === "|" || value === "&" || value === "is" || value === "extends";
}

export function needsTrailingSpace(value: string): boolean {
  return value === "|" || value === "&" || value === "is" || value === "extends";
}

function getTokenKind(text: string): TokenKind {
  if (isBuiltInType(text)) {
    return TokenKind.Keyword;
  }
  if (PUNCTUATION_CHARS.has(text)) {
    return TokenKind.Punctuation;
  }
  return TokenKind.Text;
}

/** Process excerpt tokens and add them to the tokens array */
export function processExcerptTokens(
  excerptTokens: readonly ExcerptToken[],
  tokens: ReviewToken[],
  deprecated?: boolean,
): void {
  for (const excerpt of excerptTokens) {
    const text = excerpt.text;
    if (!text || !text.trim()) continue;

    const lines = text.split(/(\r?\n)/);

    for (const segment of lines) {
      if (segment === "\n" || segment === "\r\n") {
        continue;
      }

      const trimmedText = segment.trim();
      if (!trimmedText) continue;

      const hasPrefixSpace =
        segment.startsWith(" ") || segment.startsWith("\t") || needsLeadingSpace(trimmedText);
      const hasSuffixSpace =
        segment.endsWith(" ") || segment.endsWith("\t") || needsTrailingSpace(trimmedText);

      if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
        tokens.push(
          createToken(TokenKind.TypeName, trimmedText, {
            navigateToId: excerpt.canonicalReference.toString(),
            hasPrefixSpace,
            hasSuffixSpace,
            deprecated,
          }),
        );
      } else {
        const tokenKind = getTokenKind(trimmedText);
        tokens.push(
          createToken(tokenKind, trimmedText, {
            hasPrefixSpace,
            hasSuffixSpace,
            deprecated,
          }),
        );
      }
    }
  }
}

/**
 * Creates indentation tokens (tabs) for a given depth
 */
function createIndentation(depth: number, deprecated?: boolean): ReviewToken | undefined {
  if (depth <= 0) return undefined;
  const spaces = "  ".repeat(depth * 4); // 8 spaces per level
  return createToken(TokenKind.Text, spaces, { deprecated });
}

/**
 * Parses a TypeScript type AST node and builds ReviewLines with proper nesting
 */
export function buildTypeNodeTokens(
  node: ts.Node,
  tokens: ReviewToken[],
  deprecated?: boolean,
  depth: number = 0,
): ReviewLine[] | undefined {
  const children: ReviewLine[] = [];

  if (ts.isTypeLiteralNode(node)) {
    tokens.push(createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }));

    for (const member of node.members) {
      const memberTokens: ReviewToken[] = [];

      // Add indentation for this level
      const indent = createIndentation(depth + 1, deprecated);
      if (indent) memberTokens.push(indent);

      const memberChildren = buildTypeElementTokens(member, memberTokens, deprecated, depth + 1);

      const childLine: ReviewLine = { Tokens: memberTokens };
      if (memberChildren?.length) {
        childLine.Children = memberChildren;
      }
      children.push(childLine);
    }

    // Add indentation for closing brace
    const closingTokens: ReviewToken[] = [];
    const closingIndent = createIndentation(depth, deprecated);
    if (closingIndent) closingTokens.push(closingIndent);
    // ensure 4 spaces before '}'
    closingTokens.push(createToken(TokenKind.Punctuation, "    ", { deprecated }));
    closingTokens.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
    closingTokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

    children.push({
      Tokens: closingTokens,
      IsContextEndLine: true,
    });

    return children;
  }

  if (ts.isUnionTypeNode(node)) {
    // Handle union types: TypeA | TypeB | { inline: string; }
    node.types.forEach((typeNode, index) => {
      if (index > 0) {
        tokens.push(
          createToken(TokenKind.Punctuation, "|", {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
      }
      const nestedChildren = buildTypeNodeTokens(typeNode, tokens, deprecated, depth);
      if (nestedChildren?.length) {
        children.push(...nestedChildren);
      }
    });
    return children.length > 0 ? children : undefined;
  }

  if (ts.isIntersectionTypeNode(node)) {
    // Handle intersection types: TypeA & TypeB & { inline: string; }
    node.types.forEach((typeNode, index) => {
      if (index > 0) {
        tokens.push(
          createToken(TokenKind.Punctuation, "&", {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
      }
      const nestedChildren = buildTypeNodeTokens(typeNode, tokens, deprecated, depth);
      if (nestedChildren?.length) {
        children.push(...nestedChildren);
      }
    });
    return children.length > 0 ? children : undefined;
  }

  if (ts.isParenthesizedTypeNode(node)) {
    // Handle parenthesized types: (TypeA | TypeB)
    tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
    const nestedChildren = buildTypeNodeTokens(node.type, tokens, deprecated, depth);
    tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
    if (nestedChildren?.length) {
      children.push(...nestedChildren);
    }
    return children.length > 0 ? children : undefined;
  }

  if (ts.isArrayTypeNode(node)) {
    const nestedChildren = buildTypeNodeTokens(node.elementType, tokens, deprecated, depth);
    tokens.push(createToken(TokenKind.Punctuation, "[", { deprecated }));
    tokens.push(createToken(TokenKind.Punctuation, "]", { deprecated }));
    if (nestedChildren?.length) {
      children.push(...nestedChildren);
    }
    return children.length > 0 ? children : undefined;
  }

  if (ts.isTypeReferenceNode(node)) {
    tokens.push(createToken(TokenKind.TypeName, node.typeName.getText(), { deprecated }));
    if (node.typeArguments?.length) {
      tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
      node.typeArguments.forEach((arg, index) => {
        if (index > 0) {
          tokens.push(
            createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }),
          );
        }
        const nestedChildren = buildTypeNodeTokens(arg, tokens, deprecated, depth);
        if (nestedChildren?.length) {
          children.push(...nestedChildren);
        }
      });
      tokens.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
    }
    return children.length > 0 ? children : undefined;
  }

  const text = node.getText();
  const tokenKind = getTokenKind(text);
  tokens.push(createToken(tokenKind, text, { deprecated }));
  return undefined;
}

/**
 * Builds tokens for a TypeScript type element AST node
 */
export function buildTypeElementTokens(
  member: ts.TypeElement,
  tokens: ReviewToken[],
  deprecated?: boolean,
  depth: number = 0,
): ReviewLine[] | undefined {
  // Handle modifiers (readonly, static, etc.)
  const modifiers = ts.canHaveModifiers(member) ? ts.getModifiers(member) : undefined;
  if (modifiers) {
    for (const modifier of modifiers) {
      const modifierText = modifier.getText();
      tokens.push(
        createToken(TokenKind.Keyword, modifierText, { hasSuffixSpace: true, deprecated }),
      );
    }
  }

  if (ts.isPropertySignature(member)) {
    const name = member.name.getText();
    tokens.push(createToken(TokenKind.MemberName, name, { deprecated }));

    if (member.questionToken) {
      tokens.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
    }

    tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));

    if (member.type) {
      const children = buildTypeNodeTokens(member.type, tokens, deprecated, depth);
      if (!children) {
        tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
      }
      return children;
    }
  } else if (ts.isIndexSignatureDeclaration(member)) {
    tokens.push(createToken(TokenKind.Punctuation, "[", { deprecated }));

    for (const param of member.parameters) {
      tokens.push(createToken(TokenKind.MemberName, param.name.getText(), { deprecated }));
      tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      if (param.type) {
        const typeText = param.type.getText();
        const tokenKind = getTokenKind(typeText);
        tokens.push(createToken(tokenKind, typeText, { deprecated }));
      }
    }

    tokens.push(createToken(TokenKind.Punctuation, "]", { deprecated }));
    tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));

    if (member.type) {
      const children = buildTypeNodeTokens(member.type, tokens, deprecated, depth);
      if (!children) {
        tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
      }
      return children;
    }
  } else if (ts.isMethodSignature(member)) {
    // Handle method signatures if needed
    const name = member.name.getText();
    tokens.push(createToken(TokenKind.MemberName, name, { deprecated }));
    // Add method signature handling as needed...
    tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    return undefined;
  }

  tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
  return undefined;
}

/**
 * Parses type text using TypeScript compiler and builds tokens with proper children structure
 */
export function parseTypeText(
  typeText: string,
  tokens: ReviewToken[],
  deprecated?: boolean,
  depth: number = 0,
): ReviewLine[] | undefined {
  const sourceText = `type T = ${typeText};`;
  const sourceFile = ts.createSourceFile(
    "temp.ts",
    sourceText,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TS,
  );

  const typeAlias = sourceFile.statements[0];
  if (ts.isTypeAliasDeclaration(typeAlias) && typeAlias.type) {
    return buildTypeNodeTokens(typeAlias.type, tokens, deprecated, depth);
  }

  // Fallback: just add as text
  const tokenKind = getTokenKind(typeText.trim());
  tokens.push(createToken(tokenKind, typeText.trim(), { deprecated }));
  return undefined;
}
