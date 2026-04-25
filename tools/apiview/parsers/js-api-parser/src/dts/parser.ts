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
): ReviewLine[] {
  const children: ReviewLine[] = [];
  if (!params?.length) return children;
  tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
  let target = tokens;
  params.forEach((tp, i) => {
    target.push(createToken(TokenKind.TypeName, tp.name.text, { deprecated }));
    if (tp.constraint) {
      target.push(createToken(TokenKind.Keyword, "extends", { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));
      const c = buildTypeNodeTokens(tp.constraint, target, deprecated, 0, referenceMap);
      if (c?.length) { children.push(...c); target = c[c.length - 1].Tokens; }
    }
    if (tp.default) {
      target.push(createToken(TokenKind.Punctuation, "=", { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));
      const c = buildTypeNodeTokens(tp.default, target, deprecated, 0, referenceMap);
      if (c?.length) { children.push(...c); target = c[c.length - 1].Tokens; }
    }
    if (i < params.length - 1) {
      target.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
    }
  });
  target.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
  return children;
}

// ---- Heritage clauses -------------------------------------------------------

function emitHeritageClauses(
  clauses: ts.NodeArray<ts.HeritageClause> | undefined,
  tokens: ReviewToken[],
  deprecated: boolean | undefined,
  referenceMap: ReferenceMap,
): ReviewLine[] {
  const children: ReviewLine[] = [];
  if (!clauses?.length) return children;
  for (const clause of clauses) {
    const kw = clause.token === ts.SyntaxKind.ExtendsKeyword ? "extends" : "implements";
    tokens.push(createToken(TokenKind.Keyword, kw, { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));
    clause.types.forEach((ht, i) => {
      if (i > 0) tokens.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
      const exprText = ht.expression.getText();
      const navigateToId = referenceMap.get(exprText);
      tokens.push(createToken(TokenKind.TypeName, exprText, { deprecated, navigateToId }));
      if (ht.typeArguments?.length) {
        tokens.push(createToken(TokenKind.Punctuation, "<", { deprecated }));
        let argTarget = tokens;
        ht.typeArguments.forEach((arg, j) => {
          if (j > 0) argTarget.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
          const c = buildTypeNodeTokens(arg, argTarget, deprecated, 0, referenceMap);
          if (c?.length) { children.push(...c); argTarget = c[c.length - 1].Tokens; }
        });
        argTarget.push(createToken(TokenKind.Punctuation, ">", { deprecated }));
      }
    });
  }
  return children;
}

// ---- Parameters ------------------------------------------------------------

function emitParameters(
  params: ts.NodeArray<ts.ParameterDeclaration>,
  tokens: ReviewToken[],
  deprecated: boolean | undefined,
  referenceMap: ReferenceMap,
): ReviewLine[] {
  const children: ReviewLine[] = [];
  tokens.push(createToken(TokenKind.Punctuation, "(", { deprecated }));
  let target = tokens;
  params.forEach((param, i) => {
    if (i > 0) target.push(createToken(TokenKind.Punctuation, ",", { hasSuffixSpace: true, deprecated }));
    if (param.dotDotDotToken) target.push(createToken(TokenKind.Punctuation, "...", { deprecated }));
    const name = param.name.getText();
    target.push(createToken(TokenKind.MemberName, name, { deprecated }));
    if (param.questionToken) target.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
    if (param.type) {
      target.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      const c = buildTypeNodeTokens(param.type, target, deprecated, 0, referenceMap);
      if (c?.length) { children.push(...c); target = c[c.length - 1].Tokens; }
    }
  });
  target.push(createToken(TokenKind.Punctuation, ")", { deprecated }));
  return children;
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

// ---- Name token helper -----------------------------------------------------

interface NameTokenOptions {
  name: string;
  lineId: string;
  kind: TokenKind;
  deprecated?: boolean;
  renderClass?: string;
}

/**
 * Creates a navigable name token with consistent NavigateToId and NavigationDisplayName.
 */
function createNameToken(opts: NameTokenOptions): ReviewToken {
  const token = createToken(opts.kind, opts.name, { deprecated: opts.deprecated });
  token.NavigateToId = opts.lineId;
  token.NavigationDisplayName = opts.name;
  if (opts.renderClass) token.RenderClasses = [opts.renderClass];
  return token;
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
  t.push(createNameToken({ name: node.name.text, lineId, kind: TokenKind.TypeName, deprecated, renderClass: "interface" }));

  const tpChildren = emitTypeParameters(node.typeParameters, t, deprecated, ctx.referenceMap);
  if (tpChildren.length) line.Children!.push(...tpChildren);

  // If type-parameter emission created children (e.g. a multiline constraint), subsequent
  // tokens — heritage clauses, the opening "{" — must be appended to the last child line,
  // not back onto the header.  Track `headTarget` for this purpose.
  let headTarget: ReviewToken[] = tpChildren.length ? tpChildren[tpChildren.length - 1].Tokens : t;

  const hcChildren = emitHeritageClauses(node.heritageClauses, headTarget, deprecated, ctx.referenceMap);
  if (hcChildren.length) {
    line.Children!.push(...hcChildren);
    headTarget = hcChildren[hcChildren.length - 1].Tokens;
  }

  if (node.members.length > 0) {
    headTarget[headTarget.length - 1].HasSuffixSpace = true;
    headTarget.push(createToken(TokenKind.Punctuation, "{", { deprecated }));

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
    headTarget[headTarget.length - 1].HasSuffixSpace = true;
    headTarget.push(createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }));
    headTarget.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
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
  t.push(createNameToken({ name, lineId, kind: TokenKind.TypeName, deprecated, renderClass: "class" }));

  const tpChildren = emitTypeParameters(node.typeParameters, t, deprecated, ctx.referenceMap);
  if (tpChildren.length) line.Children!.push(...tpChildren);

  // If type-parameter emission created children (e.g. a multiline constraint `extends { … }`),
  // the heritage clause and the opening "{" must be appended to the last child line, not back
  // onto the header.  Track `headTarget` for this purpose.
  let headTarget: ReviewToken[] = tpChildren.length ? tpChildren[tpChildren.length - 1].Tokens : t;

  const hcChildren = emitHeritageClauses(node.heritageClauses, headTarget, deprecated, ctx.referenceMap);
  if (hcChildren.length) {
    line.Children!.push(...hcChildren);
    headTarget = hcChildren[hcChildren.length - 1].Tokens;
  }

  if (node.members.length > 0) {
    headTarget[headTarget.length - 1].HasSuffixSpace = true;
    headTarget.push(createToken(TokenKind.Punctuation, "{", { deprecated }));

    const childCtx: VisitContext = {
      ...ctx,
      prefix: symbolPath + ".",
      parentReleaseTag: releaseTag ?? ctx.parentReleaseTag,
      deprecated,
    };

    // Track method names seen so far to give overloads a disambiguated LineId.
    const seenMethodNames = new Set<string>();
    for (const member of node.members) {
      visitClassMember(member, line.Children!, childCtx, seenMethodNames);
    }

    out.push(line);
    out.push({
      Tokens: [createToken(TokenKind.Punctuation, "}", { deprecated })],
      RelatedToLine: lineId,
      IsContextEndLine: true,
    });
  } else {
    headTarget[headTarget.length - 1].HasSuffixSpace = true;
    headTarget.push(createToken(TokenKind.Punctuation, "{", { hasSuffixSpace: true, deprecated }));
    headTarget.push(createToken(TokenKind.Punctuation, "}", { deprecated }));
    out.push(line);
  }

  out.push(emptyLine(lineId));
}

function visitClassMember(
  member: ts.ClassElement,
  out: ReviewLine[],
  ctx: VisitContext,
  seenMethodNames?: Set<string>,
): void {
  // Private members are not part of the public API surface.
  // ECMAScript private fields (#name) are unconditionally private.
  if (hasModifier(member, ts.ModifierFlags.Private)) return;
  if (
    (ts.isPropertyDeclaration(member) || ts.isMethodDeclaration(member)) &&
    ts.isPrivateIdentifier(member.name)
  ) return;

  const deprecated = ctx.deprecated || isDeprecatedNode(member);

  if (ts.isConstructorDeclaration(member)) {
    const lineId = makeId(ctx.packageName, ctx.prefix + "constructor", "constructor");
    const t: ReviewToken[] = [];
    t.push(createToken(TokenKind.Keyword, "constructor", { deprecated }));
    const paramChildren = emitParameters(member.parameters, t, deprecated, ctx.referenceMap);
    const ctorLine: ReviewLine = { LineId: lineId, Tokens: t };
    if (paramChildren.length) ctorLine.Children = paramChildren;
    out.push(ctorLine);
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
    let propSemiTarget = t;
    if (member.type) {
      t.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      const propTypeChildren = buildTypeNodeTokens(member.type, t, deprecated, 0, ctx.referenceMap);
      if (propTypeChildren?.length) propSemiTarget = propTypeChildren[propTypeChildren.length - 1].Tokens;
      const propLine: ReviewLine = { LineId: lineId, Tokens: t };
      if (propTypeChildren?.length) propLine.Children = propTypeChildren;
      propSemiTarget.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
      out.push(propLine);
    } else {
      t.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
      out.push({ LineId: lineId, Tokens: t });
    }
  } else if (ts.isMethodDeclaration(member)) {
    const methodName = member.name.getText();
    // Only the first overload gets the canonical LineId (which matches the
    // reference map entry). Subsequent overloads of the same name must not
    // share it — give them no LineId so the uniqueness invariant is preserved.
    const isOverload = seenMethodNames?.has(methodName) ?? false;
    seenMethodNames?.add(methodName);
    const lineId = isOverload ? undefined : makeId(ctx.packageName, ctx.prefix + methodName, "method");
    const t: ReviewToken[] = [];
    const access = getAccessibilityKeyword(member);
    if (access) t.push(createToken(TokenKind.Keyword, access, { hasSuffixSpace: true, deprecated }));
    if (hasModifier(member, ts.ModifierFlags.Static))
      t.push(createToken(TokenKind.Keyword, "static", { hasSuffixSpace: true, deprecated }));
    if (hasModifier(member, ts.ModifierFlags.Abstract))
      t.push(createToken(TokenKind.Keyword, "abstract", { hasSuffixSpace: true, deprecated }));
    t.push(createToken(TokenKind.MemberName, methodName, { deprecated }));
    if (member.questionToken) t.push(createToken(TokenKind.Punctuation, "?", { deprecated }));
    const allMethodChildren: ReviewLine[] = [];
    const methodTpChildren = emitTypeParameters(member.typeParameters, t, deprecated, ctx.referenceMap);
    allMethodChildren.push(...methodTpChildren);
    const paramChildren = emitParameters(member.parameters, t, deprecated, ctx.referenceMap);
    allMethodChildren.push(...paramChildren);
    let semiTarget = paramChildren.length ? paramChildren[paramChildren.length - 1].Tokens : t;
    if (member.type) {
      semiTarget.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
      const returnChildren = buildTypeNodeTokens(member.type, semiTarget, deprecated, 0, ctx.referenceMap);
      if (returnChildren?.length) {
        allMethodChildren.push(...returnChildren);
        semiTarget = returnChildren[returnChildren.length - 1].Tokens;
      }
    }
    semiTarget.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
    const methodLine: ReviewLine = { LineId: lineId, Tokens: t };
    if (allMethodChildren.length) methodLine.Children = allMethodChildren;
    out.push(methodLine);
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
  t.push(createNameToken({ name, lineId, kind: TokenKind.MemberName, deprecated }));

  const tpChildren = emitTypeParameters(node.typeParameters, t, deprecated, ctx.referenceMap);
  const paramChildren = emitParameters(node.parameters, t, deprecated, ctx.referenceMap);
  const allFnChildren = [...tpChildren, ...paramChildren];
  let fnSemiTarget = paramChildren.length ? paramChildren[paramChildren.length - 1].Tokens : t;

  if (node.type) {
    fnSemiTarget.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
    const returnChildren = buildTypeNodeTokens(node.type, fnSemiTarget, deprecated, 0, ctx.referenceMap);
    if (returnChildren?.length) {
      allFnChildren.push(...returnChildren);
      fnSemiTarget = returnChildren[returnChildren.length - 1].Tokens;
    }
  }
  fnSemiTarget.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  const fnLine: ReviewLine = { LineId: lineId, Tokens: t };
  if (allFnChildren.length) fnLine.Children = allFnChildren;
  out.push(fnLine);
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
  t.push(createNameToken({ name: node.name.text, lineId, kind: TokenKind.TypeName, deprecated }));

  const tpChildren = emitTypeParameters(node.typeParameters, t, deprecated, ctx.referenceMap);
  t.push(createToken(TokenKind.Punctuation, "=", { hasPrefixSpace: true, hasSuffixSpace: true, deprecated }));

  const line: ReviewLine = { LineId: lineId, Tokens: t };
  const typeChildren = buildTypeNodeTokens(node.type, t, deprecated, 0, ctx.referenceMap);
  const allChildren = [...tpChildren, ...(typeChildren ?? [])];
  if (allChildren.length) line.Children = allChildren;
  const semiTarget = typeChildren?.length ? typeChildren[typeChildren.length - 1].Tokens : t;
  semiTarget.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

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
  t.push(createNameToken({ name: node.name.text, lineId, kind: TokenKind.TypeName, deprecated, renderClass: "enum" }));

  if (node.members.length > 0) {
    t[t.length - 1].HasSuffixSpace = true;
    t.push(createToken(TokenKind.Punctuation, "{", { deprecated }));

    for (const member of node.members) {
      const memberName = member.name.getText();
      const memberId = makeId(ctx.packageName, symbolPath + "." + memberName, "member");
      const mt: ReviewToken[] = [];
      const memberNameToken = createToken(TokenKind.MemberName, memberName, { deprecated });
      memberNameToken.NavigationDisplayName = memberName;
      mt.push(memberNameToken);
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
  const isConst = !!(node.declarationList.flags & ts.NodeFlags.Const);
  const isLet = !!(node.declarationList.flags & ts.NodeFlags.Let);
  const keyword = isConst ? "const" : isLet ? "let" : "var";

  for (const decl of node.declarationList.declarations) {
    if (!ts.isIdentifier(decl.name)) continue;
    const varName = decl.name.text;
    const symbolPath = ctx.prefix + varName;
    const lineId = makeId(ctx.packageName, symbolPath, "var");

    // JSDoc is on the VariableStatement, so pass `node` to emitPreamble
    const { deprecated } = emitPreamble(node, lineId, out, ctx);

    const t: ReviewToken[] = [];
    t.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));
    t.push(createToken(TokenKind.Keyword, keyword, { hasSuffixSpace: true, deprecated }));
    t.push(createNameToken({ name: varName, lineId, kind: TokenKind.MemberName, deprecated }));

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
  // Detect companion namespace: if a class/interface with the same name already exists
  // in the reference map, append "(namespace)" to disambiguate the sidebar entry.
  // e.g. `export class OpenAI` + `export namespace OpenAI` → sidebar shows "OpenAI (namespace)"
  const siblingId = ctx.referenceMap.get(name);
  const isCompanion = siblingId !== undefined && !siblingId.endsWith(":namespace");
  nameToken.NavigationDisplayName = isCompanion ? `${name} (namespace)` : name;
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
  /** Trailing comment on the declare module line */
  trailingComment?: string;
}

/**
 * Returns the list of entry points to process.
 * If the file contains any `declare module "..."` blocks they become separate subpaths.
 * Otherwise the entire file is treated as the default "." subpath.
 */
function getEntryPoints(sourceFile: ts.SourceFile): EntryPoint[] {
  const moduleMap = new Map<string, { statements: ts.Statement[]; trailingComment?: string }>();

  for (const stmt of sourceFile.statements) {
    if (
      ts.isModuleDeclaration(stmt) &&
      ts.isStringLiteral(stmt.name) &&
      stmt.body &&
      ts.isModuleBlock(stmt.body)
    ) {
      // Normalise: strip leading ./ so "." and "./" both map to "."
      const rawName = stmt.name.text;
      const subpath = rawName === "" || rawName === "./" ? "." : rawName;

      // Extract trailing comment from the opening brace line
      // e.g. declare module "foo" { // some comment
      let trailingComment: string | undefined;
      const fullText = sourceFile.getFullText();
      const afterName = fullText.slice(stmt.name.end);
      const braceIndex = afterName.indexOf("{");
      if (braceIndex !== -1) {
        const posAfterBrace = stmt.name.end + braceIndex + 1;
        const trailingComments = ts.getTrailingCommentRanges(fullText, posAfterBrace);
        if (trailingComments && trailingComments.length > 0) {
          const comment = fullText.slice(trailingComments[0].pos, trailingComments[0].end).trim();
          if (comment) trailingComment = comment;
        }
      }

      const existing = moduleMap.get(subpath);
      if (existing) {
        existing.statements.push(...stmt.body.statements);
      } else {
        moduleMap.set(subpath, { statements: [...stmt.body.statements], trailingComment });
      }
    }
  }

  if (moduleMap.size > 0) {
    return Array.from(moduleMap.entries()).map(([subpath, data]) => ({
      subpath,
      statements: data.statements,
      trailingComment: data.trailingComment,
    }));
  }

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

export interface ParsedModule {
  lines: ReviewLine[];
  /** Trailing comment on the declare module line */
  trailingComment?: string;
}

/**
 * Parses a `.d.ts` file and returns ReviewLine arrays keyed by subpath.
 * Each subpath corresponds to one entry in the final CodeFile review.
 */
export function parseDtsFile(options: DtsParseOptions): Map<string, ParsedModule> {
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
  const result = new Map<string, ParsedModule>();

  // For named-module entry points (declare module "foo") the package name used
  // for ID generation is the module name itself, not the packageName parameter.
  // For the "." fallback (top-level file, no declare module blocks) use packageName.
  function modulePackageName(ep: EntryPoint): string {
    return ep.subpath === "." ? packageName : ep.subpath;
  }

  // Build per-module reference maps first so we can construct scoped lookup
  // maps for each module: own types at highest priority, other modules' types
  // as fallback for cross-module references.
  const perModuleMaps = new Map<string, ReferenceMap>();
  for (const ep of entryPoints) {
    perModuleMaps.set(
      ep.subpath,
      buildLocalReferenceMap(ep.statements as ts.Statement[], modulePackageName(ep)),
    );
  }

  // Build a single fallback map (all modules, first-wins) once — O(N×M).
  // Each per-module scoped map clones this then overwrites with its own types,
  // reducing total work from O(N²×M) to O(N×M).
  const fallbackMap: ReferenceMap = new Map();
  for (const otherMap of perModuleMaps.values()) {
    for (const [name, id] of otherMap) {
      if (!fallbackMap.has(name)) fallbackMap.set(name, id);
    }
  }

  for (const ep of entryPoints) {
    // Build a scoped reference map for this module:
    //   1. clone the fallback map (all modules, first-wins among others)
    //   2. overwrite with this module's own types (always highest priority)
    // This ensures that a type defined locally always navigates to the local
    // definition, even when another module declares a type with the same name.
    const ownMap = perModuleMaps.get(ep.subpath)!;
    const scopedMap: ReferenceMap = new Map(fallbackMap);
    for (const [name, id] of ownMap) {
      scopedMap.set(name, id);
    }

    const ctx: VisitContext = {
      packageName: modulePackageName(ep),
      prefix: "",
      referenceMap: scopedMap,
      parentReleaseTag: undefined,
      deprecated: false,
    };

    const lines: ReviewLine[] = [];
    for (const stmt of ep.statements) {
      visitStatement(stmt as ts.Statement, lines, ctx);
    }

    result.set(ep.subpath, { lines, trailingComment: ep.trailingComment });
  }

  return result;
}
