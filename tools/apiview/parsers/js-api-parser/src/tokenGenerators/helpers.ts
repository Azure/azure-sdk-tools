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

function containsTypeLiteral(node: ts.Node): boolean {
  if (ts.isTypeLiteralNode(node)) {
    return true;
  }

  let foundTypeLiteral = false;
  ts.forEachChild(node, (child) => {
    if (!foundTypeLiteral && containsTypeLiteral(child)) {
      foundTypeLiteral = true;
    }
  });

  return foundTypeLiteral;
}

function normalizeInlineTypeText(text: string): string {
  return text.replace(/\s+/g, " ").trim();
}

function isKeywordSyntaxKind(kind: ts.SyntaxKind): boolean {
  return kind >= ts.SyntaxKind.FirstKeyword && kind <= ts.SyntaxKind.LastKeyword;
}

type InlineScannedToken = {
  kind: ts.SyntaxKind;
  text: string;
  hasPrefixSpace: boolean;
  hasSuffixSpace: boolean;
};

function isInlinePropertyNameLhs(tokens: InlineScannedToken[], index: number): boolean {
  const current = tokens[index];
  if (!current) {
    return false;
  }

  const isNameKind = current.kind === ts.SyntaxKind.Identifier || isKeywordSyntaxKind(current.kind);
  if (!isNameKind) {
    return false;
  }

  const next = tokens[index + 1];
  const nextNext = tokens[index + 2];
  const hasColonAfter = next?.text === ":" || (next?.text === "?" && nextNext?.text === ":");
  if (!hasColonAfter) {
    return false;
  }

  const previousText = tokens[index - 1]?.text;
  if (!previousText) {
    return true;
  }

  if (previousText === "readonly") {
    return true;
  }

  return previousText === "{" || previousText === ";" || previousText === ",";
}

function getInlineTypeTokenKind(
  kind: ts.SyntaxKind,
  value: string,
  isPropertyLhs: boolean,
): TokenKind {
  if (isPropertyLhs) {
    return TokenKind.MemberName;
  }

  if (
    kind === ts.SyntaxKind.StringLiteral ||
    kind === ts.SyntaxKind.NoSubstitutionTemplateLiteral
  ) {
    return TokenKind.StringLiteral;
  }

  if (kind === ts.SyntaxKind.Identifier) {
    return TokenKind.TypeName;
  }

  if (isKeywordSyntaxKind(kind) || isBuiltInType(value)) {
    return TokenKind.Keyword;
  }

  if (PUNCTUATION_CHARS.has(value)) {
    return TokenKind.Punctuation;
  }

  return TokenKind.Text;
}

function addInlineTypeTextTokens(
  text: string,
  tokens: ReviewToken[],
  deprecated?: boolean,
  referenceMap?: ReferenceMap,
): void {
  const scanner = ts.createScanner(
    ts.ScriptTarget.Latest,
    false,
    ts.LanguageVariant.Standard,
    text,
  );

  const scannedTokens: InlineScannedToken[] = [];
  let pendingPrefixSpace = false;
  let previousToken: InlineScannedToken | undefined;

  let token = scanner.scan();
  while (token !== ts.SyntaxKind.EndOfFileToken) {
    if (token === ts.SyntaxKind.WhitespaceTrivia || token === ts.SyntaxKind.NewLineTrivia) {
      pendingPrefixSpace = true;
      if (previousToken) {
        previousToken.hasSuffixSpace = true;
      }
      token = scanner.scan();
      continue;
    }

    if (
      token === ts.SyntaxKind.SingleLineCommentTrivia ||
      token === ts.SyntaxKind.MultiLineCommentTrivia
    ) {
      token = scanner.scan();
      continue;
    }

    const tokenText = scanner.getTokenText();
    if (!tokenText) {
      token = scanner.scan();
      continue;
    }

    const scannedToken: InlineScannedToken = {
      kind: token,
      text: tokenText,
      hasPrefixSpace: pendingPrefixSpace,
      hasSuffixSpace: false,
    };

    scannedTokens.push(scannedToken);
    previousToken = scannedToken;
    pendingPrefixSpace = false;

    token = scanner.scan();
  }

  scannedTokens.forEach((scannedToken, index) => {
    const isPropertyLhs = isInlinePropertyNameLhs(scannedTokens, index);
    const tokenKind = getInlineTypeTokenKind(scannedToken.kind, scannedToken.text, isPropertyLhs);

    // Look up navigation ID for TypeName tokens
    let navigateToId: string | undefined;
    if (tokenKind === TokenKind.TypeName && referenceMap) {
      navigateToId = referenceMap.get(scannedToken.text);
    }

    tokens.push(
      createToken(tokenKind, scannedToken.text, {
        hasPrefixSpace: scannedToken.hasPrefixSpace,
        hasSuffixSpace: scannedToken.hasSuffixSpace,
        deprecated,
        navigateToId,
      }),
    );
  });
}

function shouldInlineTypeNode(node: ts.TypeNode): boolean {
  if (ts.isTypeLiteralNode(node)) {
    return true;
  }

  if (
    ts.isUnionTypeNode(node) ||
    ts.isIntersectionTypeNode(node) ||
    ts.isConditionalTypeNode(node)
  ) {
    return containsTypeLiteral(node);
  }

  return false;
}

function addTypeNodeTokensOrInlineLiteral(
  node: ts.TypeNode,
  tokens: ReviewToken[],
  children: ReviewLine[],
  deprecated: boolean | undefined,
  depth: number,
  referenceMap?: ReferenceMap,
): void {
  if (shouldInlineTypeNode(node)) {
    addInlineTypeTextTokens(
      normalizeInlineTypeText(node.getText()),
      tokens,
      deprecated,
      referenceMap,
    );
    return;
  }

  const nestedChildren = buildTypeNodeTokens(node, tokens, deprecated, depth, referenceMap);
  if (nestedChildren?.length) {
    children.push(...nestedChildren);
  }
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
  const spaces = " ".repeat(depth * 4); // depth * 4 spaces total (4 spaces per level)
  return createToken(TokenKind.Text, spaces, { deprecated });
}

/**
 * Handles union/intersection types that contain type literals by building
 * proper multi-line structure. Returns undefined if no type literals are present,
 * signaling the caller to use the simple inline handler.
 */
function buildCompositeTypeWithLiterals(
  types: ts.NodeArray<ts.TypeNode>,
  separator: "|" | "&",
  tokens: ReviewToken[],
  children: ReviewLine[],
  deprecated: boolean | undefined,
  depth: number,
): ReviewLine[] | undefined {
  const hasTypeLiterals = types.some((t) => ts.isTypeLiteralNode(t));
  if (!hasTypeLiterals) return undefined;

  let inChildrenMode = false;

  types.forEach((typeNode, index) => {
    const isFirst = index === 0;
    const isLast = index === types.length - 1;
    const prevIsLiteral = index > 0 && ts.isTypeLiteralNode(types[index - 1]);

    if (ts.isTypeLiteralNode(typeNode)) {
      if (!inChildrenMode) {
        // First literal encountered: add separator and { to parent tokens
        if (!isFirst) {
          tokens.push(
            createToken(TokenKind.Punctuation, separator, {
              hasPrefixSpace: true,
              hasSuffixSpace: true,
              deprecated,
            }),
          );
        }
        tokens.push(createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }));
        inChildrenMode = true;
      } else if (prevIsLiteral) {
        // Consecutive literals: add "} separator {" as child line
        const sepTokens: ReviewToken[] = [];
        const sepIndent = createIndentation(depth, deprecated);
        if (sepIndent) sepTokens.push(sepIndent);
        sepTokens.push(createToken(TokenKind.Text, " ", { deprecated }));
        sepTokens.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
        sepTokens.push(
          createToken(TokenKind.Punctuation, separator, {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        sepTokens.push(
          createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }),
        );
        children.push({ Tokens: sepTokens });
      } else {
        // After non-literal(s): append "separator {" to the last child line
        const lastChild = children[children.length - 1];
        lastChild.Tokens.push(
          createToken(TokenKind.Punctuation, separator, {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        lastChild.Tokens.push(
          createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }),
        );
      }

      // Add members as children
      for (const member of typeNode.members) {
        const memberTokens: ReviewToken[] = [];
        const indent = createIndentation(depth + 1, deprecated);
        if (indent) memberTokens.push(indent);
        const memberChildren = buildTypeElementTokens(member, memberTokens, deprecated, depth + 1);
        const childLine: ReviewLine = { Tokens: memberTokens };
        if (memberChildren?.length) {
          childLine.Children = memberChildren;
        }
        children.push(childLine);
      }

      // If this is the last type, add closing "};
      if (isLast) {
        const closingTokens: ReviewToken[] = [];
        const closingIndent = createIndentation(depth, deprecated);
        if (closingIndent) closingTokens.push(closingIndent);
        closingTokens.push(createToken(TokenKind.Text, " ", { deprecated }));
        closingTokens.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
        closingTokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
        children.push({ Tokens: closingTokens, IsContextEndLine: true });
      }
    } else {
      // Non-literal type
      if (prevIsLiteral) {
        // After a literal: start a child line with "} separator" then render this type
        const sepTokens: ReviewToken[] = [];
        const sepIndent = createIndentation(depth, deprecated);
        if (sepIndent) sepTokens.push(sepIndent);
        sepTokens.push(createToken(TokenKind.Text, " ", { deprecated }));
        sepTokens.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
        sepTokens.push(
          createToken(TokenKind.Punctuation, separator, {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        const nestedChildren = buildTypeNodeTokens(typeNode, sepTokens, deprecated, depth);
        if (isLast) {
          sepTokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
          children.push({ Tokens: sepTokens, IsContextEndLine: true });
        } else {
          children.push({ Tokens: sepTokens });
        }
        if (nestedChildren?.length) {
          children.push(...nestedChildren);
        }
      } else if (inChildrenMode) {
        // After another non-literal, but we're already in children mode:
        // append "separator type" to the last child line
        const lastChild = children[children.length - 1];
        lastChild.Tokens.push(
          createToken(TokenKind.Punctuation, separator, {
            hasPrefixSpace: true,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
        const nestedChildren = buildTypeNodeTokens(typeNode, lastChild.Tokens, deprecated, depth);
        if (isLast) {
          lastChild.Tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
          lastChild.IsContextEndLine = true;
        }
        if (nestedChildren?.length) {
          children.push(...nestedChildren);
        }
      } else {
        // Before any literal: put on parent line
        if (!isFirst) {
          tokens.push(
            createToken(TokenKind.Punctuation, separator, {
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
      }
    }
  });

  return children.length > 0 ? children : [];
}

/**
 * Parses a TypeScript type AST node and builds ReviewLines with proper nesting
 */
export function buildTypeNodeTokens(
  node: ts.Node,
  tokens: ReviewToken[],
  deprecated?: boolean,
  depth: number = 0,
  referenceMap?: ReferenceMap,
): ReviewLine[] | undefined {
  const children: ReviewLine[] = [];

  if (ts.isTypeLiteralNode(node)) {
    tokens.push(createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }));

    for (const member of node.members) {
      const memberTokens: ReviewToken[] = [];

      // Add indentation for this level
      const indent = createIndentation(depth + 1, deprecated);
      if (indent) memberTokens.push(indent);

      const memberChildren = buildTypeElementTokens(
        member,
        memberTokens,
        deprecated,
        depth + 1,
        referenceMap,
      );

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
    // NOTE: We intentionally add exactly 2 spaces here (in addition to createIndentation above)
    // to keep the closing '}' visually aligned with the type members in APIView. This spacing
    // is part of the expected rendered output; do not change it to use createIndentation or a
    // different width without verifying the impact on existing reviews.
    closingTokens.push(createToken(TokenKind.Text, " ", { deprecated }));
    closingTokens.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
    closingTokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

    children.push({
      Tokens: closingTokens,
      IsContextEndLine: true,
    });

    return children;
  }

  if (ts.isUnionTypeNode(node)) {
    const result = buildCompositeTypeWithLiterals(
      node.types,
      "|",
      tokens,
      children,
      deprecated,
      depth,
    );
    if (result !== undefined) return result;

    // Simple union without type literals
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
      const nestedChildren = buildTypeNodeTokens(typeNode, tokens, deprecated, depth, referenceMap);
      if (nestedChildren?.length) {
        children.push(...nestedChildren);
      }
    });
    return children.length > 0 ? children : undefined;
  }

  if (ts.isIntersectionTypeNode(node)) {
    const result = buildCompositeTypeWithLiterals(
      node.types,
      "&",
      tokens,
      children,
      deprecated,
      depth,
    );
    if (result !== undefined) return result;

    // Simple intersection without type literals
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
      const nestedChildren = buildTypeNodeTokens(typeNode, tokens, deprecated, depth, referenceMap);
      if (nestedChildren?.length) {
        children.push(...nestedChildren);
      }
    });
    return children.length > 0 ? children : undefined;
  }

  if (ts.isParenthesizedTypeNode(node)) {
    // Handle parenthesized types: (TypeA | TypeB)
    tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
    const nestedChildren = buildTypeNodeTokens(node.type, tokens, deprecated, depth, referenceMap);
    tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
    if (nestedChildren?.length) {
      children.push(...nestedChildren);
    }
    return children.length > 0 ? children : undefined;
  }

  if (ts.isArrayTypeNode(node)) {
    const nestedChildren = buildTypeNodeTokens(
      node.elementType,
      tokens,
      deprecated,
      depth,
      referenceMap,
    );

    if (nestedChildren?.length) {
      // If element type has children (e.g., inline object),
      // append [] to the context end line (the one with closing brace)
      const lastChild = nestedChildren[nestedChildren.length - 1];
      if (lastChild.IsContextEndLine && lastChild.Tokens) {
        // Find the semicolon and insert [] before it
        const semiIndex = lastChild.Tokens.findIndex(
          (t) => t.Value === ";" && t.Kind === TokenKind.Punctuation,
        );
        if (semiIndex !== -1) {
          // Insert [] before the semicolon
          lastChild.Tokens.splice(
            semiIndex,
            0,
            createToken(TokenKind.Punctuation, "[", { deprecated }),
            createToken(TokenKind.Punctuation, "]", { deprecated }),
          );
        } else {
          // No semicolon found, just append []
          lastChild.Tokens.push(createToken(TokenKind.Punctuation, "[", { deprecated }));
          lastChild.Tokens.push(createToken(TokenKind.Punctuation, "]", { deprecated }));
        }
      }
      children.push(...nestedChildren);
    } else {
      // Simple element type (e.g., string[]), just add [] to current tokens
      tokens.push(createToken(TokenKind.Punctuation, "[", { deprecated }));
      tokens.push(createToken(TokenKind.Punctuation, "]", { deprecated }));
    }

    return children.length > 0 ? children : undefined;
  }

  if (ts.isTypeReferenceNode(node)) {
    const typeName = node.typeName.getText();
    const navigateToId = referenceMap?.get(typeName);
    tokens.push(createToken(TokenKind.TypeName, typeName, { deprecated, navigateToId }));
    if (node.typeArguments?.length) {
      tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
      node.typeArguments.forEach((arg, index) => {
        if (index > 0) {
          tokens.push(
            createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }),
          );
        }
        addTypeNodeTokensOrInlineLiteral(arg, tokens, children, deprecated, depth, referenceMap);
      });
      tokens.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
    }
    return children.length > 0 ? children : undefined;
  }

  if (ts.isFunctionTypeNode(node)) {
    if (node.typeParameters?.length) {
      tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
      node.typeParameters.forEach((typeParameter, index) => {
        tokens.push(createToken(TokenKind.TypeName, typeParameter.name.getText(), { deprecated }));

        if (typeParameter.constraint) {
          tokens.push(
            createToken(TokenKind.Keyword, "extends", {
              hasPrefixSpace: true,
              hasSuffixSpace: true,
              deprecated,
            }),
          );
          addTypeNodeTokensOrInlineLiteral(
            typeParameter.constraint,
            tokens,
            children,
            deprecated,
            depth,
          );
        }

        if (typeParameter.default) {
          tokens.push(
            createToken(TokenKind.Punctuation, "=", {
              hasPrefixSpace: true,
              hasSuffixSpace: true,
              deprecated,
            }),
          );
          addTypeNodeTokensOrInlineLiteral(
            typeParameter.default,
            tokens,
            children,
            deprecated,
            depth,
          );
        }

        if (index < node.typeParameters!.length - 1) {
          tokens.push(
            createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }),
          );
        }
      });
      tokens.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
    }

    tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));

    node.parameters.forEach((param, index) => {
      if (index > 0) {
        tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
      }

      if (param.dotDotDotToken) {
        tokens.push(createToken(TokenKind.Punctuation, "...", { deprecated }));
      }

      tokens.push(createToken(TokenKind.MemberName, param.name.getText(), { deprecated }));

      if (param.questionToken) {
        tokens.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
      }

      tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      if (param.type) {
        addTypeNodeTokensOrInlineLiteral(
          param.type,
          tokens,
          children,
          deprecated,
          depth,
          referenceMap,
        );
      }
    });

    tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
    tokens.push(
      createToken(TokenKind.Punctuation, "=>", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    addTypeNodeTokensOrInlineLiteral(node.type, tokens, children, deprecated, depth, referenceMap);

    return children.length > 0 ? children : undefined;
  }

  if (ts.isImportTypeNode(node)) {
    // Handle import("@azure/logger").AzureLogger
    tokens.push(createToken(TokenKind.Keyword, "import", { deprecated }));
    tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));

    // The argument is a string literal (module specifier)
    const modulePath = node.argument.getText();
    tokens.push(createToken(TokenKind.StringLiteral, modulePath, { deprecated }));

    tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));

    // Handle qualifier (e.g., .AzureLogger)
    if (node.qualifier) {
      tokens.push(createToken(TokenKind.Punctuation, ".", { deprecated }));
      const qualifierName = node.qualifier.getText();
      const navigateToId = referenceMap?.get(qualifierName);
      tokens.push(createToken(TokenKind.TypeName, qualifierName, { deprecated, navigateToId }));
    }

    // Handle type arguments (e.g., import("...").SomeType<T>)
    if (node.typeArguments?.length) {
      tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
      node.typeArguments.forEach((arg, index) => {
        if (index > 0) {
          tokens.push(
            createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }),
          );
        }
        const nestedChildren = buildTypeNodeTokens(arg, tokens, deprecated, depth, referenceMap);
        if (nestedChildren?.length) {
          children.push(...nestedChildren);
        }
      });
      tokens.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
    }

    return children.length > 0 ? children : undefined;
  }

  if (ts.isConditionalTypeNode(node)) {
    const addConditionalOperandTokens = (operand: ts.TypeNode): void => {
      addTypeNodeTokensOrInlineLiteral(operand, tokens, children, deprecated, depth, referenceMap);
    };

    addConditionalOperandTokens(node.checkType);

    tokens.push(
      createToken(TokenKind.Keyword, "extends", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    addConditionalOperandTokens(node.extendsType);

    tokens.push(
      createToken(TokenKind.Punctuation, "?", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    addConditionalOperandTokens(node.trueType);

    tokens.push(
      createToken(TokenKind.Punctuation, ":", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    addConditionalOperandTokens(node.falseType);

    return children.length > 0 ? children : undefined;
  }

  if (ts.isInferTypeNode(node)) {
    tokens.push(
      createToken(TokenKind.Keyword, "infer", {
        hasSuffixSpace: true,
        deprecated,
      }),
    );
    const typeParameterName = node.typeParameter.name.getText();
    tokens.push(createToken(TokenKind.TypeName, typeParameterName, { deprecated }));

    if (node.typeParameter.constraint) {
      tokens.push(
        createToken(TokenKind.Keyword, "extends", {
          hasPrefixSpace: true,
          hasSuffixSpace: true,
          deprecated,
        }),
      );
      const constraintChildren = buildTypeNodeTokens(
        node.typeParameter.constraint,
        tokens,
        deprecated,
        depth,
        referenceMap,
      );
      if (constraintChildren?.length) {
        children.push(...constraintChildren);
      }
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
  referenceMap?: ReferenceMap,
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
      const children = buildTypeNodeTokens(member.type, tokens, deprecated, depth, referenceMap);
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
      const children = buildTypeNodeTokens(member.type, tokens, deprecated, depth, referenceMap);
      if (!children) {
        tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
      }
      return children;
    }
  } else if (ts.isMethodSignature(member)) {
    const methodChildren: ReviewLine[] = [];

    const name = member.name.getText();
    tokens.push(createToken(TokenKind.MemberName, name, { deprecated }));

    if (member.questionToken) {
      tokens.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
    }

    if (member.typeParameters?.length) {
      tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
      member.typeParameters.forEach((typeParameter, index) => {
        tokens.push(createToken(TokenKind.TypeName, typeParameter.name.getText(), { deprecated }));

        if (typeParameter.constraint) {
          tokens.push(
            createToken(TokenKind.Keyword, "extends", {
              hasPrefixSpace: true,
              hasSuffixSpace: true,
              deprecated,
            }),
          );
          addTypeNodeTokensOrInlineLiteral(
            typeParameter.constraint,
            tokens,
            methodChildren,
            deprecated,
            depth,
            referenceMap,
          );
        }

        if (typeParameter.default) {
          tokens.push(
            createToken(TokenKind.Punctuation, "=", {
              hasPrefixSpace: true,
              hasSuffixSpace: true,
              deprecated,
            }),
          );
          addTypeNodeTokensOrInlineLiteral(
            typeParameter.default,
            tokens,
            methodChildren,
            deprecated,
            depth,
            referenceMap,
          );
        }

        if (index < member.typeParameters.length - 1) {
          tokens.push(
            createToken(TokenKind.Punctuation, ",", {
              hasSuffixSpace: true,
              deprecated,
            }),
          );
        }
      });
      tokens.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
    }

    tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
    member.parameters.forEach((param, index) => {
      if (index > 0) {
        tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
      }

      if (param.dotDotDotToken) {
        tokens.push(createToken(TokenKind.Punctuation, "...", { deprecated }));
      }

      tokens.push(createToken(TokenKind.MemberName, param.name.getText(), { deprecated }));

      if (param.questionToken) {
        tokens.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
      }

      if (param.type) {
        tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
        addTypeNodeTokensOrInlineLiteral(
          param.type,
          tokens,
          methodChildren,
          deprecated,
          depth,
          referenceMap,
        );
      }
    });
    tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));

    if (member.type) {
      tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      addTypeNodeTokensOrInlineLiteral(
        member.type,
        tokens,
        methodChildren,
        deprecated,
        depth,
        referenceMap,
      );
    }

    if (!methodChildren.length) {
      tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
      return undefined;
    }

    return methodChildren;
  }

  tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
  return undefined;
}

/**
 * A map from type name text to canonical reference string.
 * Used to inject NavigateToId into tokens during AST-based parsing.
 */
export type ReferenceMap = Map<string, string>;

/**
 * Builds a reference map from excerpt tokens.
 * This extracts canonical references from Reference tokens so they can be
 * injected into tokens during AST-based parsing.
 */
export function buildReferenceMap(excerptTokens: readonly ExcerptToken[]): ReferenceMap {
  const map: ReferenceMap = new Map();
  for (const token of excerptTokens) {
    if (token.kind === ExcerptTokenKind.Reference && token.canonicalReference) {
      map.set(token.text.trim(), token.canonicalReference.toString());
    }
  }
  return map;
}

/**
 * Parses type text using TypeScript compiler and builds tokens with proper children structure.
 * If a referenceMap is provided, type references will include NavigateToId for navigation.
 */
export function parseTypeText(
  typeText: string,
  tokens: ReviewToken[],
  deprecated?: boolean,
  depth: number = 0,
  referenceMap?: ReferenceMap,
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
    return buildTypeNodeTokens(typeAlias.type, tokens, deprecated, depth, referenceMap);
  }

  // Fallback: just add as text
  const tokenKind = getTokenKind(typeText.trim());
  tokens.push(createToken(tokenKind, typeText.trim(), { deprecated }));
  return undefined;
}
