// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/**
 * Comprehensive tests against real-world .d.ts files from Azure/azure-sdk-for-js
 * PR #38143 (feat(dev-tool): add extract-api-v2 command).
 *
 * Fixtures: review-v2/browser/typespec-ts-http-runtime.d.ts
 *           (single `declare module "@typespec/ts-http-runtime"` block, ~1100 lines)
 *
 * These tests validate that the parser correctly handles production TypeScript
 * declaration files with rich constructs: generics, union types, intersection
 * types, heritage clauses, complex method signatures, JSDoc comments, and
 * cross-reference NavigateToId links.
 */

import path from "node:path";
import { describe, it, beforeAll, expect } from "vitest";
import { parseDtsFile, ParsedModule } from "../../src/dts/parser.js";
import { ReviewLine, TokenKind } from "../../src/models.js";

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

const FIXTURES = path.join(import.meta.dirname, "fixtures", "real-world");
const PACKAGE_NAME = "@typespec/ts-http-runtime";

function findLine(lines: ReviewLine[], idSuffix: string): ReviewLine | undefined {
  for (const l of lines) {
    if (l.LineId?.endsWith(idSuffix)) return l;
    if (l.Children?.length) {
      const r = findLine(l.Children, idSuffix);
      if (r) return r;
    }
  }
}

function collectAllLines(lines: ReviewLine[]): ReviewLine[] {
  const all: ReviewLine[] = [];
  for (const l of lines) {
    all.push(l);
    if (l.Children?.length) all.push(...collectAllLines(l.Children));
  }
  return all;
}

function tokenValues(line: ReviewLine): string[] {
  return line.Tokens.map((t) => t.Value ?? "").filter(Boolean);
}

function joinTokens(line: ReviewLine): string {
  return tokenValues(line).join(" ");
}

// ---------------------------------------------------------------------------
// @typespec/ts-http-runtime browser fixture
// ---------------------------------------------------------------------------

describe("real-world: ts-http-runtime-browser.d.ts", () => {
  let subpathMap: Map<string, ParsedModule>;
  let lines: ReviewLine[];

  beforeAll(() => {
    subpathMap = parseDtsFile({
      filePath: path.join(FIXTURES, "ts-http-runtime-browser.d.ts"),
      packageName: PACKAGE_NAME,
    });
    lines = subpathMap.get(PACKAGE_NAME)!.lines;
  });

  // ── Module detection ──────────────────────────────────────────────────────

  describe("module detection", () => {
    it("parses without throwing", () => {
      expect(() =>
        parseDtsFile({
          filePath: path.join(FIXTURES, "ts-http-runtime-browser.d.ts"),
          packageName: PACKAGE_NAME,
        }),
      ).not.toThrow();
    });

    it("produces exactly one entry point keyed by the module name", () => {
      expect(subpathMap.size).toBe(1);
      expect(subpathMap.has(PACKAGE_NAME)).toBe(true);
    });

    it("does NOT create a '.' entry point when a named module block exists", () => {
      expect(subpathMap.has(".")).toBe(false);
    });

    it("produces lines for the entry point", () => {
      expect(lines).toBeDefined();
      expect(lines.length).toBeGreaterThan(0);
    });
  });

  // ── Declaration counts ────────────────────────────────────────────────────

  describe("declaration counts", () => {
    let all: ReviewLine[];

    beforeAll(() => {
      all = collectAllLines(lines);
    });

    it("finds all 3 class declarations", () => {
      const classes = all.filter((l) => l.LineId?.endsWith(":class"));
      expect(classes.length).toBe(3);
    });

    it("finds all 57 interface declarations", () => {
      const ifaces = all.filter((l) => l.LineId?.endsWith(":interface"));
      expect(ifaces.length).toBe(57);
    });

    it("finds all 29 type alias declarations", () => {
      const types = all.filter((l) => l.LineId?.endsWith(":typealias"));
      expect(types.length).toBe(29);
    });

    it("finds all 35 function declarations", () => {
      const fns = all.filter((l) => l.LineId?.endsWith(":function"));
      expect(fns.length).toBe(35);
    });
  });

  // ── Class — AbortError ────────────────────────────────────────────────────

  describe("AbortError class", () => {
    let abortLine: ReviewLine;

    beforeAll(() => {
      abortLine = findLine(lines, "AbortError:class")!;
    });

    it("is present", () => {
      expect(abortLine).toBeDefined();
    });

    it("has correct LineId", () => {
      expect(abortLine.LineId).toBe(`${PACKAGE_NAME}!AbortError:class`);
    });

    it("emits export and class keywords", () => {
      const tokens = tokenValues(abortLine);
      expect(tokens).toContain("export");
      expect(tokens).toContain("class");
      expect(tokens).toContain("AbortError");
    });

    it("emits 'extends Error' heritage clause", () => {
      const tokens = tokenValues(abortLine);
      expect(tokens).toContain("extends");
      expect(tokens).toContain("Error");
    });

    it("sets RenderClasses=['class'] on the name token", () => {
      const nameToken = abortLine.Tokens.find((t) => t.Value === "AbortError");
      expect(nameToken?.RenderClasses).toContain("class");
    });

    it("sets NavigateToId on the name token", () => {
      const nameToken = abortLine.Tokens.find((t) => t.Value === "AbortError");
      expect(nameToken?.NavigateToId).toBeTruthy();
    });

    it("has 1 child (constructor)", () => {
      expect(abortLine.Children?.length).toBe(1);
    });

    it("constructor child emits 'constructor' keyword and optional string parameter", () => {
      const ctor = abortLine.Children![0];
      const tokens = tokenValues(ctor);
      expect(tokens).toContain("constructor");
      expect(tokens).toContain("message");
      expect(tokens).toContain("?");
      expect(tokens).toContain("string");
    });

    it("emits a context-end line with '}' after the class", () => {
      const all = collectAllLines(lines);
      const contextEnd = all.find(
        (l) => l.IsContextEndLine && l.RelatedToLine === abortLine.LineId,
      );
      expect(contextEnd).toBeDefined();
      expect(contextEnd?.Tokens.some((t) => t.Value === "}")).toBe(true);
    });
  });

  // ── Class — RestError ─────────────────────────────────────────────────────

  describe("RestError class", () => {
    let restLine: ReviewLine;

    beforeAll(() => {
      restLine = findLine(lines, "RestError:class")!;
    });

    it("is present", () => {
      expect(restLine).toBeDefined();
    });

    it("extends Error", () => {
      const tokens = tokenValues(restLine);
      expect(tokens).toContain("extends");
      expect(tokens).toContain("Error");
    });

    it("has 8 children (properties + constructor)", () => {
      expect(restLine.Children?.length).toBe(8);
    });

    it("includes readonly properties", () => {
      const readonlyChildren = restLine.Children!.filter((c) =>
        tokenValues(c).includes("readonly"),
      );
      expect(readonlyChildren.length).toBeGreaterThan(0);
    });

    it("includes optional properties with '?'", () => {
      const optionalChildren = restLine.Children!.filter((c) =>
        tokenValues(c).includes("?"),
      );
      expect(optionalChildren.length).toBeGreaterThan(0);
    });

    it("constructor child accepts message and options parameters", () => {
      const ctor = restLine.Children!.find((c) => tokenValues(c).includes("constructor"));
      expect(ctor).toBeDefined();
      const tokens = tokenValues(ctor!);
      expect(tokens).toContain("message");
      expect(tokens).toContain("string");
      expect(tokens).toContain("options");
    });

    it("has NavigateToId on PipelineRequest type reference", () => {
      const childWithPipeline = restLine.Children!.find((c) =>
        tokenValues(c).includes("PipelineRequest"),
      );
      expect(childWithPipeline).toBeDefined();
      const pipelineToken = childWithPipeline!.Tokens.find(
        (t) => t.Value === "PipelineRequest",
      );
      expect(pipelineToken?.NavigateToId).toBe(
        `${PACKAGE_NAME}!PipelineRequest:interface`,
      );
    });

    it("JSDoc comment is emitted before the class declaration", () => {
      const all = collectAllLines(lines);
      const restIdx = all.findIndex((l) => l.LineId === restLine.LineId);
      const docsBefore = all
        .slice(0, restIdx)
        .filter((l) => l.RelatedToLine === restLine.LineId);
      expect(docsBefore.length).toBeGreaterThan(0);
      const hasComment = docsBefore.some((l) =>
        l.Tokens.some((t) => t.Kind === TokenKind.Comment),
      );
      expect(hasComment).toBe(true);
    });
  });

  // ── Class — Sanitizer ────────────────────────────────────────────────────

  describe("Sanitizer class", () => {
    let sanitizer: ReviewLine;

    beforeAll(() => {
      sanitizer = findLine(lines, "Sanitizer:class")!;
    });

    it("is present", () => {
      expect(sanitizer).toBeDefined();
    });

    it("does NOT extend anything (no 'extends' token)", () => {
      expect(tokenValues(sanitizer)).not.toContain("extends");
    });

    it("has constructor, sanitize, and sanitizeUrl children", () => {
      const names = sanitizer.Children!.map((c) => tokenValues(c)[0]);
      expect(names).toContain("constructor");
      expect(names).toContain("sanitize");
      expect(names).toContain("sanitizeUrl");
    });

    it("sanitize method returns string", () => {
      const sanitizeChild = sanitizer.Children!.find((c) =>
        tokenValues(c).includes("sanitize"),
      );
      expect(sanitizeChild).toBeDefined();
      expect(tokenValues(sanitizeChild!)).toContain("string");
    });
  });

  // ── Interface — Pipeline ─────────────────────────────────────────────────

  describe("Pipeline interface", () => {
    let pipeline: ReviewLine;

    beforeAll(() => {
      pipeline = findLine(lines, "Pipeline:interface")!;
    });

    it("is present", () => {
      expect(pipeline).toBeDefined();
    });

    it("emits export and interface keywords", () => {
      const tokens = tokenValues(pipeline);
      expect(tokens).toContain("export");
      expect(tokens).toContain("interface");
      expect(tokens).toContain("Pipeline");
    });

    it("has 5 method children", () => {
      expect(pipeline.Children?.length).toBe(5);
    });

    it("sendRequest child has NavigateToId on HttpClient, PipelineRequest, PipelineResponse", () => {
      const sendRequest = pipeline.Children!.find((c) =>
        tokenValues(c).includes("sendRequest"),
      );
      expect(sendRequest).toBeDefined();
      const navTokens = sendRequest!.Tokens.filter((t) => t.NavigateToId);
      const navIds = navTokens.map((t) => t.NavigateToId!);
      expect(navIds).toContain(`${PACKAGE_NAME}!HttpClient:interface`);
      expect(navIds).toContain(`${PACKAGE_NAME}!PipelineRequest:interface`);
      expect(navIds).toContain(`${PACKAGE_NAME}!PipelineResponse:interface`);
    });

    it("clone method has NavigateToId on Pipeline (self-reference)", () => {
      const clone = pipeline.Children!.find((c) => tokenValues(c).includes("clone"));
      expect(clone).toBeDefined();
      const pipelineRef = clone!.Tokens.find((t) => t.NavigateToId?.endsWith("Pipeline:interface"));
      expect(pipelineRef).toBeDefined();
    });

    it("addPolicy child has NavigateToId on PipelinePolicy and AddPolicyOptions", () => {
      const addPolicy = pipeline.Children!.find((c) =>
        tokenValues(c).includes("addPolicy"),
      );
      expect(addPolicy).toBeDefined();
      const navIds = addPolicy!.Tokens.filter((t) => t.NavigateToId).map(
        (t) => t.NavigateToId!,
      );
      expect(navIds).toContain(`${PACKAGE_NAME}!PipelinePolicy:interface`);
      expect(navIds).toContain(`${PACKAGE_NAME}!AddPolicyOptions:interface`);
    });
  });

  // ── Interface — generic OAuth2TokenCredential ─────────────────────────────

  describe("OAuth2TokenCredential generic interface", () => {
    let oauth: ReviewLine;

    beforeAll(() => {
      oauth = findLine(lines, "OAuth2TokenCredential:interface")!;
    });

    it("is present", () => {
      expect(oauth).toBeDefined();
    });

    it("emits the type parameter TFlows", () => {
      expect(joinTokens(oauth)).toContain("TFlows");
    });

    it("emits 'extends OAuth2Flow' constraint on the type parameter", () => {
      const full = joinTokens(oauth);
      expect(full).toContain("extends");
      expect(full).toContain("OAuth2Flow");
    });

    it("has a getOAuth2Token method child", () => {
      const child = oauth.Children?.find((c) => tokenValues(c).includes("getOAuth2Token"));
      expect(child).toBeDefined();
    });
  });

  // ── Interface — DefaultRetryPolicyOptions extends PipelineRetryOptions ────

  describe("DefaultRetryPolicyOptions interface extends", () => {
    let dro: ReviewLine;

    beforeAll(() => {
      dro = findLine(lines, "DefaultRetryPolicyOptions:interface")!;
    });

    it("is present", () => {
      expect(dro).toBeDefined();
    });

    it("emits 'extends PipelineRetryOptions'", () => {
      const tokens = tokenValues(dro);
      expect(tokens).toContain("extends");
      expect(tokens).toContain("PipelineRetryOptions");
    });

    it("has NavigateToId on PipelineRetryOptions (extends target)", () => {
      const target = dro.Tokens.find((t) => t.Value === "PipelineRetryOptions");
      expect(target?.NavigateToId).toBe(
        `${PACKAGE_NAME}!PipelineRetryOptions:interface`,
      );
    });
  });

  // ── Type alias — union type ClientCredential ──────────────────────────────

  describe("ClientCredential union type alias", () => {
    let cc: ReviewLine;

    beforeAll(() => {
      cc = findLine(lines, "ClientCredential:typealias")!;
    });

    it("is present", () => {
      expect(cc).toBeDefined();
    });

    it("emits export, type keywords and name", () => {
      const tokens = tokenValues(cc);
      expect(tokens).toContain("export");
      expect(tokens).toContain("type");
      expect(tokens).toContain("ClientCredential");
    });

    it("contains all four union members", () => {
      const full = joinTokens(cc);
      expect(full).toContain("OAuth2TokenCredential");
      expect(full).toContain("BearerTokenCredential");
      expect(full).toContain("BasicCredential");
      expect(full).toContain("ApiKeyCredential");
    });

    it("uses '|' separators", () => {
      expect(tokenValues(cc)).toContain("|");
    });

    it("has NavigateToId on BearerTokenCredential", () => {
      const token = cc.Tokens.find((t) => t.Value === "BearerTokenCredential");
      expect(token?.NavigateToId).toBe(`${PACKAGE_NAME}!BearerTokenCredential:interface`);
    });
  });

  // ── Type alias — string literal union HttpMethods ─────────────────────────

  describe("HttpMethods string literal union", () => {
    let hm: ReviewLine;

    beforeAll(() => {
      hm = findLine(lines, "HttpMethods:typealias")!;
    });

    it("is present", () => {
      expect(hm).toBeDefined();
    });

    it("contains all 8 HTTP methods", () => {
      const full = joinTokens(hm);
      for (const method of ["GET", "PUT", "POST", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE"]) {
        expect(full).toContain(`"${method}"`);
      }
    });
  });

  // ── Type alias — never (NodeReadableStream / NodeBuffer in browser) ────────

  describe("NodeReadableStream type alias", () => {
    it("is present", () => {
      const nrs = findLine(lines, "NodeReadableStream:typealias");
      expect(nrs).toBeDefined();
    });

    it("emits export and type keywords", () => {
      const nrs = findLine(lines, "NodeReadableStream:typealias")!;
      const tokens = tokenValues(nrs);
      expect(tokens).toContain("export");
      expect(tokens).toContain("type");
      expect(tokens).toContain("NodeReadableStream");
    });
  });

  // ── Type alias — WebReadableStream<R = any> with default type parameter ───

  describe("WebReadableStream generic type alias", () => {
    it("is present", () => {
      const wrs = findLine(lines, "WebReadableStream:typealias");
      expect(wrs).toBeDefined();
    });

    it("contains 'R' type parameter", () => {
      const wrs = findLine(lines, "WebReadableStream:typealias")!;
      expect(joinTokens(wrs)).toContain("R");
    });
  });

  // ── Function declarations ─────────────────────────────────────────────────

  describe("getClient function", () => {
    let gc: ReviewLine;

    beforeAll(() => {
      gc = findLine(lines, "getClient:function")!;
    });

    it("is present", () => {
      expect(gc).toBeDefined();
    });

    it("emits export and function keywords", () => {
      const tokens = tokenValues(gc);
      expect(tokens).toContain("export");
      expect(tokens).toContain("function");
      expect(tokens).toContain("getClient");
    });

    it("has 'endpoint: string' parameter", () => {
      const full = joinTokens(gc);
      expect(full).toContain("endpoint");
      expect(full).toContain("string");
    });

    it("has optional clientOptions parameter of type ClientOptions", () => {
      const tokens = tokenValues(gc);
      expect(tokens).toContain("clientOptions");
      expect(tokens).toContain("ClientOptions");
      // optional marker
      const optIdx = tokens.indexOf("clientOptions") + 1;
      expect(tokens[optIdx]).toBe("?");
    });

    it("returns Client (has NavigateToId on return type)", () => {
      const clientToken = gc.Tokens.find(
        (t) => t.Value === "Client" && t.NavigateToId?.endsWith("Client:interface"),
      );
      expect(clientToken).toBeDefined();
    });

    it("ends with semicolon", () => {
      const tokens = tokenValues(gc);
      expect(tokens[tokens.length - 1]).toBe(";");
    });

    it("has NavigationDisplayName set", () => {
      const nameToken = gc.Tokens.find((t) => t.Value === "getClient");
      expect(nameToken?.NavigationDisplayName).toBe("getClient");
    });
  });

  describe("isRestError function", () => {
    it("is present", () => {
      expect(findLine(lines, "isRestError:function")).toBeDefined();
    });

    it("has 'e: unknown' parameter", () => {
      const fn = findLine(lines, "isRestError:function")!;
      const full = joinTokens(fn);
      expect(full).toContain("e");
      expect(full).toContain("unknown");
    });
  });

  describe("createPipelineRequest function", () => {
    it("is present", () => {
      expect(findLine(lines, "createPipelineRequest:function")).toBeDefined();
    });

    it("accepts PipelineRequestOptions parameter", () => {
      const fn = findLine(lines, "createPipelineRequest:function")!;
      expect(joinTokens(fn)).toContain("PipelineRequestOptions");
    });

    it("returns PipelineRequest", () => {
      const fn = findLine(lines, "createPipelineRequest:function")!;
      const pipelineToken = fn.Tokens.find(
        (t) => t.Value === "PipelineRequest" && t.NavigateToId?.endsWith("PipelineRequest:interface"),
      );
      expect(pipelineToken).toBeDefined();
    });
  });

  describe("computeSha256Hmac function (if present)", () => {
    it("has key, stringToSign, and encoding parameters", () => {
      const fn = findLine(lines, "computeSha256Hmac:function");
      // Only in require variant, may not be present in browser
      if (!fn) return;
      const full = joinTokens(fn);
      expect(full).toContain("key");
      expect(full).toContain("stringToSign");
      expect(full).toContain("encoding");
    });
  });

  // ── JSDoc documentation ───────────────────────────────────────────────────

  describe("JSDoc documentation extraction", () => {
    it("emits at least 100 comment lines total", () => {
      const all = collectAllLines(lines);
      const commentLines = all.filter((l) =>
        l.Tokens.some((t) => t.Kind === TokenKind.Comment),
      );
      expect(commentLines.length).toBeGreaterThanOrEqual(100);
    });

    it("JSDoc comment for AbortError uses TokenKind.Comment", () => {
      const all = collectAllLines(lines);
      const abortId = findLine(lines, "AbortError:class")!.LineId!;
      const abortIdx = all.findIndex((l) => l.LineId === abortId);
      const commentsBefore = all
        .slice(0, abortIdx)
        .filter((l) => l.RelatedToLine === abortId);
      expect(commentsBefore.length).toBeGreaterThan(0);
      const hasComment = commentsBefore.some((l) =>
        l.Tokens.some((t) => t.Kind === TokenKind.Comment),
      );
      expect(hasComment).toBe(true);
    });

    it("JSDoc comment for AbortError contains expected text", () => {
      const all = collectAllLines(lines);
      const abortId = findLine(lines, "AbortError:class")!.LineId!;
      const abortIdx = all.findIndex((l) => l.LineId === abortId);
      const commentText = all
        .slice(0, abortIdx)
        .filter((l) => l.RelatedToLine === abortId && l.Tokens.some((t) => t.Kind === TokenKind.Comment))
        .map((l) => l.Tokens[0].Value ?? "")
        .join(" ");
      expect(commentText).toContain("asynchronous operation");
    });

    it("JSDoc comment for RestError contains 'custom error' text", () => {
      const all = collectAllLines(lines);
      const restId = findLine(lines, "RestError:class")!.LineId!;
      const restIdx = all.findIndex((l) => l.LineId === restId);
      const commentText = all
        .slice(0, restIdx)
        .filter((l) => l.RelatedToLine === restId && l.Tokens.some((t) => t.Kind === TokenKind.Comment))
        .map((l) => l.Tokens[0].Value ?? "")
        .join(" ");
      expect(commentText).toContain("custom error");
    });

    it("JSDoc comment for getClient is present", () => {
      const all = collectAllLines(lines);
      const gcId = findLine(lines, "getClient:function")!.LineId!;
      const gcIdx = all.findIndex((l) => l.LineId === gcId);
      const comments = all
        .slice(0, gcIdx)
        .filter((l) => l.RelatedToLine === gcId && l.Tokens.some((t) => t.Kind === TokenKind.Comment));
      expect(comments.length).toBeGreaterThan(0);
    });
  });

  // ── NavigateToId cross-references ─────────────────────────────────────────

  describe("NavigateToId cross-references", () => {
    it("all NavigateToId values reference valid LineIds in the entry point", () => {
      const all = collectAllLines(lines);
      const allLineIds = new Set(all.map((l) => l.LineId).filter(Boolean));

      const badNavs: string[] = [];
      for (const l of all) {
        for (const t of l.Tokens) {
          if (t.NavigateToId && !allLineIds.has(t.NavigateToId)) {
            badNavs.push(`${t.Value} → ${t.NavigateToId}`);
          }
        }
      }
      expect(badNavs).toHaveLength(0);
    });

    it("OAuth2Flow NavigateToId resolves to the typealias line", () => {
      const all = collectAllLines(lines);
      const oauth2flowId = `${PACKAGE_NAME}!OAuth2Flow:typealias`;
      const token = all
        .flatMap((l) => l.Tokens)
        .find((t) => t.NavigateToId === oauth2flowId);
      expect(token).toBeDefined();
    });

    it("PipelinePolicy NavigateToId resolves correctly", () => {
      const all = collectAllLines(lines);
      const policyId = `${PACKAGE_NAME}!PipelinePolicy:interface`;
      const token = all
        .flatMap((l) => l.Tokens)
        .find((t) => t.NavigateToId === policyId);
      expect(token).toBeDefined();
    });
  });

  // ── Empty lines between declarations ────────────────────────────────────

  describe("blank lines between declarations", () => {
    it("emits an empty line after AbortError (after context-end)", () => {
      const all = collectAllLines(lines);
      const abortId = findLine(lines, "AbortError:class")!.LineId!;
      const contextEnd = all.find((l) => l.IsContextEndLine && l.RelatedToLine === abortId);
      expect(contextEnd).toBeDefined();
      const ctxIdx = all.indexOf(contextEnd!);
      const emptyAfter = all[ctxIdx + 1];
      expect(emptyAfter?.Tokens.length).toBe(0);
    });

    it("emits an empty line after a function declaration", () => {
      const all = collectAllLines(lines);
      const gcLine = findLine(lines, "getClient:function")!;
      const gcIdx = all.indexOf(gcLine);
      const emptyAfter = all[gcIdx + 1];
      expect(emptyAfter?.Tokens.length).toBe(0);
    });
  });

  // ── No release tags (all stable) ──────────────────────────────────────────

  describe("no release tag markers", () => {
    it("emits no @beta marker lines", () => {
      const all = collectAllLines(lines);
      const betaLines = all.filter((l) => l.Tokens.some((t) => t.Value === "@beta"));
      expect(betaLines).toHaveLength(0);
    });

    it("emits no @alpha marker lines", () => {
      const all = collectAllLines(lines);
      const alphaLines = all.filter((l) => l.Tokens.some((t) => t.Value === "@alpha"));
      expect(alphaLines).toHaveLength(0);
    });
  });

  // ── LineId format ──────────────────────────────────────────────────────────

  describe("LineId format", () => {
    it("all LineIds are prefixed with @typespec/ts-http-runtime!", () => {
      const all = collectAllLines(lines);
      const badIds = all
        .filter((l) => l.LineId)
        .filter((l) => !l.LineId!.startsWith(`${PACKAGE_NAME}!`));
      expect(badIds).toHaveLength(0);
    });

    it("AbortError LineId follows the @pkg!Name:kind pattern", () => {
      const abort = findLine(lines, "AbortError:class")!;
      expect(abort.LineId).toBe(`${PACKAGE_NAME}!AbortError:class`);
    });

    it("getClient LineId follows the @pkg!Name:kind pattern", () => {
      const gc = findLine(lines, "getClient:function")!;
      expect(gc.LineId).toBe(`${PACKAGE_NAME}!getClient:function`);
    });
  });
});

// ---------------------------------------------------------------------------
// @typespec/ts-http-runtime require fixture (Node.js variant)
// ---------------------------------------------------------------------------

describe("real-world: ts-http-runtime-require.d.ts (Node.js variant)", () => {
  let subpathMap: Map<string, ParsedModule>;
  let lines: ReviewLine[];

  beforeAll(() => {
    subpathMap = parseDtsFile({
      filePath: path.join(FIXTURES, "ts-http-runtime-require.d.ts"),
      packageName: PACKAGE_NAME,
    });
    lines = subpathMap.get(PACKAGE_NAME)!.lines;
  });

  it("parses without throwing", () => {
    expect(lines).toBeDefined();
    expect(lines.length).toBeGreaterThan(0);
  });

  it("produces one entry point for the module name", () => {
    expect(subpathMap.size).toBe(1);
    expect(subpathMap.has(PACKAGE_NAME)).toBe(true);
  });

  it("finds AbortError class", () => {
    expect(findLine(lines, "AbortError:class")).toBeDefined();
  });

  it("finds all expected function declarations", () => {
    const all = collectAllLines(lines);
    const fns = all.filter((l) => l.LineId?.endsWith(":function"));
    expect(fns.length).toBeGreaterThanOrEqual(35);
  });

  it("has NavigateToId cross-references consistent with browser fixture", () => {
    const all = collectAllLines(lines);
    const pipelineChild = all.find(
      (l) =>
        l.Tokens.some((t) => t.Value === "PipelineRequest" && t.NavigateToId?.endsWith("PipelineRequest:interface")),
    );
    expect(pipelineChild).toBeDefined();
  });
});
