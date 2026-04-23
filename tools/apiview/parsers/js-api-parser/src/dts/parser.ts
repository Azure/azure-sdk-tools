// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import * as ts from "typescript";
import { ReviewLine, ReviewToken, TokenKind } from "../models.js";
import {
  createToken,
  buildTypeNodeTokens,
  buildTypeElementTokens,
  ReferenceMap,
} from "../tokenGenerators/helpers.js";

// ---------------------------------------------------------------------------
// ID helpers
// ---------------------------------------------------------------------------

/**
 * Builds a canonical-reference-style ID compatible with the api-extractor format,
 * e.g.  @azure/core-http!RequestPolicy:interface
 *       @azure/core-http!HttpClient.sendRequest:method
 */
function makeId(packageName: string, symbolPath: string, kind: string): string {
  return `${packageName}!${symbolPath}:${kind}`;
}

// ---------------------------------------------------------------------------
// JSDoc / TSDoc helpers
// ---------------------------------------------------------------------------

/**
 * Returns "beta" | "alpha" | undefined from @beta / @alpha TSDoc tags on the node.
 */
function getReleaseTagFromNode(node: ts.Node): "beta" | "alpha" | undefined {
  for (const tag of ts.getJSDocTags(node)) {
    const name = tag.tagName.text.toLowerCase();
    if (name === "beta") return "beta";
    if (name === "alpha") return "alpha";
  }
  return undefined;
}

/**
 * Returns true if the node has a @deprecated JSDoc tag.
 */
function isDeprecatedNode(node: ts.Node): boolean {
  return ts.getJSDocDeprecatedTag(node) !== undefined;
}

/**
 * Extracts the textual JSDoc comment from a node, returning each non-empty
 * line as a documentation ReviewToken line.
 */
function buildDocumentationLines(node: ts.Node, relatedToLine: string): ReviewLine[] {
  const lines: ReviewLine[] = [];
  const jsDocNodes = ts.getJSDocCommentsAndTags(node);
  for (const jsDoc of jsDocNodes) {
    if (!ts.isJSDoc(jsDoc)) continue;
    const raw = jsDoc.getText();
    for (const line of raw.split(/\r?\n/)) {
      const trimmed = line.trim();
      if (!trimmed) continue;
      lines.push({
        RelatedToLine: relatedToLine,
        Tokens: [
          createToken(TokenKind.Comment, trimmed, {}),
        ],
      });
    }
  }
  return lines;
}

// ---------------------------------------------------------------------------
// Reference map
// ---------------------------------------------------------------------------

/**
 * Builds a map from exported top-level type name → its canonical ID.
 * Used to inject NavigateToId into type reference tokens.
 */
export function buildLocalReferenceMap(
  statements: readonly ts.Statement[],
  packageName: string,
): ReferenceMap {
  const map: ReferenceMap = new Map();

  function processStatement(stmt: ts.Statement, prefix: string) {
    if (ts.isInterfaceDeclaration(stmt) && isExported(stmt)) {
      map.set(stmt.name.text, makeId(packageName, prefix + stmt.name.text, "interface"));
    } else if (ts.isClassDeclaration(stmt) && stmt.name && isExported(stmt)) {
      map.set(stmt.name.text, makeId(packageName, prefix + stmt.name.text, "class"));
    } else if (ts.isTypeAliasDeclaration(stmt) && isExported(stmt)) {
      map.set(stmt.name.text, makeId(packageName, prefix + stmt.name.text, "typealias"));
    } else if (ts.isEnumDeclaration(stmt) && isExported(stmt)) {
      map.set(stmt.name.text, makeId(packageName, prefix + stmt.name.text, "enum"));
    } else if (ts.isFunctionDeclaration(stmt) && stmt.name && isExported(stmt)) {
      map.set(stmt.name.text, makeId(packageName, prefix + stmt.name.text, "function"));
    } else if (ts.isModuleDeclaration(stmt) && isExported(stmt) && ts.isIdentifier(stmt.name)) {
      // namespace — recurse
      const body = stmt.body;
      if (body && ts.isModuleBlock(body)) {
        for (const child of body.statements) {
          processStatement(child, prefix + stmt.name.text + ".");
        }
      }
    } else if (ts.isVariableStatement(stmt) && isExported(stmt)) {
      for (const decl of stmt.declarationList.declarations) {
        if (ts.isIdentifier(decl.name)) {
          map.set(decl.name.text, makeId(packageName, prefix + decl.name.text, "var"));
        }
      }
    }
  }

  for (const stmt of statements) {
    processStatement(stmt, "");
  }
  return map;
}

// ---------------------------------------------------------------------------
// Export / modifier helpers
// ---------------------------------------------------------------------------

function isExported(node: ts.Node): boolean {
  return !!(
    ts.getCombinedModifierFlags(node as ts.Declaration) & ts.ModifierFlags.Export ||
    (node.parent && ts.isSourceFile(node.parent))  // top-level in module block is implicitly exported
  );
}

function hasModifier(node: ts.Node, flag: ts.ModifierFlags): boolean {
  return !!(ts.getCombinedModifierFlags(node as ts.Declaration) & flag);
}

function getAccessibilityKeyword(node: ts.ClassElement): string | undefined {
  if (hasModifier(node, ts.ModifierFlags.Private)) return "private";
  if (hasModifier(node, ts.ModifierFlags.Protected)) return "protected";
  if (hasModifier(node, ts.ModifierFlags.Public)) return "public";
  return undefined;
}

// ---------------------------------------------------------------------------
// Individual declaration visitors
// ---------------------------------------------------------------------------

interface VisitContext {
  packageName: string;
  /** dot-separated symbol path prefix, e.g. "MyNamespace." */
  prefix: string;
  referenceMap: ReferenceMap;
  /** release tag of the nearest parent with one, for inheritance check */
  parentReleaseTag?: "beta" | "alpha";
  deprecated?: boolean;
}

/**
 * Emits an empty blank line associated with a lineId.
 */
function emptyLine(relatedToLine?: string): ReviewLine {
  return { RelatedToLine: relatedToLine, Tokens: [] };
}

/**
 * Dispatches a single statement to the appropriate visitor.
 * Returns null if the statement should be ignored.
 */
function visitStatement(
  stmt: ts.Statement,
  out: ReviewLine[],
  ctx: VisitContext,
): void {
  if (ts.isInterfaceDeclaration(stmt)) {
    visitInterface(stmt, out, ctx);
  } else if (ts.isClassDeclaration(stmt) && stmt.name) {
    visitClass(stmt, out, ctx);
  } else if (ts.isFunctionDeclaration(stmt) && stmt.name) {
    visitFunction(stmt, out, ctx);
  } else if (ts.isTypeAliasDeclaration(stmt)) {
    visitTypeAlias(stmt, out, ctx);
  } else if (ts.isEnumDeclaration(stmt)) {
    visitEnum(stmt, out, ctx);
  } else if (ts.isVariableStatement(stmt)) {
    visitVariableStatement(stmt, out, ctx);
  } else if (ts.isModuleDeclaration(stmt) && ts.isIdentifier(stmt.name)) {
    visitNamespace(stmt, out, ctx);
  }
  // export { X } re-exports and import statements are intentionally skipped
}

// ---- Type parameters -------------------------------------------------------

function emitTypeParameters(
  params: ts.NodeArray<ts.TypeParameterDeclaration> | undefined,
  tokens: ReviewToken[],
  deprecated: boolean | undefined,
  referenceMap: ReferenceMap,
): void {
  if (!params?.length) return;
  tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
  params.forEach((tp, i) => {
    tokens.push(createToken(TokenKind.TypeName, tp.name.text, { deprecated }));
    if (tp.constraint) {
      tokens.push(createToken(TokenKind.Keyword, "extends", { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));
      buildTypeNodeTokens(tp.constraint, tokens, deprecated, 0, referenceMap);
    }
    if (tp.default) {
      tokens.push(createToken(TokenKind.Punctuation, "=", { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));
      buildTypeNodeTokens(tp.default, tokens, deprecated, 0, referenceMap);
    }
    if (i < params.length - 1) {
      tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
    }
  });
  tokens.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
}

// ---- Parameters ------------------------------------------------------------

function emitParameters(
  params: ts.NodeArray<ts.ParameterDeclaration>,
  tokens: ReviewToken[],
  deprecated: boolean | undefined,
  referenceMap: ReferenceMap,
): void {
  tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
  params.forEach((param, i) => {
    if (i > 0) tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
    if (param.dotDotDotToken) tokens.push(createToken(TokenKind.Punctuation, "...", { deprecated }));
    const name = param.name.getText();
    tokens.push(createToken(TokenKind.MemberName, name, { deprecated }));
    if (param.questionToken) tokens.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
    if (param.type) {
      tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      buildTypeNodeTokens(param.type, tokens, deprecated, 0, referenceMap);
    }
  });
  tokens.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
}

// ---- Release / deprecated preamble -----------------------------------------

function emitPreamble(
  node: ts.Node,
  lineId: string,
  out: ReviewLine[],
  ctx: VisitContext,
): { releaseTag: "beta" | "alpha" | undefined; deprecated: boolean } {
  const docLines = buildDocumentationLines(node, lineId);
  out.push(...docLines);

  const releaseTag = getReleaseTagFromNode(node);
  if (releaseTag && releaseTag !== ctx.parentReleaseTag) {
    out.push({
      Tokens: [createToken(TokenKind.Keyword, `@${releaseTag}`, {})],
      RelatedToLine: lineId,
    });
  }

  const deprecated = ctx.deprecated || isDeprecatedNode(node);
  if (deprecated && !ctx.deprecated) {
    out.push({
      Tokens: [createToken(TokenKind.Keyword, "@deprecated", { deprecated: true })],
      RelatedToLine: lineId,
    });
  }

  return { releaseTag, deprecated };
}

// ---- Interface -------------------------------------------------------------

function visitInterface(
  node: ts.InterfaceDeclaration,
  out: ReviewLine[],
  ctx: VisitContext,
): void {
  const symbolPath = ctx.prefix + node.name.text;
  const lineId = makeId(ctx.packageName, symbolPath, "interface");
  const { deprecated } = emitPreamble(node, lineId, out, ctx);

  const line: ReviewLine = { LineId: lineId, Tokens: [], Children: [] };
  const t = line.Tokens;

  t.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));
  t.push(createToken(TokenKind.Keyword, "interface", { hasSuffixSpace: true, deprecated }));

  const nameToken = createToken(TokenKind.TypeName, node.name.text, { deprecated });
  nameToken.NavigateToId = lineId;
  nameToken.NavigationDisplayName = node.name.text;
  nameToken.RenderClasses = ["interface"];
  t.push(nameToken);

  emitTypeParameters(node.typeParameters, t, deprecated, ctx.referenceMap);

  if (node.heritageClauses?.length) {
    for (const clause of node.heritageClauses) {
      const kw = clause.token === ts.SyntaxKind.ExtendsKeyword ? "extends" : "implements";
      t.push(createToken(TokenKind.Keyword, kw, { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));
      clause.types.forEach((ht, i) => {
        if (i > 0) t.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
        const exprText = ht.expression.getText();
        const navigateToId = ctx.referenceMap.get(exprText);
        t.push(createToken(TokenKind.TypeName, exprText, { deprecated, navigateToId }));
        if (ht.typeArguments?.length) {
          t.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
          ht.typeArguments.forEach((arg, j) => {
            if (j > 0) t.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
            buildTypeNodeTokens(arg, t, deprecated, 0, ctx.referenceMap);
          });
          t.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
        }
      });
    }
  }

  if (node.members.length > 0) {
    t[t.length - 1].HasSuffixSpace = true;
    t.push(createToken(TokenKind.Punctuation, "{", { deprecated }));

    const childCtx: VisitContext = { ...ctx, prefix: symbolPath + ".", deprecated };
    for (const member of node.members) {
      const memberTokens: ReviewToken[] = [];
      const memberChildren = buildTypeElementTokens(
        member,
        memberTokens,
        deprecated,
        0,
        ctx.referenceMap,
      );
      const childLine: ReviewLine = { Tokens: memberTokens };
      if (memberChildren?.length) childLine.Children = memberChildren;
      line.Children!.push(childLine);
    }

    out.push(line);
    out.push({
      Tokens: [createToken(TokenKind.Punctuation, "}", { deprecated })],
      RelatedToLine: lineId,
      IsContextEndLine: true,
    });
  } else {
    t[t.length - 1].HasSuffixSpace = true;
    t.push(createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }));
    t.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
    out.push(line);
  }

  out.push(emptyLine(lineId));
}

// ---- Class -----------------------------------------------------------------

function visitClass(
  node: ts.ClassDeclaration,
  out: ReviewLine[],
  ctx: VisitContext,
): void {
  const name = node.name!.text;
  const symbolPath = ctx.prefix + name;
  const lineId = makeId(ctx.packageName, symbolPath, "class");
  const { releaseTag, deprecated } = emitPreamble(node, lineId, out, ctx);

  const line: ReviewLine = { LineId: lineId, Tokens: [], Children: [] };
  const t = line.Tokens;

  t.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));
  if (hasModifier(node, ts.ModifierFlags.Abstract)) {
    t.push(createToken(TokenKind.Keyword, "abstract", { hasSuffixSpace: true, deprecated }));
  }
  t.push(createToken(TokenKind.Keyword, "class", { hasSuffixSpace: true, deprecated }));

  const nameToken = createToken(TokenKind.TypeName, name, { deprecated });
  nameToken.NavigateToId = lineId;
  nameToken.NavigationDisplayName = name;
  nameToken.RenderClasses = ["class"];
  t.push(nameToken);

  emitTypeParameters(node.typeParameters, t, deprecated, ctx.referenceMap);

  if (node.heritageClauses?.length) {
    for (const clause of node.heritageClauses) {
      const kw = clause.token === ts.SyntaxKind.ExtendsKeyword ? "extends" : "implements";
      t.push(createToken(TokenKind.Keyword, kw, { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));
      clause.types.forEach((ht, i) => {
        if (i > 0) t.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
        const exprText = ht.expression.getText();
        const navigateToId = ctx.referenceMap.get(exprText);
        t.push(createToken(TokenKind.TypeName, exprText, { deprecated, navigateToId }));
        if (ht.typeArguments?.length) {
          t.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
          ht.typeArguments.forEach((arg, j) => {
            if (j > 0) t.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
            buildTypeNodeTokens(arg, t, deprecated, 0, ctx.referenceMap);
          });
          t.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
        }
      });
    }
  }

  if (node.members.length > 0) {
    t[t.length - 1].HasSuffixSpace = true;
    t.push(createToken(TokenKind.Punctuation, "{", { deprecated }));

    const childCtx: VisitContext = {
      ...ctx,
      prefix: symbolPath + ".",
      parentReleaseTag: releaseTag ?? ctx.parentReleaseTag,
      deprecated,
    };

    for (const member of node.members) {
      visitClassMember(member, line.Children!, childCtx);
    }

    out.push(line);
    out.push({
      Tokens: [createToken(TokenKind.Punctuation, "}", { deprecated })],
      RelatedToLine: lineId,
      IsContextEndLine: true,
    });
  } else {
    t[t.length - 1].HasSuffixSpace = true;
    t.push(createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }));
    t.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
    out.push(line);
  }

  out.push(emptyLine(lineId));
}

function visitClassMember(
  member: ts.ClassElement,
  out: ReviewLine[],
  ctx: VisitContext,
): void {
  const deprecated = ctx.deprecated || isDeprecatedNode(member);

  if (ts.isConstructorDeclaration(member)) {
    const lineId = makeId(ctx.packageName, ctx.prefix + "constructor", "constructor");
    const t: ReviewToken[] = [];
    t.push(createToken(TokenKind.Keyword, "constructor", { deprecated }));
    emitParameters(member.parameters, t, deprecated, ctx.referenceMap);
    out.push({ LineId: lineId, Tokens: t });
  } else if (ts.isPropertyDeclaration(member)) {
    const propName = member.name.getText();
    const lineId = makeId(ctx.packageName, ctx.prefix + propName, "property");
    const t: ReviewToken[] = [];
    const access = getAccessibilityKeyword(member);
    if (access) t.push(createToken(TokenKind.Keyword, access, { hasSuffixSpace: true, deprecated }));
    if (hasModifier(member, ts.ModifierFlags.Static))
      t.push(createToken(TokenKind.Keyword, "static", { hasSuffixSpace: true, deprecated }));
    if (hasModifier(member, ts.ModifierFlags.Abstract))
      t.push(createToken(TokenKind.Keyword, "abstract", { hasSuffixSpace: true, deprecated }));
    if (hasModifier(member, ts.ModifierFlags.Readonly))
      t.push(createToken(TokenKind.Keyword, "readonly", { hasSuffixSpace: true, deprecated }));
    t.push(createToken(TokenKind.MemberName, propName, { deprecated }));
    if (member.questionToken) t.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
    if (member.type) {
      t.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      buildTypeNodeTokens(member.type, t, deprecated, 0, ctx.referenceMap);
    }
    t.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    out.push({ LineId: lineId, Tokens: t });
  } else if (ts.isMethodDeclaration(member)) {
    const methodName = member.name.getText();
    const lineId = makeId(ctx.packageName, ctx.prefix + methodName, "method");
    const t: ReviewToken[] = [];
    const access = getAccessibilityKeyword(member);
    if (access) t.push(createToken(TokenKind.Keyword, access, { hasSuffixSpace: true, deprecated }));
    if (hasModifier(member, ts.ModifierFlags.Static))
      t.push(createToken(TokenKind.Keyword, "static", { hasSuffixSpace: true, deprecated }));
    if (hasModifier(member, ts.ModifierFlags.Abstract))
      t.push(createToken(TokenKind.Keyword, "abstract", { hasSuffixSpace: true, deprecated }));
    t.push(createToken(TokenKind.MemberName, methodName, { deprecated }));
    if (member.questionToken) t.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
    emitTypeParameters(member.typeParameters, t, deprecated, ctx.referenceMap);
    emitParameters(member.parameters, t, deprecated, ctx.referenceMap);
    if (member.type) {
      t.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      buildTypeNodeTokens(member.type, t, deprecated, 0, ctx.referenceMap);
    }
    t.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    out.push({ LineId: lineId, Tokens: t });
  } else if (ts.isIndexSignatureDeclaration(member)) {
    const t: ReviewToken[] = [];
    const memberChildren = buildTypeElementTokens(member, t, deprecated, 0, ctx.referenceMap);
    const childLine: ReviewLine = { Tokens: t };
    if (memberChildren?.length) childLine.Children = memberChildren;
    out.push(childLine);
  }
}

// ---- Function --------------------------------------------------------------

function visitFunction(
  node: ts.FunctionDeclaration,
  out: ReviewLine[],
  ctx: VisitContext,
): void {
  const name = node.name!.text;
  const symbolPath = ctx.prefix + name;
  const lineId = makeId(ctx.packageName, symbolPath, "function");
  const { deprecated } = emitPreamble(node, lineId, out, ctx);

  const t: ReviewToken[] = [];
  t.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));
  t.push(createToken(TokenKind.Keyword, "function", { hasSuffixSpace: true, deprecated }));

  const nameToken = createToken(TokenKind.MemberName, name, { deprecated });
  nameToken.NavigateToId = lineId;
  nameToken.NavigationDisplayName = name;
  t.push(nameToken);

  emitTypeParameters(node.typeParameters, t, deprecated, ctx.referenceMap);
  emitParameters(node.parameters, t, deprecated, ctx.referenceMap);

  if (node.type) {
    t.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
    buildTypeNodeTokens(node.type, t, deprecated, 0, ctx.referenceMap);
  }
  t.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  out.push({ LineId: lineId, Tokens: t });
  out.push(emptyLine(lineId));
}

// ---- Type alias ------------------------------------------------------------

function visitTypeAlias(
  node: ts.TypeAliasDeclaration,
  out: ReviewLine[],
  ctx: VisitContext,
): void {
  const symbolPath = ctx.prefix + node.name.text;
  const lineId = makeId(ctx.packageName, symbolPath, "typealias");
  const { deprecated } = emitPreamble(node, lineId, out, ctx);

  const t: ReviewToken[] = [];
  t.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));
  t.push(createToken(TokenKind.Keyword, "type", { hasSuffixSpace: true, deprecated }));

  const nameToken = createToken(TokenKind.TypeName, node.name.text, { deprecated });
  nameToken.NavigateToId = lineId;
  nameToken.NavigationDisplayName = node.name.text;
  t.push(nameToken);

  emitTypeParameters(node.typeParameters, t, deprecated, ctx.referenceMap);
  t.push(createToken(TokenKind.Punctuation, "=", { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));

  const line: ReviewLine = { LineId: lineId, Tokens: t, Children: [] };
  const children = buildTypeNodeTokens(node.type, t, deprecated, 0, ctx.referenceMap);
  if (children?.length) line.Children = children;
  t.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  out.push(line);
  out.push(emptyLine(lineId));
}

// ---- Enum ------------------------------------------------------------------

function visitEnum(
  node: ts.EnumDeclaration,
  out: ReviewLine[],
  ctx: VisitContext,
): void {
  const symbolPath = ctx.prefix + node.name.text;
  const lineId = makeId(ctx.packageName, symbolPath, "enum");
  const { deprecated } = emitPreamble(node, lineId, out, ctx);

  const line: ReviewLine = { LineId: lineId, Tokens: [], Children: [] };
  const t = line.Tokens;

  t.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));
  if (hasModifier(node, ts.ModifierFlags.Const)) {
    t.push(createToken(TokenKind.Keyword, "const", { hasSuffixSpace: true, deprecated }));
  }
  t.push(createToken(TokenKind.Keyword, "enum", { hasSuffixSpace: true, deprecated }));

  const nameToken = createToken(TokenKind.TypeName, node.name.text, { deprecated });
  nameToken.NavigateToId = lineId;
  nameToken.NavigationDisplayName = node.name.text;
  nameToken.RenderClasses = ["enum"];
  t.push(nameToken);

  if (node.members.length > 0) {
    t[t.length - 1].HasSuffixSpace = true;
    t.push(createToken(TokenKind.Punctuation, "{", { deprecated }));

    for (const member of node.members) {
      const memberName = member.name.getText();
      const memberId = makeId(ctx.packageName, symbolPath + "." + memberName, "member");
      const mt: ReviewToken[] = [];
      mt.push(createToken(TokenKind.MemberName, memberName, { deprecated }));
      if (member.initializer) {
        mt.push(createToken(TokenKind.Punctuation, "=", { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));
        mt.push(createToken(TokenKind.StringLiteral, member.initializer.getText(), { deprecated }));
      }
      mt.push(createToken(TokenKind.Punctuation, ",", { deprecated }));
      line.Children!.push({ LineId: memberId, Tokens: mt });
    }

    out.push(line);
    out.push({
      Tokens: [createToken(TokenKind.Punctuation, "}", { deprecated })],
      RelatedToLine: lineId,
      IsContextEndLine: true,
    });
  } else {
    t[t.length - 1].HasSuffixSpace = true;
    t.push(createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }));
    t.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
    out.push(line);
  }

  out.push(emptyLine(lineId));
}

// ---- Variable --------------------------------------------------------------

function visitVariableStatement(
  node: ts.VariableStatement,
  out: ReviewLine[],
  ctx: VisitContext,
): void {
  const deprecated = ctx.deprecated || isDeprecatedNode(node);
  const isConst = !!(node.declarationList.flags & ts.NodeFlags.Const);
  const isLet = !!(node.declarationList.flags & ts.NodeFlags.Let);
  const keyword = isConst ? "const" : isLet ? "let" : "var";

  for (const decl of node.declarationList.declarations) {
    if (!ts.isIdentifier(decl.name)) continue;
    const varName = decl.name.text;
    const symbolPath = ctx.prefix + varName;
    const lineId = makeId(ctx.packageName, symbolPath, "var");

    // Emit preamble on the declaration (JSDoc is on the VariableStatement)
    const docLines = buildDocumentationLines(node, lineId);
    out.push(...docLines);

    const releaseTag = getReleaseTagFromNode(node);
    if (releaseTag && releaseTag !== ctx.parentReleaseTag) {
      out.push({
        Tokens: [createToken(TokenKind.Keyword, `@${releaseTag}`, {})],
        RelatedToLine: lineId,
      });
    }

    if (deprecated && !ctx.deprecated) {
      out.push({
        Tokens: [createToken(TokenKind.Keyword, "@deprecated", {})],
        RelatedToLine: lineId,
      });
    }

    const t: ReviewToken[] = [];
    t.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));
    t.push(createToken(TokenKind.Keyword, keyword, { hasSuffixSpace: true, deprecated }));

    const nameToken = createToken(TokenKind.MemberName, varName, { deprecated });
    nameToken.NavigateToId = lineId;
    nameToken.NavigationDisplayName = varName;
    t.push(nameToken);

    if (decl.type) {
      t.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      buildTypeNodeTokens(decl.type, t, deprecated, 0, ctx.referenceMap);
    }
    t.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    out.push({ LineId: lineId, Tokens: t });
    out.push(emptyLine(lineId));
  }
}

// ---- Namespace -------------------------------------------------------------

function visitNamespace(
  node: ts.ModuleDeclaration,
  out: ReviewLine[],
  ctx: VisitContext,
): void {
  const name = (node.name as ts.Identifier).text;
  const symbolPath = ctx.prefix + name;
  const lineId = makeId(ctx.packageName, symbolPath, "namespace");
  const { releaseTag, deprecated } = emitPreamble(node, lineId, out, ctx);

  const body = node.body;
  if (!body || !ts.isModuleBlock(body)) return;

  const line: ReviewLine = { LineId: lineId, Tokens: [], Children: [] };
  const t = line.Tokens;

  t.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));
  t.push(createToken(TokenKind.Keyword, "namespace", { hasSuffixSpace: true, deprecated }));

  const nameToken = createToken(TokenKind.TypeName, name, { deprecated });
  nameToken.NavigateToId = lineId;
  nameToken.NavigationDisplayName = name;
  t.push(nameToken);
  t[t.length - 1].HasSuffixSpace = true;
  t.push(createToken(TokenKind.Punctuation, "{", { deprecated }));

  const childCtx: VisitContext = {
    ...ctx,
    prefix: symbolPath + ".",
    parentReleaseTag: releaseTag ?? ctx.parentReleaseTag,
    deprecated,
  };

  const childLines: ReviewLine[] = [];
  for (const stmt of body.statements) {
    visitStatement(stmt, childLines, childCtx);
  }
  line.Children = childLines;

  out.push(line);
  out.push({
    Tokens: [createToken(TokenKind.Punctuation, "}", { deprecated })],
    RelatedToLine: lineId,
    IsContextEndLine: true,
  });
  out.push(emptyLine(lineId));
}

// ---------------------------------------------------------------------------
// Entry-point subpath detection
// ---------------------------------------------------------------------------

interface EntryPoint {
  subpath: string;
  statements: readonly ts.Statement[];
}

/**
 * Returns the list of entry points to process.
 * If the file contains any `declare module "..."` blocks they become separate subpaths.
 * Otherwise the entire file is treated as the default "." subpath.
 */
function getEntryPoints(sourceFile: ts.SourceFile): EntryPoint[] {
  const moduleBlocks: EntryPoint[] = [];

  for (const stmt of sourceFile.statements) {
    if (
      ts.isModuleDeclaration(stmt) &&
      ts.isStringLiteral(stmt.name) &&
      stmt.body &&
      ts.isModuleBlock(stmt.body)
    ) {
      // Normalise: strip leading ./ so "." and "./" both map to "."
      const rawName = stmt.name.text;
      const subpath = rawName === "" ? "." : rawName;
      moduleBlocks.push({ subpath, statements: stmt.body.statements });
    }
  }

  if (moduleBlocks.length > 0) return moduleBlocks;

  // No declare module blocks — treat whole file as "."
  return [{ subpath: ".", statements: sourceFile.statements }];
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

export interface DtsParseOptions {
  /** File system path to the .d.ts file */
  filePath: string;
  /** Package name used for ID generation, e.g. "@azure/core-http" */
  packageName: string;
}

/**
 * Parses a `.d.ts` file and returns ReviewLine arrays keyed by subpath.
 * Each subpath corresponds to one entry in the final CodeFile review.
 */
export function parseDtsFile(options: DtsParseOptions): Map<string, ReviewLine[]> {
  const { filePath, packageName } = options;

  const program = ts.createProgram([filePath], {
    target: ts.ScriptTarget.Latest,
    moduleResolution: ts.ModuleResolutionKind.Bundler,
    noEmit: true,
    declaration: true,
  });

  // Trigger binding so that parent pointers are set on all AST nodes.
  // Without this, calls to node.getText() will throw because they walk the
  // parent chain to find the SourceFile's text buffer.
  program.getTypeChecker();

  const sourceFile = program.getSourceFile(filePath);
  if (!sourceFile) {
    throw new Error(`Could not load source file: ${filePath}`);
  }

  const entryPoints = getEntryPoints(sourceFile);
  const result = new Map<string, ReviewLine[]>();

  for (const ep of entryPoints) {
    // Build a reference map scoped to this entry point's statements
    const referenceMap = buildLocalReferenceMap(ep.statements as ts.Statement[], packageName);

    const ctx: VisitContext = {
      packageName,
      prefix: "",
      referenceMap,
      parentReleaseTag: undefined,
      deprecated: false,
    };

    const lines: ReviewLine[] = [];
    for (const stmt of ep.statements) {
      visitStatement(stmt as ts.Statement, lines, ctx);
    }

    result.set(ep.subpath, lines);
  }

  return result;
}
