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

function addTypeNodeTokensOrInlineLiteral(
  node: ts.TypeNode,
  tokens: ReviewToken[],
  children: ReviewLine[],
  deprecated: boolean | undefined,
  depth: number,
  referenceMap?: ReferenceMap,
): ReviewToken[] {
  const nestedChildren = buildTypeNodeTokens(node, tokens, deprecated, depth, referenceMap);
  if (nestedChildren?.length) {
    children.push(...nestedChildren);
    return nestedChildren[nestedChildren.length - 1].Tokens;
  }
  return tokens;
}

/**
 * Collects tokens and manages multi-line splitting into ReviewLine children.
 * When a newline is encountered during excerpt processing, subsequent tokens
 * are routed to child ReviewLines instead of the main token array.
 */
export class TokenCollector {
  readonly tokens: ReviewToken[] = [];
  readonly children: ReviewLine[] = [];

  /** Get the current target array (last child's tokens, or main tokens) */
  get currentTarget(): ReviewToken[] {
    return this.children.length > 0
      ? this.children[this.children.length - 1].Tokens
      : this.tokens;
  }

  /** Push tokens to the current target (main line or last child line) */
  push(...items: ReviewToken[]): void {
    this.currentTarget.push(...items);
  }

  /** Start a new child line for multi-line content */
  newLine(): void {
    this.children.push({ Tokens: [] });
  }

  /** Build the final result object */
  toResult(additionalChildren?: ReviewLine[]): { tokens: ReviewToken[]; children?: ReviewLine[] } {
    const allChildren = [...this.children, ...(additionalChildren || [])];
    return {
      tokens: this.tokens,
      ...(allChildren.length > 0 && { children: allChildren }),
    };
  }
}

/** Process excerpt tokens and add them to the tokens array or TokenCollector */
export function processExcerptTokens(
  excerptTokens: readonly ExcerptToken[],
  target: ReviewToken[] | TokenCollector,
  deprecated?: boolean,
): void {
  for (const excerpt of excerptTokens) {
    const text = excerpt.text;
    if (!text || !text.trim()) continue;

    const lines = text.split(/(\r?\n)/);

    for (const segment of lines) {
      if (segment === "\n" || segment === "\r\n") {
        if (target instanceof TokenCollector) {
          target.newLine();
        }
        continue;
      }

      const trimmedText = segment.trim();
      if (!trimmedText) continue;

      const hasPrefixSpace =
        segment.startsWith(" ") || segment.startsWith("\t") || needsLeadingSpace(trimmedText);
      const hasSuffixSpace =
        segment.endsWith(" ") || segment.endsWith("\t") || needsTrailingSpace(trimmedText);

      const token =
        excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference
          ? createToken(TokenKind.TypeName, trimmedText, {
              navigateToId: excerpt.canonicalReference.toString(),
              hasPrefixSpace,
              hasSuffixSpace,
              deprecated,
            })
          : createToken(getTokenKind(trimmedText), trimmedText, {
              hasPrefixSpace,
              hasSuffixSpace,
              deprecated,
            });

      target.push(token);
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

      // If this is the last type, add closing "}"
      if (isLast) {
        const closingTokens: ReviewToken[] = [];
        const closingIndent = createIndentation(depth, deprecated);
        if (closingIndent) closingTokens.push(closingIndent);
        closingTokens.push(createToken(TokenKind.Text, " ", { deprecated }));
        closingTokens.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
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
      let target = tokens;
      node.typeArguments.forEach((arg, index) => {
        if (index > 0) {
          target.push(
            createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }),
          );
        }
        target = addTypeNodeTokensOrInlineLiteral(arg, target, children, deprecated, depth, referenceMap);
      });
      target.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
    }
    return children.length > 0 ? children : undefined;
  }

  if (ts.isFunctionTypeNode(node)) {
    let target: ReviewToken[] = tokens;
    if (node.typeParameters?.length) {
      target.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
      node.typeParameters.forEach((typeParameter, index) => {
        target.push(createToken(TokenKind.TypeName, typeParameter.name.getText(), { deprecated }));

        if (typeParameter.constraint) {
          target.push(
            createToken(TokenKind.Keyword, "extends", {
              hasPrefixSpace: true,
              hasSuffixSpace: true,
              deprecated,
            }),
          );
          target = addTypeNodeTokensOrInlineLiteral(
            typeParameter.constraint,
            target,
            children,
            deprecated,
            depth,
          );
        }

        if (typeParameter.default) {
          target.push(
            createToken(TokenKind.Punctuation, "=", {
              hasPrefixSpace: true,
              hasSuffixSpace: true,
              deprecated,
            }),
          );
          target = addTypeNodeTokensOrInlineLiteral(
            typeParameter.default,
            target,
            children,
            deprecated,
            depth,
          );
        }

        if (index < node.typeParameters!.length - 1) {
          target.push(
            createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }),
          );
        }
      });
      target.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
    }

    target.push(createToken(TokenKind.Punctuation, "(", { deprecated }));

    node.parameters.forEach((param, index) => {
      if (index > 0) {
        target.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
      }

      if (param.dotDotDotToken) {
        target.push(createToken(TokenKind.Punctuation, "...", { deprecated }));
      }

      target.push(createToken(TokenKind.MemberName, param.name.getText(), { deprecated }));

      if (param.questionToken) {
        target.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
      }

      target.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      if (param.type) {
        target = addTypeNodeTokensOrInlineLiteral(
          param.type,
          target,
          children,
          deprecated,
          depth,
          referenceMap,
        );
      }
    });

    target.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
    target.push(
      createToken(TokenKind.Punctuation, "=>", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    target = addTypeNodeTokensOrInlineLiteral(node.type, target, children, deprecated, depth, referenceMap);

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
    let target: ReviewToken[] = tokens;

    target = addTypeNodeTokensOrInlineLiteral(node.checkType, target, children, deprecated, depth, referenceMap);

    target.push(
      createToken(TokenKind.Keyword, "extends", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    target = addTypeNodeTokensOrInlineLiteral(node.extendsType, target, children, deprecated, depth, referenceMap);

    target.push(
      createToken(TokenKind.Punctuation, "?", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    target = addTypeNodeTokensOrInlineLiteral(node.trueType, target, children, deprecated, depth, referenceMap);

    target.push(
      createToken(TokenKind.Punctuation, ":", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );

    target = addTypeNodeTokensOrInlineLiteral(node.falseType, target, children, deprecated, depth, referenceMap);

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

  if (ts.isTemplateLiteralTypeNode(node)) {
    // Handle template literal types like `${string}-${number}`
    tokens.push(createToken(TokenKind.Punctuation, "`", { deprecated }));
    // Process template head text (between ` and first ${)
    const headText = node.head.text;
    if (headText) {
      tokens.push(createToken(TokenKind.Text, headText, { deprecated }));
    }
    for (const span of node.templateSpans) {
      tokens.push(createToken(TokenKind.Punctuation, "${", { deprecated }));
      const nestedChildren = buildTypeNodeTokens(span.type, tokens, deprecated, depth, referenceMap);
      if (nestedChildren?.length) {
        children.push(...nestedChildren);
      }
      tokens.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
      // Text after the type expression (between } and next ${ or `)
      const literalText = span.literal.text;
      if (literalText) {
        tokens.push(createToken(TokenKind.Text, literalText, { deprecated }));
      }
    }
    tokens.push(createToken(TokenKind.Punctuation, "`", { deprecated }));
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
      } else {
        children[children.length - 1].Tokens.push(
          createToken(TokenKind.Punctuation, ";", { deprecated }),
        );
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
      } else {
        children[children.length - 1].Tokens.push(
          createToken(TokenKind.Punctuation, ";", { deprecated }),
        );
      }
      return children;
    }
  } else if (ts.isMethodSignature(member)) {
    const methodChildren: ReviewLine[] = [];

    const name = member.name.getText();
    let target: ReviewToken[] = tokens;
    target.push(createToken(TokenKind.MemberName, name, { deprecated }));

    if (member.questionToken) {
      target.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
    }

    if (member.typeParameters?.length) {
      target.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
      member.typeParameters.forEach((typeParameter, index) => {
        target.push(createToken(TokenKind.TypeName, typeParameter.name.getText(), { deprecated }));

        if (typeParameter.constraint) {
          target.push(
            createToken(TokenKind.Keyword, "extends", {
              hasPrefixSpace: true,
              hasSuffixSpace: true,
              deprecated,
            }),
          );
          target = addTypeNodeTokensOrInlineLiteral(
            typeParameter.constraint,
            target,
            methodChildren,
            deprecated,
            depth,
            referenceMap,
          );
        }

        if (typeParameter.default) {
          target.push(
            createToken(TokenKind.Punctuation, "=", {
              hasPrefixSpace: true,
              hasSuffixSpace: true,
              deprecated,
            }),
          );
          target = addTypeNodeTokensOrInlineLiteral(
            typeParameter.default,
            target,
            methodChildren,
            deprecated,
            depth,
            referenceMap,
          );
        }

        if (index < member.typeParameters.length - 1) {
          target.push(
            createToken(TokenKind.Punctuation, ",", {
              hasSuffixSpace: true,
              deprecated,
            }),
          );
        }
      });
      target.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
    }

    target.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
    member.parameters.forEach((param, index) => {
      if (index > 0) {
        target.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
      }

      if (param.dotDotDotToken) {
        target.push(createToken(TokenKind.Punctuation, "...", { deprecated }));
      }

      target.push(createToken(TokenKind.MemberName, param.name.getText(), { deprecated }));

      if (param.questionToken) {
        target.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
      }

      if (param.type) {
        target.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
        target = addTypeNodeTokensOrInlineLiteral(
          param.type,
          target,
          methodChildren,
          deprecated,
          depth,
          referenceMap,
        );
      }
    });
    target.push(createToken(TokenKind.Punctuation, ")", { deprecated }));

    if (member.type) {
      target.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      target = addTypeNodeTokensOrInlineLiteral(
        member.type,
        target,
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

    methodChildren[methodChildren.length - 1].Tokens.push(
      createToken(TokenKind.Punctuation, ";", { deprecated }),
    );
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
