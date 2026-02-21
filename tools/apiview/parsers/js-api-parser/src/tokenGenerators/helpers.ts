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

function getInlineTypeTokenKind(kind: ts.SyntaxKind, value: string, isPropertyLhs: boolean): TokenKind {
  if (isPropertyLhs) {
    return TokenKind.MemberName;
  }

  if (kind === ts.SyntaxKind.StringLiteral || kind === ts.SyntaxKind.NoSubstitutionTemplateLiteral) {
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

function addInlineTypeTextTokens(text: string, tokens: ReviewToken[], deprecated?: boolean): void {
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

    if (token === ts.SyntaxKind.SingleLineCommentTrivia || token === ts.SyntaxKind.MultiLineCommentTrivia) {
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
    const tokenKind = getInlineTypeTokenKind(
      scannedToken.kind,
      scannedToken.text,
      isInlinePropertyNameLhs(scannedTokens, index),
    );

    tokens.push(
      createToken(tokenKind, scannedToken.text, {
        hasPrefixSpace: scannedToken.hasPrefixSpace,
        hasSuffixSpace: scannedToken.hasSuffixSpace,
        deprecated,
      }),
    );
  });
}

function shouldInlineTypeNode(node: ts.TypeNode): boolean {
  if (ts.isTypeLiteralNode(node)) {
    return true;
  }

  if (ts.isUnionTypeNode(node) || ts.isIntersectionTypeNode(node) || ts.isConditionalTypeNode(node)) {
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
): void {
  if (shouldInlineTypeNode(node)) {
    addInlineTypeTextTokens(normalizeInlineTypeText(node.getText()), tokens, deprecated);
    return;
  }

  const nestedChildren = buildTypeNodeTokens(node, tokens, deprecated, depth);
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
  const spaces = "  ".repeat(depth * 4); // depth * 8 spaces total (8 spaces per level)
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
    // NOTE: We intentionally add exactly 4 spaces here (in addition to createIndentation above)
    // to keep the closing '}' visually aligned with the type members in APIView. This spacing
    // is part of the expected rendered output; do not change it to use createIndentation or a
    // different width without verifying the impact on existing reviews.
    closingTokens.push(createToken(TokenKind.Text, "    ", { deprecated }));
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
    tokens.push(createToken(TokenKind.TypeName, node.typeName.getText(), { deprecated }));
    if (node.typeArguments?.length) {
      tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
      node.typeArguments.forEach((arg, index) => {
        if (index > 0) {
          tokens.push(
            createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }),
          );
        }
        addTypeNodeTokensOrInlineLiteral(arg, tokens, children, deprecated, depth);
      });
      tokens.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
    }
    return children.length > 0 ? children : undefined;
  }



  if (ts.isFunctionTypeNode(node)) {
    tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));

    node.parameters.forEach((param, index) => {
      if (index > 0) {
        tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
      }

      tokens.push(createToken(TokenKind.MemberName, param.name.getText(), { deprecated }));

      if (param.questionToken) {
        tokens.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
      }

      tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      if (param.type) {
        addTypeNodeTokensOrInlineLiteral(param.type, tokens, children, deprecated, depth);
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

    addTypeNodeTokensOrInlineLiteral(node.type, tokens, children, deprecated, depth);

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
      tokens.push(createToken(TokenKind.TypeName, node.qualifier.getText(), { deprecated }));
    }

    // Handle type arguments (e.g., import("...").SomeType<T>)
    if (node.typeArguments?.length) {
      tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
      node.typeArguments.forEach((arg, index) => {
        if (index > 0) {
          tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
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


  if (ts.isConditionalTypeNode(node)) {
    const addConditionalOperandTokens = (operand: ts.TypeNode): void => {
      addTypeNodeTokensOrInlineLiteral(operand, tokens, children, deprecated, depth);
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
        addTypeNodeTokensOrInlineLiteral(param.type, tokens, methodChildren, deprecated, depth);
      }
    });
    tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));

    if (member.type) {
      tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      addTypeNodeTokensOrInlineLiteral(member.type, tokens, methodChildren, deprecated, depth);
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

