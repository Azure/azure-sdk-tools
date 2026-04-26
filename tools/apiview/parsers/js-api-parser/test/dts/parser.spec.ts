// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { describe, it, expect, beforeAll, afterAll } from "vitest";
import path from "node:path";
import { parseDtsFile, ParsedModule } from "../../src/dts/parser.js";
import { ReviewLine, ReviewToken, TokenKind } from "../../src/models.js";

const FIXTURES = path.join(__dirname, "fixtures");

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function tokenValues(line: ReviewLine): string[] {
  return line.Tokens.map((t) => t.Value);
}

function joinTokens(line: ReviewLine): string {
  return tokenValues(line).join(" ");
}

function findLine(lines: ReviewLine[], lineIdSuffix: string): ReviewLine | undefined {
  for (const line of lines) {
    if (line.LineId?.endsWith(lineIdSuffix)) return line;
    if (line.Children) {
      const found = findLine(line.Children, lineIdSuffix);
      if (found) return found;
    }
  }
  return undefined;
}

function collectAllLines(lines: ReviewLine[]): ReviewLine[] {
  const result: ReviewLine[] = [];
  for (const line of lines) {
    result.push(line);
    if (line.Children) result.push(...collectAllLines(line.Children));
  }
  return result;
}

function collectAllTokens(line: ReviewLine): ReviewToken[] {
  const result: ReviewToken[] = [...line.Tokens];
  if (line.Children) {
    for (const child of line.Children) {
      result.push(...collectAllTokens(child));
    }
  }
  return result;
}

// ---------------------------------------------------------------------------
// basic.d.ts — flat file (no declare module blocks)
// ---------------------------------------------------------------------------

describe("parseDtsFile — basic.d.ts", () => {
  let subpathMap: Map<string, ParsedModule>;
  let lines: ReviewLine[];
  let pkg: string;

  beforeAll(() => {
    pkg = "@azure/http-client";
    subpathMap = parseDtsFile({
      filePath: path.join(FIXTURES, "basic.d.ts"),
      packageName: pkg,
    });
  });

  it("produces a single '.' entry point when no declare module blocks are present", () => {
    expect(subpathMap.size).toBe(1);
    expect(subpathMap.has(".")).toBe(true);
    lines = subpathMap.get(".")!.lines;
    expect(lines.length).toBeGreaterThan(0);
  });

  describe("interface", () => {
    it("generates an interface declaration line with correct LineId", () => {
      lines = subpathMap.get(".")!.lines;
      const iface = findLine(lines, ":interface");
      expect(iface).toBeDefined();
    });

    it("emits export + interface keywords and name for RequestPolicy", () => {
      lines = subpathMap.get(".")!.lines;
      const iface = findLine(lines, "RequestPolicy:interface");
      expect(iface).toBeDefined();
      const tokens = tokenValues(iface!);
      expect(tokens).toContain("export");
      expect(tokens).toContain("interface");
      expect(tokens).toContain("RequestPolicy");
    });

    it("sets NavigationDisplayName on the interface name token", () => {
      lines = subpathMap.get(".")!.lines;
      const iface = findLine(lines, "RequestPolicy:interface");
      const nameToken = iface!.Tokens.find((t) => t.Value === "RequestPolicy");
      expect(nameToken?.NavigationDisplayName).toBe("RequestPolicy");
    });

    it("sets RenderClasses=['interface'] on the name token", () => {
      lines = subpathMap.get(".")!.lines;
      const iface = findLine(lines, "RequestPolicy:interface");
      const nameToken = iface!.Tokens.find((t) => t.Value === "RequestPolicy");
      expect(nameToken?.RenderClasses).toContain("interface");
    });

    it("has the method as a child of the interface", () => {
      lines = subpathMap.get(".")!.lines;
      const iface = findLine(lines, "RequestPolicy:interface");
      const memberNames = (iface!.Children ?? []).flatMap((c) =>
        c.Tokens.map((t) => t.Value),
      );
      expect(memberNames).toContain("sendRequest");
    });

    it("emits opening brace on the declaration line", () => {
      lines = subpathMap.get(".")!.lines;
      const iface = findLine(lines, "RequestPolicy:interface");
      expect(tokenValues(iface!)).toContain("{");
    });

    it("emits a context-end line with closing brace after the interface", () => {
      lines = subpathMap.get(".")!.lines;
      const all = collectAllLines(lines);
      const ifaceLine = all.find((l) => l.LineId?.endsWith("RequestPolicy:interface"));
      expect(ifaceLine).toBeDefined();
      // The context-end line is identified by RelatedToLine pointing back to the declaration.
      const contextEnd = all.find((l) => l.IsContextEndLine && l.RelatedToLine === ifaceLine!.LineId);
      expect(contextEnd?.IsContextEndLine).toBe(true);
      expect(contextEnd?.Tokens.some((t) => t.Value === "}")).toBe(true);
    });
  });

  describe("class", () => {
    it("generates a class declaration with correct keywords", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "HttpClient:class");
      expect(cls).toBeDefined();
      const tokens = tokenValues(cls!);
      expect(tokens).toContain("export");
      expect(tokens).toContain("class");
      expect(tokens).toContain("HttpClient");
    });

    it("sets RenderClasses=['class'] on the name token", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "HttpClient:class");
      const nameToken = cls!.Tokens.find((t) => t.Value === "HttpClient");
      expect(nameToken?.RenderClasses).toContain("class");
    });

    it("emits 'extends' and parent class name for RetryableHttpClient", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "RetryableHttpClient:class");
      const tokens = tokenValues(cls!);
      expect(tokens).toContain("extends");
      expect(tokens).toContain("HttpClient");
    });

    it("generates a constructor child line", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "RetryableHttpClient:class");
      const ctorLine = (cls!.Children ?? []).find((c) =>
        c.LineId?.endsWith(":constructor"),
      );
      expect(ctorLine).toBeDefined();
      expect(tokenValues(ctorLine!)).toContain("constructor");
    });

    it("generates property child lines with correct modifiers", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "RetryableHttpClient:class");
      const maxRetriesProp = (cls!.Children ?? []).find((c) =>
        c.LineId?.endsWith("maxRetries:property"),
      );
      expect(maxRetriesProp).toBeDefined();
      expect(tokenValues(maxRetriesProp!)).toContain("readonly");
    });

    it("generates method child lines", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "RetryableHttpClient:class");
      const methodLine = (cls!.Children ?? []).find((c) =>
        c.LineId?.endsWith("sendRequest:method"),
      );
      expect(methodLine).toBeDefined();
    });
  });

  describe("function", () => {
    it("generates a function declaration line", () => {
      lines = subpathMap.get(".")!.lines;
      const fn = findLine(lines, "createHttpClient:function");
      expect(fn).toBeDefined();
      const tokens = tokenValues(fn!);
      expect(tokens).toContain("export");
      expect(tokens).toContain("function");
      expect(tokens).toContain("createHttpClient");
    });

    it("emits a semicolon somewhere in the function declaration", () => {
      lines = subpathMap.get(".")!.lines;
      const fn = findLine(lines, "createHttpClient:function");
      // The inline object parameter type '{timeout?: number}' expands to children,
      // so the ';' may land on the last child line rather than the header line.
      const allTokens = [
        ...tokenValues(fn!),
        ...(fn!.Children ?? []).flatMap((c) => tokenValues(c)),
      ];
      expect(allTokens.at(-1)).toBe(";");
    });

    it("handles multiline type parameter constraint correctly", () => {
      lines = subpathMap.get(".")!.lines;
      const fn = findLine(lines, "processEntity:function");
      expect(fn).toBeDefined();
      // Should have children for the constraint object
      expect(fn!.Children).toBeDefined();
      expect(fn!.Children!.length).toBeGreaterThan(0);
      // Last child should have } > ( ... ) : T ;
      const lastChild = fn!.Children![fn!.Children!.length - 1];
      const lastTokens = tokenValues(lastChild);
      expect(lastTokens).toContain("}");
      expect(lastTokens).toContain(">");
      expect(lastTokens).toContain("(");
      expect(lastTokens.at(-1)).toBe(";");
    });
  });

  describe("enum", () => {
    it("generates an enum declaration with correct structure", () => {
      lines = subpathMap.get(".")!.lines;
      const en = findLine(lines, "HttpMethod:enum");
      expect(en).toBeDefined();
      const tokens = tokenValues(en!);
      expect(tokens).toContain("export");
      expect(tokens).toContain("enum");
      expect(tokens).toContain("HttpMethod");
      expect(tokens).toContain("{");
    });

    it("generates enum member children with values", () => {
      lines = subpathMap.get(".")!.lines;
      const en = findLine(lines, "HttpMethod:enum");
      const getMember = findLine(en!.Children ?? [], "HttpMethod.Get:member");
      expect(getMember).toBeDefined();
      const tokens = tokenValues(getMember!);
      expect(tokens).toContain("Get");
      expect(tokens).toContain("=");
      expect(tokens).toContain('"GET"');
    });
  });

  describe("type alias", () => {
    it("generates a type alias declaration with export + type keywords", () => {
      lines = subpathMap.get(".")!.lines;
      const ta = findLine(lines, "ContentType:typealias");
      expect(ta).toBeDefined();
      const tokens = tokenValues(ta!);
      expect(tokens).toContain("export");
      expect(tokens).toContain("type");
      expect(tokens).toContain("ContentType");
      expect(tokens).toContain("=");
    });

    it("generates a generic type alias with type parameters", () => {
      lines = subpathMap.get(".")!.lines;
      const ta = findLine(lines, "OperationOptions:typealias");
      expect(ta).toBeDefined();
      const tokens = tokenValues(ta!);
      expect(tokens).toContain("<");
      expect(tokens).toContain("T");
    });

    it("handles multiline type parameter constraint correctly", () => {
      lines = subpathMap.get(".")!.lines;
      const ta = findLine(lines, "EntityProcessor:typealias");
      expect(ta).toBeDefined();
      // Should have children for the constraint object
      expect(ta!.Children).toBeDefined();
      expect(ta!.Children!.length).toBeGreaterThan(0);
      // Last child should have } > = and the type body ending with ;
      const lastChild = ta!.Children![ta!.Children!.length - 1];
      const lastTokens = tokenValues(lastChild);
      expect(lastTokens).toContain("}");
      expect(lastTokens).toContain(">");
      expect(lastTokens).toContain("=");
      expect(lastTokens.at(-1)).toBe(";");
    });
  });

  describe("variable", () => {
    it("generates a const declaration", () => {
      lines = subpathMap.get(".")!.lines;
      const v = findLine(lines, "DEFAULT_TIMEOUT:var");
      expect(v).toBeDefined();
      const tokens = tokenValues(v!);
      expect(tokens).toContain("export");
      expect(tokens).toContain("const");
      expect(tokens).toContain("DEFAULT_TIMEOUT");
    });

    it("sets NavigateToId on the variable name token", () => {
      lines = subpathMap.get(".")!.lines;
      const v = findLine(lines, "DEFAULT_TIMEOUT:var");
      expect(v).toBeDefined();
      const nameToken = v!.Tokens.find((t) => t.Value === "DEFAULT_TIMEOUT");
      expect(nameToken?.NavigateToId).toBeTruthy();
    });

    it("emits an empty ReviewLine after each variable declaration", () => {
      lines = subpathMap.get(".")!.lines;
      const all = collectAllLines(lines);
      const varLine = all.find((l) => l.LineId?.endsWith("DEFAULT_TIMEOUT:var"));
      expect(varLine).toBeDefined();
      const varIdx = all.indexOf(varLine!);
      const emptyAfter = all[varIdx + 1];
      expect(emptyAfter?.Tokens.length).toBe(0);
    });

    it("handles multiline object type with children", () => {
      lines = subpathMap.get(".")!.lines;
      const v = findLine(lines, "CONFIG:var");
      expect(v).toBeDefined();
      // Should have children for the object type members
      expect(v!.Children).toBeDefined();
      expect(v!.Children!.length).toBeGreaterThan(0);
      // Last child should have the closing brace and semicolon
      const lastChild = v!.Children![v!.Children!.length - 1];
      const lastTokens = tokenValues(lastChild);
      expect(lastTokens).toContain("}");
      expect(lastTokens).toContain(";");
    });
  });

  describe("accessors", () => {
    it("generates getter accessor with correct keywords", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "AccessorExample:class");
      expect(cls?.Children).toBeDefined();
      const getter = findLine(cls!.Children!, "value:get");
      expect(getter).toBeDefined();
      const tokens = tokenValues(getter!);
      expect(tokens).toContain("get");
      expect(tokens).toContain("value");
    });

    it("generates setter accessor with correct keywords", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "AccessorExample:class");
      const setter = findLine(cls!.Children!, "value:set");
      expect(setter).toBeDefined();
      const tokens = tokenValues(setter!);
      expect(tokens).toContain("set");
      expect(tokens).toContain("value");
    });

    it("generates static getter accessor", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "AccessorExample:class");
      const staticGetter = findLine(cls!.Children!, "instanceCount:get");
      expect(staticGetter).toBeDefined();
      const tokens = tokenValues(staticGetter!);
      expect(tokens).toContain("static");
      expect(tokens).toContain("get");
      expect(tokens).toContain("instanceCount");
    });
  });

  describe("export default", () => {
    it("emits export default keyword for default exported class", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "DefaultClient:class");
      expect(cls).toBeDefined();
      const tokens = tokenValues(cls!);
      expect(tokens).toContain("export");
      expect(tokens).toContain("default");
      expect(tokens).toContain("class");
      expect(tokens).toContain("DefaultClient");
    });
  });

  describe("class method multiline type constraint", () => {
    it("handles method with multiline type parameter constraint", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "ConstraintMethodClass:class");
      expect(cls?.Children).toBeDefined();
      const method = findLine(cls!.Children!, "process:method");
      expect(method).toBeDefined();
      // Should have children for the constraint object
      expect(method!.Children).toBeDefined();
      expect(method!.Children!.length).toBeGreaterThan(0);
      // Opening `(` should be on a child line (after multiline constraint), not header
      const allTokens = collectAllTokens(method!);
      const openParenIdx = allTokens.findIndex((t) => t.Value === "(");
      const closeAngleIdx = allTokens.findIndex((t) => t.Value === ">");
      expect(openParenIdx).toBeGreaterThan(closeAngleIdx);
    });
  });

  describe("constructor overloads", () => {
    it("first constructor overload gets LineId", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "OverloadedConstructor:class");
      expect(cls?.Children).toBeDefined();
      const ctors = cls!.Children!.filter((c) =>
        tokenValues(c).includes("constructor"),
      );
      expect(ctors.length).toBe(3);
      // First overload should have LineId
      expect(ctors[0].LineId).toBeDefined();
      expect(ctors[0].LineId).toContain("constructor");
      // Subsequent overloads should not have LineId
      expect(ctors[1].LineId).toBeUndefined();
      expect(ctors[2].LineId).toBeUndefined();
    });
  });

  describe("constructor parameter properties", () => {
    it("emits parameter modifiers in constructor", () => {
      lines = subpathMap.get(".")!.lines;
      const cls = findLine(lines, "ParameterProperties:class");
      const ctor = cls!.Children!.find((c) =>
        tokenValues(c).includes("constructor"),
      );
      expect(ctor).toBeDefined();
      const tokens = tokenValues(ctor!);
      expect(tokens).toContain("public");
      expect(tokens).toContain("private");
      expect(tokens).toContain("readonly");
    });
  });

  describe("function overloads", () => {
    it("first function overload gets LineId", () => {
      lines = subpathMap.get(".")!.lines;
      const all = collectAllLines(lines);
      const parseFns = all.filter((l) =>
        l.Tokens.some((t) => t.Value === "parse") &&
        l.Tokens.some((t) => t.Value === "function"),
      );
      expect(parseFns.length).toBe(2);
      // First overload should have LineId
      expect(parseFns[0].LineId).toBeDefined();
      expect(parseFns[0].LineId).toContain("parse:function");
      // Subsequent overload should not have LineId
      expect(parseFns[1].LineId).toBeUndefined();
    });
  });

  describe("type operators", () => {
    it("renders keyof keyword", () => {
      lines = subpathMap.get(".")!.lines;
      const ta = findLine(lines, "Keys:typealias");
      expect(ta).toBeDefined();
      const tokens = tokenValues(ta!);
      expect(tokens).toContain("keyof");
    });

    it("renders typeof keyword", () => {
      lines = subpathMap.get(".")!.lines;
      const ta = findLine(lines, "ConfigType:typealias");
      expect(ta).toBeDefined();
      const tokens = tokenValues(ta!);
      expect(tokens).toContain("typeof");
      expect(tokens).toContain("CONFIG_OBJ");
    });

    it("renders indexed access type", () => {
      lines = subpathMap.get(".")!.lines;
      const ta = findLine(lines, "PropertyType:typealias");
      expect(ta).toBeDefined();
      const allTokens = collectAllTokens(ta!);
      const values = allTokens.map((t) => t.Value);
      expect(values).toContain("T");
      expect(values).toContain("[");
      expect(values).toContain("K");
      expect(values).toContain("]");
    });

    it("renders mapped type", () => {
      lines = subpathMap.get(".")!.lines;
      const ta = findLine(lines, "Readonly2:typealias");
      expect(ta).toBeDefined();
      const allTokens = collectAllTokens(ta!);
      const values = allTokens.map((t) => t.Value);
      expect(values).toContain("readonly");
      expect(values).toContain("in");
      expect(values).toContain("keyof");
    });
  });

  describe("namespace", () => {
    it("generates a namespace declaration with export + namespace keywords", () => {
      lines = subpathMap.get(".")!.lines;
      const ns = findLine(lines, "Internal:namespace");
      expect(ns).toBeDefined();
      const tokens = tokenValues(ns!);
      expect(tokens).toContain("export");
      expect(tokens).toContain("namespace");
      expect(tokens).toContain("Internal");
    });

    it("places nested members as children of the namespace", () => {
      lines = subpathMap.get(".")!.lines;
      const ns = findLine(lines, "Internal:namespace");
      expect(ns!.Children?.length).toBeGreaterThan(0);
    });
  });

  describe("documentation", () => {
    it("emits JSDoc comment lines as documentation tokens related to the declaration", () => {
      lines = subpathMap.get(".")!.lines;
      const all = collectAllLines(lines);
      const ifaceIdx = all.findIndex((l) => l.LineId?.endsWith("RequestPolicy:interface"));
      // Comment lines appear before the declaration line and reference it
      const commentsBefore = all
        .slice(0, ifaceIdx)
        .filter((l) => l.RelatedToLine?.endsWith("RequestPolicy:interface"));
      expect(commentsBefore.length).toBeGreaterThan(0);
      const docTokens = commentsBefore.flatMap((l) => l.Tokens);
      expect(docTokens.some((t) => t.Kind === TokenKind.Comment)).toBe(true);
    });
  });

  describe("blank lines between declarations", () => {
    it("emits an empty ReviewLine after each top-level declaration", () => {
      lines = subpathMap.get(".")!.lines;
      const all = collectAllLines(lines);
      const ifaceLine = all.find((l) => l.LineId?.endsWith("RequestPolicy:interface"));
      expect(ifaceLine).toBeDefined();
      // The context-end line comes after all children in the flat list
      const contextEnd = all.find((l) => l.IsContextEndLine && l.RelatedToLine === ifaceLine!.LineId);
      expect(contextEnd?.IsContextEndLine).toBe(true);
      const contextIdx = all.indexOf(contextEnd!);
      // The empty line that follows (RelatedToLine = the iface line id)
      const emptyAfter = all[contextIdx + 1];
      expect(emptyAfter?.Tokens.length).toBe(0);
    });
  });
});

// ---------------------------------------------------------------------------
// subpaths.d.ts — declare module blocks
// ---------------------------------------------------------------------------

describe("parseDtsFile — subpaths.d.ts", () => {
  let subpathMap: Map<string, ReviewLine[]>;

  beforeAll(() => {
    subpathMap = parseDtsFile({
      filePath: path.join(FIXTURES, "subpaths.d.ts"),
      packageName: "@azure/storage",
    });
  });

  it("produces three distinct subpath entry points", () => {
    expect(subpathMap.size).toBe(3);
    expect(subpathMap.has(".")).toBe(true);
    expect(subpathMap.has("./models")).toBe(true);
    expect(subpathMap.has("./storage")).toBe(true);
  });

  it('"./models" subpath contains StorageOptions interface', () => {
    const modelsLines = subpathMap.get("./models")!.lines;
    const iface = findLine(modelsLines, "StorageOptions:interface");
    expect(iface).toBeDefined();
    expect(tokenValues(iface!)).toContain("StorageOptions");
  });

  it('"./models" subpath contains StorageErrorCode enum', () => {
    const modelsLines = subpathMap.get("./models")!.lines;
    const en = findLine(modelsLines, "StorageErrorCode:enum");
    expect(en).toBeDefined();
  });

  it('"./storage" subpath contains BlobClient class', () => {
    const storageLines = subpathMap.get("./storage")!.lines;
    const cls = findLine(storageLines, "BlobClient:class");
    expect(cls).toBeDefined();
    expect(tokenValues(cls!)).toContain("BlobClient");
  });

  it("BlobClient has constructor, url property, and upload method", () => {
    const storageLines = subpathMap.get("./storage")!.lines;
    const cls = findLine(storageLines, "BlobClient:class");
    const children = cls!.Children ?? [];
    expect(children.some((c) => c.LineId?.endsWith(":constructor"))).toBe(true);
    expect(children.some((c) => c.LineId?.endsWith("url:property"))).toBe(true);
    expect(children.some((c) => c.LineId?.endsWith("upload:method"))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// tags.d.ts — TSDoc release tags and deprecation
// ---------------------------------------------------------------------------

describe("parseDtsFile — tags.d.ts", () => {
  let subpathMap: Map<string, ReviewLine[]>;
  let lines: ReviewLine[];

  beforeAll(() => {
    subpathMap = parseDtsFile({
      filePath: path.join(FIXTURES, "tags.d.ts"),
      packageName: "@azure/tags-test",
    });
    lines = subpathMap.get(".")!.lines;
  });

  it("emits a @beta line before BetaFeatureOptions", () => {
    const all = collectAllLines(lines);
    const ifaceIdx = all.findIndex((l) => l.LineId?.endsWith("BetaFeatureOptions:interface"));
    const linesBefore = all.slice(0, ifaceIdx);
    const hasBeta = linesBefore.some((l) =>
      l.Tokens.some((t) => t.Value === "@beta"),
    );
    expect(hasBeta).toBe(true);
  });

  it("emits a @alpha line before AlphaFeatureOptions", () => {
    const all = collectAllLines(lines);
    const ifaceIdx = all.findIndex((l) => l.LineId?.endsWith("AlphaFeatureOptions:interface"));
    const linesBefore = all.slice(0, ifaceIdx);
    const hasAlpha = linesBefore.some((l) =>
      l.Tokens.some((t) => t.Value === "@alpha"),
    );
    expect(hasAlpha).toBe(true);
  });

  it("emits a @deprecated line before LegacyClient", () => {
    const all = collectAllLines(lines);
    const clsIdx = all.findIndex((l) => l.LineId?.endsWith("LegacyClient:class"));
    const linesBefore = all.slice(0, clsIdx);
    const hasDeprecated = linesBefore.some((l) =>
      l.Tokens.some((t) => t.Value === "@deprecated"),
    );
    expect(hasDeprecated).toBe(true);
  });

  it("marks deprecated tokens with IsDeprecated=true on LegacyClient class line", () => {
    const cls = findLine(lines, "LegacyClient:class");
    expect(cls).toBeDefined();
    // The class name token should have IsDeprecated
    const nameToken = cls!.Tokens.find((t) => t.Value === "LegacyClient");
    expect(nameToken?.IsDeprecated).toBe(true);
  });

  it("emits @beta for betaMethod inside a non-beta class", () => {
    const all = collectAllLines(lines);
    const methodIdx = all.findIndex((l) => l.LineId?.endsWith("betaMethod:method"));
    expect(methodIdx).toBeGreaterThanOrEqual(0);
    const linesBefore = all.slice(0, methodIdx);
    const hasBeta = linesBefore.some((l) =>
      l.Tokens.some((t) => t.Value === "@beta"),
    );
    expect(hasBeta).toBe(true);
  });

  it("does NOT re-emit @beta for plain methods inside BetaUtils namespace", () => {
    const all = collectAllLines(lines);
    const nsLine = findLine(lines, "BetaUtils:namespace");
    expect(nsLine).toBeDefined();
    // All lines inside BetaUtils namespace
    const nsChildren = collectAllLines(nsLine!.Children ?? []);
    const helperFnIdx = nsChildren.findIndex((l) =>
      l.Tokens.some((t) => t.Value === "helper"),
    );
    expect(helperFnIdx).toBeGreaterThanOrEqual(0);
    // No @beta preamble before helper within namespace children
    const before = nsChildren.slice(0, helperFnIdx);
    const hasBeta = before.some((l) => l.Tokens.some((t) => t.Value === "@beta"));
    expect(hasBeta).toBe(false);
  });

  it("DOES emit @alpha for experimental inside BetaUtils (different tag from parent)", () => {
    const nsLine = findLine(lines, "BetaUtils:namespace");
    const nsChildren = collectAllLines(nsLine!.Children ?? []);
    const expIdx = nsChildren.findIndex((l) =>
      l.Tokens.some((t) => t.Value === "experimental"),
    );
    expect(expIdx).toBeGreaterThanOrEqual(0);
    const before = nsChildren.slice(0, expIdx);
    const hasAlpha = before.some((l) => l.Tokens.some((t) => t.Value === "@alpha"));
    expect(hasAlpha).toBe(true);
  });

  it("emits @beta before betaFunction", () => {
    const all = collectAllLines(lines);
    const fnIdx = all.findIndex((l) => l.LineId?.endsWith("betaFunction:function"));
    const linesBefore = all.slice(0, fnIdx);
    const hasBeta = linesBefore.some((l) =>
      l.Tokens.some((t) => t.Value === "@beta"),
    );
    expect(hasBeta).toBe(true);
  });

  it("emits @alpha before ALPHA_CONSTANT", () => {
    const all = collectAllLines(lines);
    const varIdx = all.findIndex((l) => l.LineId?.endsWith("ALPHA_CONSTANT:var"));
    const linesBefore = all.slice(0, varIdx);
    const alphaLine = linesBefore.find((l) =>
      l.Tokens.some((t) => t.Value === "@alpha"),
    );
    expect(alphaLine).toBeDefined();
    // Release tag preamble should use Keyword kind, not StringLiteral
    const alphaToken = alphaLine!.Tokens.find((t) => t.Value === "@alpha");
    expect(alphaToken?.Kind).toBe(TokenKind.Keyword);
  });
});

// ---------------------------------------------------------------------------
// namespace.d.ts — deeply nested namespaces
// ---------------------------------------------------------------------------

describe("parseDtsFile — namespace.d.ts", () => {
  let subpathMap: Map<string, ReviewLine[]>;
  let lines: ReviewLine[];

  beforeAll(() => {
    subpathMap = parseDtsFile({
      filePath: path.join(FIXTURES, "namespace.d.ts"),
      packageName: "@azure/ns-test",
    });
    lines = subpathMap.get(".")!.lines;
  });

  it("generates Outer namespace", () => {
    const ns = findLine(lines, "Outer:namespace");
    expect(ns).toBeDefined();
    expect(tokenValues(ns!)).toContain("namespace");
    expect(tokenValues(ns!)).toContain("Outer");
  });

  it("generates Inner namespace nested inside Outer", () => {
    const outerNs = findLine(lines, "Outer:namespace");
    const innerNs = findLine(outerNs!.Children ?? [], "Outer.Inner:namespace");
    expect(innerNs).toBeDefined();
  });

  it("generates DeepNested namespace at three levels deep", () => {
    const outerNs = findLine(lines, "Outer:namespace");
    const innerNs = findLine(outerNs!.Children ?? [], "Outer.Inner:namespace");
    const deep = findLine(innerNs!.Children ?? [], "Outer.Inner.DeepNested:namespace");
    expect(deep).toBeDefined();
  });

  it("generates OuterInterface inside Outer", () => {
    const outerNs = findLine(lines, "Outer:namespace");
    const iface = findLine(outerNs!.Children ?? [], "Outer.OuterInterface:interface");
    expect(iface).toBeDefined();
  });

  it("generates DEEP_CONST variable inside DeepNested", () => {
    const outerNs = findLine(lines, "Outer:namespace");
    const innerNs = findLine(outerNs!.Children ?? [], "Outer.Inner:namespace");
    const deep = findLine(innerNs!.Children ?? [], "Outer.Inner.DeepNested:namespace");
    const deepConst = findLine(deep!.Children ?? [], "Outer.Inner.DeepNested.DEEP_CONST:var");
    expect(deepConst).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// NavigateToId cross-references
// ---------------------------------------------------------------------------

describe("parseDtsFile — NavigateToId cross-references (basic.d.ts)", () => {
  let lines: ReviewLine[];

  beforeAll(() => {
    const subpathMap = parseDtsFile({
      filePath: path.join(FIXTURES, "basic.d.ts"),
      packageName: "@azure/http-client",
    });
    lines = subpathMap.get(".")!.lines;
  });

  it("sets NavigateToId on the 'extends HttpClient' reference in RetryableHttpClient", () => {
    const cls = findLine(lines, "RetryableHttpClient:class");
    const extendsToken = cls!.Tokens.find((t) => t.Value === "HttpClient");
    expect(extendsToken).toBeDefined();
    expect(extendsToken?.NavigateToId).toContain("HttpClient:class");
  });
});

// ---------------------------------------------------------------------------
// Cross-module name collision: each module's own types take priority
// ---------------------------------------------------------------------------

describe("parseDtsFile — cross-module name collision", () => {
  // Fixture: openai declares "Agent" first, then @azure/ai-projects also declares
  // its own "Agent". References to "Agent" within each module must resolve to that
  // module's own definition, not the other module's.
  const FIXTURE_CONTENT = `
declare module "openai" {
  export interface Agent { openaiField: string; }
  export interface AgentCreateParams { model: string; }
}
declare module "@azure/ai-projects" {
  export interface Agent { azureField: string; }
  export interface AgentsOperations {
    create(params: AgentCreateParams): Agent;
  }
  export interface AgentCreateParams { name: string; }
}
`;

  let parsed: Map<string, ReviewLine[]>;
  const TEMP = path.join(path.dirname(FIXTURES), "tmp-name-collision.d.ts");

  beforeAll(async () => {
    const { writeFileSync } = await import("node:fs");
    writeFileSync(TEMP, FIXTURE_CONTENT);
    parsed = parseDtsFile({ filePath: TEMP, packageName: "@azure/ai-projects" });
  });

  afterAll(async () => {
    const { unlinkSync } = await import("node:fs");
    unlinkSync(TEMP);
  });

  it("openai Agent navigates to openai!Agent:interface", () => {
    const openaiLines = parsed.get("openai")!.lines;
    const all = collectAllLines(openaiLines);
    const agentLine = all.find((l) => l.LineId === "openai!Agent:interface");
    expect(agentLine).toBeDefined();
  });

  it("@azure/ai-projects Agent navigates to @azure/ai-projects!Agent:interface", () => {
    const azureLines = parsed.get("@azure/ai-projects")!.lines;
    const all = collectAllLines(azureLines);
    // AgentsOperations.create() return type Agent must use the local module's ID
    const createLine = all.find(
      (l) =>
        l.Tokens.some((t) => t.Value === "create") &&
        l.Tokens.some((t) => t.NavigateToId === "@azure/ai-projects!Agent:interface"),
    );
    expect(createLine).toBeDefined();
  });

  it("@azure/ai-projects Agent does NOT navigate to openai!Agent:interface", () => {
    const azureLines = parsed.get("@azure/ai-projects")!.lines;
    const all = collectAllLines(azureLines);
    const wrongNav = all
      .flatMap((l) => l.Tokens)
      .find((t) => t.Value === "Agent" && t.NavigateToId === "openai!Agent:interface");
    expect(wrongNav).toBeUndefined();
  });

  it("AgentCreateParams in @azure/ai-projects navigates to the local module's AgentCreateParams", () => {
    // AgentCreateParams is defined in BOTH modules; within @azure/ai-projects
    // the parameter type must resolve to the local definition.
    const azureLines = parsed.get("@azure/ai-projects")!.lines;
    const all = collectAllLines(azureLines);
    const localNav = all
      .flatMap((l) => l.Tokens)
      .find(
        (t) =>
          t.Value === "AgentCreateParams" &&
          t.NavigateToId === "@azure/ai-projects!AgentCreateParams:interface",
      );
    expect(localNav).toBeDefined();
  });

  it("AgentCreateParams in openai module navigates to openai!AgentCreateParams:interface", () => {
    const openaiLines = parsed.get("openai")!.lines;
    const all = collectAllLines(openaiLines);
    // Within openai, AgentCreateParams must resolve to the openai module's own
    const localNav = all
      .flatMap((l) => l.Tokens)
      .find(
        (t) =>
          t.Value === "AgentCreateParams" &&
          t.NavigateToId === "openai!AgentCreateParams:interface",
      );
    expect(localNav).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Class method overloads: no duplicate LineIds
// ---------------------------------------------------------------------------

describe("parseDtsFile — class method overloads", () => {
  // Verify that overloaded class methods do not share a LineId.
  // The first overload gets the canonical LineId; subsequent overloads get none.
  const FIXTURE_CONTENT = `
declare module "@azure/test" {
  export class Client {
    create(opts: OptionsA): Promise<Result>;
    create(opts: OptionsB): Promise<Result>;
    get(id: string): Promise<Result>;
  }
  export interface OptionsA { a: string; }
  export interface OptionsB { b: number; }
  export interface Result { id: string; }
}
`;

  let lines: ReviewLine[];
  const TEMP = path.join(path.dirname(FIXTURES), "tmp-overloads.d.ts");

  beforeAll(async () => {
    const { writeFileSync } = await import("node:fs");
    writeFileSync(TEMP, FIXTURE_CONTENT);
    const result = parseDtsFile({ filePath: TEMP, packageName: "@azure/test" });
    lines = collectAllLines(result.get("@azure/test")!.lines);
  });

  afterAll(async () => {
    const { unlinkSync } = await import("node:fs");
    unlinkSync(TEMP);
  });

  it("first create overload gets the canonical LineId", () => {
    const createLines = lines.filter((l) => l.Tokens.some((t) => t.Value === "create"));
    const withId = createLines.filter((l) => l.LineId);
    expect(withId).toHaveLength(1);
    expect(withId[0].LineId).toBe("@azure/test!Client.create:method");
  });

  it("second create overload has no LineId", () => {
    const createLines = lines.filter((l) => l.Tokens.some((t) => t.Value === "create"));
    expect(createLines).toHaveLength(2);
    const withoutId = createLines.filter((l) => !l.LineId);
    expect(withoutId).toHaveLength(1);
  });

  it("non-overloaded get method retains its LineId", () => {
    const getLine = lines.find(
      (l) => l.LineId === "@azure/test!Client.get:method",
    );
    expect(getLine).toBeDefined();
  });

  it("all LineIds across the module are unique", () => {
    const ids = lines.filter((l) => l.LineId).map((l) => l.LineId!);
    expect(new Set(ids).size).toBe(ids.length);
  });
});

// ---------------------------------------------------------------------------
// Enum member NavigationDisplayName
// ---------------------------------------------------------------------------

describe("parseDtsFile — enum member NavigationDisplayName", () => {
  const FIXTURE_CONTENT = `
declare module "@azure/test" {
  export enum Status {
    Active = "active",
    Inactive = "inactive",
  }
}
`;

  let lines: ReviewLine[];
  const TEMP = path.join(path.dirname(FIXTURES), "tmp-enum-nav.d.ts");

  beforeAll(async () => {
    const { writeFileSync } = await import("node:fs");
    writeFileSync(TEMP, FIXTURE_CONTENT);
    const result = parseDtsFile({ filePath: TEMP, packageName: "@azure/test" });
    lines = collectAllLines(result.get("@azure/test")!.lines);
  });

  afterAll(async () => {
    const { unlinkSync } = await import("node:fs");
    unlinkSync(TEMP);
  });

  it("enum member has NavigationDisplayName set on the name token", () => {
    const en = findLine(lines, "Status:enum");
    expect(en).toBeDefined();
    const activeLine = findLine(en!.Children ?? [], "Status.Active:member");
    expect(activeLine).toBeDefined();
    const nameToken = activeLine!.Tokens.find((t) => t.Value === "Active");
    expect(nameToken?.NavigationDisplayName).toBe("Active");
  });

  it("second enum member also has NavigationDisplayName", () => {
    const en = findLine(lines, "Status:enum");
    const inactiveLine = findLine(en!.Children ?? [], "Status.Inactive:member");
    expect(inactiveLine).toBeDefined();
    const nameToken = inactiveLine!.Tokens.find((t) => t.Value === "Inactive");
    expect(nameToken?.NavigationDisplayName).toBe("Inactive");
  });
});

// ---------------------------------------------------------------------------
// Duplicate declare module merge
// ---------------------------------------------------------------------------

describe("parseDtsFile — duplicate declare module blocks are merged", () => {
  const FIXTURE_CONTENT = `
declare module "@azure/test" {
  export interface Foo { x: string; }
}
declare module "@azure/test" {
  export interface Bar { y: number; }
}
`;

  let result: Map<string, ReviewLine[]>;
  const TEMP = path.join(path.dirname(FIXTURES), "tmp-dup-module.d.ts");

  beforeAll(async () => {
    const { writeFileSync } = await import("node:fs");
    writeFileSync(TEMP, FIXTURE_CONTENT);
    result = parseDtsFile({ filePath: TEMP, packageName: "@azure/test" });
  });

  afterAll(async () => {
    const { unlinkSync } = await import("node:fs");
    unlinkSync(TEMP);
  });

  it("produces exactly one entry in the result map", () => {
    expect(result.size).toBe(1);
    expect(result.has("@azure/test")).toBe(true);
  });

  it("merged module contains both Foo and Bar", () => {
    const lines = collectAllLines(result.get("@azure/test")!.lines);
    expect(findLine(lines, "Foo:interface")).toBeDefined();
    expect(findLine(lines, "Bar:interface")).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Call signatures and construct signatures
// ---------------------------------------------------------------------------

describe("parseDtsFile — call signatures and construct signatures (basic.d.ts)", () => {
  let lines: ReviewLine[];

  beforeAll(() => {
    const result = parseDtsFile({
      filePath: path.join(FIXTURES, "basic.d.ts"),
      packageName: "@azure/storage-blob",
    });
    lines = collectAllLines(result.get(".")!.lines);
  });

  describe("call signature interface (Formatter)", () => {
    it("parses Formatter interface", () => {
      const iface = findLine(lines, "Formatter:interface");
      expect(iface).toBeDefined();
    });

    it("call signature child emits parameter list tokens", () => {
      const iface = findLine(lines, "Formatter:interface");
      const children = iface!.Children ?? [];
      const callSig = children.find((c) =>
        c.Tokens.some((t) => t.Value === "(") && !c.Tokens.some((t) => t.Value === "defaultFormat"),
      );
      expect(callSig).toBeDefined();
      const vals = callSig!.Tokens.map((t) => t.Value);
      expect(vals).toContain("(");
      expect(vals).toContain("value");
      expect(vals).toContain(")");
      expect(vals).toContain(":");
    });

    it("call signature child does NOT emit 'new' keyword", () => {
      const iface = findLine(lines, "Formatter:interface");
      const children = iface!.Children ?? [];
      const hasNew = children.some((c) => c.Tokens.some((t) => t.Value === "new"));
      expect(hasNew).toBe(false);
    });

    it("property member defaultFormat is also present", () => {
      const iface = findLine(lines, "Formatter:interface");
      const hasDefault = (iface!.Children ?? []).some((c) =>
        c.Tokens.some((t) => t.Value === "defaultFormat"),
      );
      expect(hasDefault).toBe(true);
    });
  });

  describe("construct signature interface (ClientConstructor)", () => {
    it("parses ClientConstructor interface", () => {
      const iface = findLine(lines, "ClientConstructor:interface");
      expect(iface).toBeDefined();
    });

    it("construct signature child emits 'new' keyword", () => {
      const iface = findLine(lines, "ClientConstructor:interface");
      const children = iface!.Children ?? [];
      const constructSig = children.find((c) => c.Tokens.some((t) => t.Value === "new"));
      expect(constructSig).toBeDefined();
    });

    it("construct signature child emits parameter and return type tokens", () => {
      const iface = findLine(lines, "ClientConstructor:interface");
      const children = iface!.Children ?? [];
      const constructSig = children.find((c) => c.Tokens.some((t) => t.Value === "new"));
      const vals = constructSig!.Tokens.map((t) => t.Value);
      expect(vals).toContain("(");
      expect(vals).toContain("endpoint");
      expect(vals).toContain(")");
    });
  });

  describe("generic factory interface (Factory<T>) with both call and construct", () => {
    it("parses Factory interface", () => {
      const iface = findLine(lines, "Factory:interface");
      expect(iface).toBeDefined();
    });

    it("has a construct signature child with 'new'", () => {
      const iface = findLine(lines, "Factory:interface");
      const children = iface!.Children ?? [];
      const constructSig = children.find((c) => c.Tokens.some((t) => t.Value === "new"));
      expect(constructSig).toBeDefined();
    });

    it("has a call signature child without 'new'", () => {
      const iface = findLine(lines, "Factory:interface");
      const children = iface!.Children ?? [];
      // call sig: has ( but not new as first token
      const callSig = children.find(
        (c) =>
          c.Tokens.some((t) => t.Value === "(") &&
          !c.Tokens.some((t) => t.Value === "new"),
      );
      expect(callSig).toBeDefined();
    });
  });
});

// ---------------------------------------------------------------------------
// buildSignatureBodyTokens — inline return type produces children correctly
// ---------------------------------------------------------------------------

describe("parseDtsFile — signature with inline object return type", () => {
  // Exercises the target-pointer update path in buildSignatureBodyTokens:
  // when the return type expands to child lines (inline type literal),
  // the semicolon must land on the last child line, not on tokens[].
  const FIXTURE_CONTENT = `
declare module "@azure/test" {
  export interface Builder {
    build(name: string): { id: string; value: number };
  }
}
`;

  let lines: ReviewLine[];
  const TEMP = path.join(path.dirname(FIXTURES), "tmp-sig-inline-return.d.ts");

  beforeAll(async () => {
    const { writeFileSync } = await import("node:fs");
    writeFileSync(TEMP, FIXTURE_CONTENT);
    const result = parseDtsFile({ filePath: TEMP, packageName: "@azure/test" });
    lines = collectAllLines(result.get("@azure/test")!.lines);
  });

  afterAll(async () => {
    const { unlinkSync } = await import("node:fs");
    unlinkSync(TEMP);
  });

  it("parses the Builder interface", () => {
    expect(findLine(lines, "Builder:interface")).toBeDefined();
  });

  it("build method has children for the inline return type", () => {
    const iface = findLine(lines, "Builder:interface");
    const buildMethod = (iface!.Children ?? []).find((c) =>
      c.Tokens.some((t) => t.Value === "build"),
    );
    expect(buildMethod).toBeDefined();
    expect(buildMethod!.Children?.length).toBeGreaterThan(0);
  });

  it("semicolon appears in the children (on the closing brace line), not duplicated on header", () => {
    const iface = findLine(lines, "Builder:interface");
    const buildMethod = (iface!.Children ?? []).find((c) =>
      c.Tokens.some((t) => t.Value === "build"),
    );
    const allChildren = collectAllLines(buildMethod!.Children ?? []);
    const lastChild = allChildren[allChildren.length - 1];
    expect(lastChild.Tokens.some((t) => t.Value === ";")).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Private class members excluded from output
// ---------------------------------------------------------------------------

describe("parseDtsFile — private class members excluded", () => {
  const FIXTURE_CONTENT = `
declare module "@azure/test" {
  export class MyClient {
    private _secret: string;
    protected region: string;
    public endpoint: string;
    private doInternalWork(): void;
    send(request: string): string;
  }
}
`;

  let lines: ReviewLine[];
  const TEMP = path.join(path.dirname(FIXTURES), "tmp-private.d.ts");

  beforeAll(async () => {
    const { writeFileSync } = await import("node:fs");
    writeFileSync(TEMP, FIXTURE_CONTENT);
    const result = parseDtsFile({ filePath: TEMP, packageName: "@azure/test" });
    lines = collectAllLines(result.get("@azure/test")!.lines);
  });

  afterAll(async () => {
    const { unlinkSync } = await import("node:fs");
    unlinkSync(TEMP);
  });

  it("private property is excluded", () => {
    const hasSecret = lines.some((l) => l.Tokens.some((t) => t.Value === "_secret"));
    expect(hasSecret).toBe(false);
  });

  it("private method is excluded", () => {
    const hasInternal = lines.some((l) => l.Tokens.some((t) => t.Value === "doInternalWork"));
    expect(hasInternal).toBe(false);
  });

  it("protected member is included", () => {
    const hasRegion = lines.some((l) => l.Tokens.some((t) => t.Value === "region"));
    expect(hasRegion).toBe(true);
  });

  it("public member is included", () => {
    const hasEndpoint = lines.some((l) => l.Tokens.some((t) => t.Value === "endpoint"));
    expect(hasEndpoint).toBe(true);
  });

  it("public method is included", () => {
    const hasSend = lines.some((l) => l.Tokens.some((t) => t.Value === "send"));
    expect(hasSend).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// declare module "./" normalises to "."
// ---------------------------------------------------------------------------

describe('parseDtsFile — declare module "./" normalized to "."', () => {
  const FIXTURE_CONTENT = `
declare module "./" {
  export interface RootExport { id: string; }
}
`;

  const TEMP = path.join(path.dirname(FIXTURES), "tmp-dotslash.d.ts");

  beforeAll(async () => {
    const { writeFileSync } = await import("node:fs");
    writeFileSync(TEMP, FIXTURE_CONTENT);
  });

  afterAll(async () => {
    const { unlinkSync } = await import("node:fs");
    unlinkSync(TEMP);
  });

  it('maps "./" to "." key in result', () => {
    const result = parseDtsFile({ filePath: TEMP, packageName: "@azure/test" });
    expect(result.has(".")).toBe(true);
    expect(result.has("./")).toBe(false);
  });

  it('RootExport is accessible under the "." key', () => {
    const result = parseDtsFile({ filePath: TEMP, packageName: "@azure/test" });
    const lines = collectAllLines(result.get(".")!.lines);
    expect(findLine(lines, "RootExport:interface")).toBeDefined();
  });

  it('"." and "./" merge into one entry when both present', () => {
    // Write a fixture with both declare module "." and declare module "./"
    const { writeFileSync } = require("node:fs");
    const TEMP2 = path.join(path.dirname(FIXTURES), "tmp-dotslash-merge.d.ts");
    writeFileSync(TEMP2, `
declare module "." {
  export interface A { x: string; }
}
declare module "./" {
  export interface B { y: number; }
}
`);
    const result = parseDtsFile({ filePath: TEMP2, packageName: "@azure/test" });
    require("node:fs").unlinkSync(TEMP2);
    expect(result.size).toBe(1);
    const lines = collectAllLines(result.get(".")!.lines);
    expect(findLine(lines, "A:interface")).toBeDefined();
    expect(findLine(lines, "B:interface")).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Regression: tuple type in heritage clause type arguments (Bug 1)
// ---------------------------------------------------------------------------

describe("parseDtsFile — tuple type in heritage clause type argument (basic.d.ts)", () => {
  let lines: ReviewLine[];

  beforeAll(() => {
    const result = parseDtsFile({ filePath: path.join(FIXTURES, "basic.d.ts"), packageName: "test-pkg" });
    lines = collectAllLines(result.get(".")!.lines);
  });

  it("PairIterable interface is present", () => {
    expect(findLine(lines, "PairIterable:interface")).toBeDefined();
  });

  it("PairIterable extends clause contains 'Iterable' TypeName token", () => {
    const iface = findLine(lines, "PairIterable:interface");
    expect(iface!.Tokens.some((t) => t.Value === "Iterable")).toBe(true);
  });

  it("PairIterable extends clause contains tuple element 'string'", () => {
    const iface = findLine(lines, "PairIterable:interface");
    // The tuple body '[string, number]' may land on the header tokens or in Children
    const allTokens = [
      ...iface!.Tokens,
      ...(iface!.Children ?? []).flatMap((c) => c.Tokens),
    ];
    expect(allTokens.some((t) => t.Value === "string")).toBe(true);
  });

  it("PairIterable extends clause contains tuple element 'number'", () => {
    const iface = findLine(lines, "PairIterable:interface");
    const allTokens = [
      ...iface!.Tokens,
      ...(iface!.Children ?? []).flatMap((c) => c.Tokens),
    ];
    expect(allTokens.some((t) => t.Value === "number")).toBe(true);
  });

  it("PairIterable extends clause closes with '>'", () => {
    const iface = findLine(lines, "PairIterable:interface");
    const allTokens = [
      ...iface!.Tokens,
      ...(iface!.Children ?? []).flatMap((c) => c.Tokens),
    ];
    expect(allTokens.some((t) => t.Value === ">")).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Regression: inline object type constraint in generic type parameter (Bug 2)
// ---------------------------------------------------------------------------

describe("parseDtsFile — inline object constraint in generic type parameter (basic.d.ts)", () => {
  let lines: ReviewLine[];

  beforeAll(() => {
    const result = parseDtsFile({ filePath: path.join(FIXTURES, "basic.d.ts"), packageName: "test-pkg" });
    lines = collectAllLines(result.get(".")!.lines);
  });

  it("IndexedCollection class is present", () => {
    expect(findLine(lines, "IndexedCollection:class")).toBeDefined();
  });

  it("IndexedCollection header contains type parameter 'T'", () => {
    const cls = findLine(lines, "IndexedCollection:class");
    expect(cls!.Tokens.some((t) => t.Value === "T")).toBe(true);
  });

  it("IndexedCollection header contains 'extends' keyword for constraint", () => {
    const cls = findLine(lines, "IndexedCollection:class");
    expect(cls!.Tokens.some((t) => t.Value === "extends")).toBe(true);
  });

  it("IndexedCollection constraint body contains property 'id'", () => {
    const cls = findLine(lines, "IndexedCollection:class");
    // Constraint body may expand into Children lines
    const allTokens = [
      ...cls!.Tokens,
      ...(cls!.Children ?? []).flatMap((c) => [...c.Tokens, ...(c.Children ?? []).flatMap((cc) => cc.Tokens)]),
    ];
    expect(allTokens.some((t) => t.Value === "id")).toBe(true);
  });

  it("IndexedCollection constraint body contains property 'name'", () => {
    const cls = findLine(lines, "IndexedCollection:class");
    const allTokens = [
      ...cls!.Tokens,
      ...(cls!.Children ?? []).flatMap((c) => [...c.Tokens, ...(c.Children ?? []).flatMap((cc) => cc.Tokens)]),
    ];
    expect(allTokens.some((t) => t.Value === "name")).toBe(true);
  });

  it("IndexedCollection constraint body closes with '}'", () => {
    const cls = findLine(lines, "IndexedCollection:class");
    const allTokens = [
      ...cls!.Tokens,
      ...(cls!.Children ?? []).flatMap((c) => [...c.Tokens, ...(c.Children ?? []).flatMap((cc) => cc.Tokens)]),
    ];
    expect(allTokens.some((t) => t.Value === "}")).toBe(true);
  });
});
