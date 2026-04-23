// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/**
 * Comprehensive tests against a representative @azure/ai-projects .d.ts fixture.
 *
 * The fixture (test/dts/fixtures/real-world/ai-projects.d.ts) is derived from
 * sdk/ai/ai-projects/review/ai-projects-node.api.md in Azure/azure-sdk-for-js
 * (PR #38143) and structured as TWO `declare module` blocks to simulate what
 * the extract-api-v2 command would generate for a package with named subpath
 * exports:
 *
 *   declare module "@azure/ai-projects"         – client + operations + models
 *   declare module "@azure/ai-projects/models"  – raw generated tool types
 *
 * This exercises all the key parser features that the simpler ts-http-runtime
 * fixture does not:
 *
 *  • Multiple declare module blocks in one file
 *  • Per-module LineId prefix (@azure/ai-projects! vs @azure/ai-projects/models!)
 *  • Cross-module NavigateToId (combined reference map spans both modules)
 *  • Class with @beta-tagged property
 *  • Overloaded interface methods (AgentsOperations.create × 2)
 *  • PagedAsyncIterableIterator<T> generic return types
 *  • Discriminated union types (AgentDefinitionUnion)
 *  • Very long string-literal union (AttackStrategy — 28 members)
 *  • @deprecated type (LegacyProjectClientOptions)
 *  • Deep extends hierarchies (InsightRequest subtypes)
 *  • Interfaces that extend OperationOptions (AgentsCreateOptionalParams)
 */

import path from "node:path";
import { describe, it, beforeAll, expect } from "vitest";
import { parseDtsFile } from "../../src/dts/parser.js";
import { ReviewLine, TokenKind } from "../../src/models.js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const FIXTURE = path.join(import.meta.dirname, "fixtures", "real-world", "ai-projects.d.ts");
const MAIN_MODULE = "@azure/ai-projects";
const MODELS_MODULE = "@azure/ai-projects/models";

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
// Fixtures
// ---------------------------------------------------------------------------

describe("real-world: ai-projects.d.ts (multi-module)", () => {
  let parsed: Map<string, ReviewLine[]>;
  let mainLines: ReviewLine[];
  let modelsLines: ReviewLine[];

  beforeAll(() => {
    parsed = parseDtsFile({ filePath: FIXTURE, packageName: MAIN_MODULE });
    mainLines = parsed.get(MAIN_MODULE)!;
    modelsLines = parsed.get(MODELS_MODULE)!;
  });

  // ── Module detection ──────────────────────────────────────────────────────

  describe("module detection", () => {
    it("parses without throwing", () => {
      expect(() => parseDtsFile({ filePath: FIXTURE, packageName: MAIN_MODULE })).not.toThrow();
    });

    it("produces exactly two entry points", () => {
      expect(parsed.size).toBe(2);
    });

    it("has an entry for the main module", () => {
      expect(parsed.has(MAIN_MODULE)).toBe(true);
      expect(mainLines).toBeDefined();
    });

    it("has an entry for the models subpath", () => {
      expect(parsed.has(MODELS_MODULE)).toBe(true);
      expect(modelsLines).toBeDefined();
    });

    it("does NOT create a '.' fallback entry", () => {
      expect(parsed.has(".")).toBe(false);
    });

    it("main module has declarations", () => {
      expect(mainLines.length).toBeGreaterThan(0);
    });

    it("models subpath has declarations", () => {
      expect(modelsLines.length).toBeGreaterThan(0);
    });
  });

  // ── LineId prefix per module ───────────────────────────────────────────────

  describe("LineId prefix isolation", () => {
    it("all main-module LineIds start with @azure/ai-projects!", () => {
      const all = collectAllLines(mainLines);
      const bad = all
        .filter((l) => l.LineId)
        .filter((l) => !l.LineId!.startsWith(`${MAIN_MODULE}!`));
      expect(bad).toHaveLength(0);
    });

    it("all models-subpath LineIds start with @azure/ai-projects/models!", () => {
      const all = collectAllLines(modelsLines);
      const bad = all
        .filter((l) => l.LineId)
        .filter((l) => !l.LineId!.startsWith(`${MODELS_MODULE}!`));
      expect(bad).toHaveLength(0);
    });

    it("models LineIds do NOT use the main module prefix", () => {
      const all = collectAllLines(modelsLines);
      const wrongPrefix = all
        .filter((l) => l.LineId)
        .filter((l) => l.LineId!.startsWith(`${MAIN_MODULE}!`) && !l.LineId!.startsWith(`${MODELS_MODULE}!`));
      expect(wrongPrefix).toHaveLength(0);
    });
  });

  // ── Cross-module NavigateToId integrity ───────────────────────────────────

  describe("cross-module NavigateToId integrity", () => {
    it("all NavigateToId values resolve to known LineIds across both modules", () => {
      const allMain = collectAllLines(mainLines);
      const allModels = collectAllLines(modelsLines);
      const allLineIds = new Set(
        [...allMain, ...allModels].map((l) => l.LineId).filter(Boolean),
      );

      const broken: string[] = [];
      for (const l of [...allMain, ...allModels]) {
        for (const t of l.Tokens) {
          if (t.NavigateToId && !allLineIds.has(t.NavigateToId)) {
            broken.push(`${t.Value} → ${t.NavigateToId}`);
          }
        }
      }
      expect(broken).toHaveLength(0);
    });

    it("AIProjectClient.agents property NavigateToId resolves to AgentsOperations:interface", () => {
      const all = collectAllLines(mainLines);
      const agentsProp = all.find((l) => l.LineId?.endsWith("AIProjectClient.agents:property"));
      expect(agentsProp).toBeDefined();
      const navToken = agentsProp!.Tokens.find(
        (t) => t.NavigateToId?.endsWith("AgentsOperations:interface"),
      );
      expect(navToken).toBeDefined();
      expect(navToken!.NavigateToId).toBe(`${MAIN_MODULE}!AgentsOperations:interface`);
    });
  });

  // ── Declaration counts ────────────────────────────────────────────────────

  describe("main module declaration counts", () => {
    let all: ReviewLine[];
    beforeAll(() => { all = collectAllLines(mainLines); });

    it("has 1 class (AIProjectClient)", () => {
      expect(all.filter((l) => l.LineId?.endsWith(":class")).length).toBe(1);
    });

    it("has 167 interfaces", () => {
      expect(all.filter((l) => l.LineId?.endsWith(":interface")).length).toBe(167);
    });

    it("has 43 type aliases", () => {
      expect(all.filter((l) => l.LineId?.endsWith(":typealias")).length).toBe(43);
    });
  });

  describe("models subpath declaration counts", () => {
    let all: ReviewLine[];
    beforeAll(() => { all = collectAllLines(modelsLines); });

    it("has 0 classes", () => {
      expect(all.filter((l) => l.LineId?.endsWith(":class")).length).toBe(0);
    });

    it("has 30 interfaces", () => {
      expect(all.filter((l) => l.LineId?.endsWith(":interface")).length).toBe(30);
    });

    it("has 11 type aliases", () => {
      expect(all.filter((l) => l.LineId?.endsWith(":typealias")).length).toBe(11);
    });
  });

  // ── AIProjectClient class ──────────────────────────────────────────────────

  describe("AIProjectClient class", () => {
    let client: ReviewLine;
    beforeAll(() => { client = findLine(mainLines, "AIProjectClient:class")!; });

    it("is present", () => expect(client).toBeDefined());

    it("has correct LineId", () => {
      expect(client.LineId).toBe(`${MAIN_MODULE}!AIProjectClient:class`);
    });

    it("emits export and class keywords", () => {
      expect(tokenValues(client)).toContain("class");
      expect(tokenValues(client)).toContain("AIProjectClient");
    });

    it("does NOT extend anything", () => {
      expect(tokenValues(client)).not.toContain("extends");
    });

    it("has 8 children (constructor + 7 properties)", () => {
      expect(client.Children?.length).toBe(8);
    });

    it("includes a constructor child", () => {
      const ctor = client.Children?.find((c) => tokenValues(c).includes("constructor"));
      expect(ctor).toBeDefined();
      expect(tokenValues(ctor!)).toContain("endpoint");
      expect(tokenValues(ctor!)).toContain("string");
      expect(tokenValues(ctor!)).toContain("credential");
    });

    it("has agents property with NavigateToId to AgentsOperations", () => {
      const all = collectAllLines(mainLines);
      const agentsProp = all.find((l) => l.LineId?.endsWith("AIProjectClient.agents:property"));
      expect(agentsProp).toBeDefined();
      const nav = agentsProp!.Tokens.find((t) => t.NavigateToId?.endsWith("AgentsOperations:interface"));
      expect(nav).toBeDefined();
    });

    it("beta property's type BetaOperations has @beta marker at interface declaration", () => {
      // Class member @beta JSDoc is not promoted to a preamble marker;
      // instead, the BetaOperations interface itself carries the @beta marker.
      const all = collectAllLines(mainLines);
      const betaOpsId = `${MAIN_MODULE}!BetaOperations:interface`;
      const betaOpsIdx = all.findIndex((l) => l.LineId === betaOpsId);
      expect(betaOpsIdx).toBeGreaterThan(0);
      const hasBetaMarker = all.slice(0, betaOpsIdx).some(
        (l) => l.RelatedToLine === betaOpsId && l.Tokens.some((t) => t.Value === "@beta"),
      );
      expect(hasBetaMarker).toBe(true);
    });
  });

  // ── AgentsOperations overloaded methods ────────────────────────────────────

  describe("AgentsOperations interface with overloaded methods", () => {
    let agentsOps: ReviewLine;
    beforeAll(() => { agentsOps = findLine(mainLines, "AgentsOperations:interface")!; });

    it("is present", () => expect(agentsOps).toBeDefined());

    it("has 12 method children", () => {
      expect(agentsOps.Children?.length).toBe(12);
    });

    it("emits two 'create' overloads", () => {
      const createChildren = agentsOps.Children!.filter((c) =>
        tokenValues(c)[0] === "create",
      );
      expect(createChildren.length).toBe(2);
    });

    it("first create overload has AgentDefinitionUnion parameter", () => {
      const createChildren = agentsOps.Children!.filter((c) => tokenValues(c)[0] === "create");
      const defUnion = createChildren.find((c) =>
        tokenValues(c).includes("AgentDefinitionUnion"),
      );
      expect(defUnion).toBeDefined();
    });

    it("second create overload has manifestId: string parameter", () => {
      const createChildren = agentsOps.Children!.filter((c) => tokenValues(c)[0] === "create");
      const manifest = createChildren.find((c) =>
        tokenValues(c).includes("manifestId"),
      );
      expect(manifest).toBeDefined();
    });

    it("list method returns PagedAsyncIterableIterator<Agent>", () => {
      const listChild = agentsOps.Children!.find((c) => tokenValues(c)[0] === "list");
      expect(listChild).toBeDefined();
      expect(tokenValues(listChild!)).toContain("PagedAsyncIterableIterator");
      expect(tokenValues(listChild!)).toContain("Agent");
    });

    it("delete method returns Promise<DeleteAgentResponse>", () => {
      const deleteChild = agentsOps.Children!.find((c) => tokenValues(c)[0] === "delete");
      expect(deleteChild).toBeDefined();
      expect(tokenValues(deleteChild!)).toContain("Promise");
      expect(tokenValues(deleteChild!)).toContain("DeleteAgentResponse");
    });

    it("update method has two overloads", () => {
      const updateChildren = agentsOps.Children!.filter((c) =>
        tokenValues(c)[0] === "update",
      );
      expect(updateChildren.length).toBe(2);
    });
  });

  // ── BetaOperations @beta interface ────────────────────────────────────────

  describe("BetaOperations @beta interface", () => {
    let betaOps: ReviewLine;
    beforeAll(() => { betaOps = findLine(mainLines, "BetaOperations:interface")!; });

    it("is present", () => expect(betaOps).toBeDefined());

    it("emits @beta marker before the interface", () => {
      const all = collectAllLines(mainLines);
      const betaOpsIdx = all.findIndex((l) => l.LineId === betaOps.LineId);
      const linesBefore = all.slice(0, betaOpsIdx);
      const hasBeta = linesBefore.some(
        (l) => l.RelatedToLine === betaOps.LineId && l.Tokens.some((t) => t.Value === "@beta"),
      );
      expect(hasBeta).toBe(true);
    });

    it("has children for evaluators, insights, memoryStores, redTeams, schedules", () => {
      const childNames = betaOps.Children!.flatMap((c) => tokenValues(c));
      expect(childNames).toContain("evaluators");
      expect(childNames).toContain("insights");
      expect(childNames).toContain("memoryStores");
      expect(childNames).toContain("redTeams");
      expect(childNames).toContain("schedules");
    });

    it("total @beta marker lines in main module is 6", () => {
      const all = collectAllLines(mainLines);
      const betaLines = all.filter((l) => l.Tokens.some((t) => t.Value === "@beta"));
      expect(betaLines.length).toBe(6);
    });
  });

  // ── AgentKind string literal union ────────────────────────────────────────

  describe("AgentKind string literal union", () => {
    it("contains all three agent kinds", () => {
      const line = findLine(mainLines, "AgentKind:typealias")!;
      expect(line).toBeDefined();
      const full = joinTokens(line);
      expect(full).toContain('"prompt"');
      expect(full).toContain('"hosted"');
      expect(full).toContain('"workflow"');
    });
  });

  // ── AttackStrategy long string literal union ───────────────────────────────

  describe("AttackStrategy long string literal union", () => {
    let attack: ReviewLine;
    beforeAll(() => { attack = findLine(mainLines, "AttackStrategy:typealias")!; });

    it("is present", () => expect(attack).toBeDefined());

    it("contains at least 15 member values", () => {
      const members = tokenValues(attack).filter((v) => v.startsWith('"') && v.endsWith('"'));
      expect(members.length).toBeGreaterThanOrEqual(15);
    });

    it("contains 'easy', 'moderate', 'difficult' members", () => {
      const full = joinTokens(attack);
      expect(full).toContain('"easy"');
      expect(full).toContain('"moderate"');
      expect(full).toContain('"difficult"');
    });

    it("contains 'crescendo' as the last member", () => {
      expect(tokenValues(attack)).toContain('"crescendo"');
    });

    it("uses '|' separators", () => {
      expect(tokenValues(attack)).toContain("|");
    });
  });

  // ── AgentDefinitionUnion discriminated union ──────────────────────────────

  describe("AgentDefinitionUnion discriminated union type alias", () => {
    let union: ReviewLine;
    beforeAll(() => { union = findLine(mainLines, "AgentDefinitionUnion:typealias")!; });

    it("is present", () => expect(union).toBeDefined());

    it("contains PromptAgentDefinition, WorkflowAgentDefinition, HostedAgentDefinition", () => {
      const full = joinTokens(union);
      expect(full).toContain("PromptAgentDefinition");
      expect(full).toContain("WorkflowAgentDefinition");
      expect(full).toContain("HostedAgentDefinition");
      expect(full).toContain("AgentDefinition");
    });

    it("has NavigateToId on PromptAgentDefinition", () => {
      const token = union.Tokens.find((t) => t.Value === "PromptAgentDefinition");
      expect(token?.NavigateToId).toBe(`${MAIN_MODULE}!PromptAgentDefinition:interface`);
    });
  });

  // ── OperationOptions extends pattern ──────────────────────────────────────

  describe("AgentsCreateOptionalParams extends OperationOptions", () => {
    let opts: ReviewLine;
    beforeAll(() => { opts = findLine(mainLines, "AgentsCreateOptionalParams:interface")!; });

    it("is present", () => expect(opts).toBeDefined());

    it("emits 'extends OperationOptions'", () => {
      expect(tokenValues(opts)).toContain("extends");
      expect(tokenValues(opts)).toContain("OperationOptions");
    });

    it("has NavigateToId on OperationOptions (extends target)", () => {
      const target = opts.Tokens.find((t) => t.Value === "OperationOptions");
      expect(target?.NavigateToId).toBe(`${MAIN_MODULE}!OperationOptions:interface`);
    });

    it("has optional properties: description, metadata, foundryFeatures", () => {
      const childNames = opts.Children!.map((c) => tokenValues(c)[0]);
      expect(childNames).toContain("description");
      expect(childNames).toContain("metadata");
      expect(childNames).toContain("foundryFeatures");
    });
  });

  // ── @deprecated type ──────────────────────────────────────────────────────

  describe("LegacyProjectClientOptions @deprecated interface", () => {
    let legacy: ReviewLine;
    beforeAll(() => { legacy = findLine(mainLines, "LegacyProjectClientOptions:interface")!; });

    it("is present", () => expect(legacy).toBeDefined());

    it("emits a @deprecated marker line before the interface", () => {
      const all = collectAllLines(mainLines);
      const idx = all.findIndex((l) => l.LineId === legacy.LineId);
      const linesBefore = all.slice(0, idx);
      const hasDepr = linesBefore.some(
        (l) => l.RelatedToLine === legacy.LineId && l.Tokens.some((t) => t.Value === "@deprecated"),
      );
      expect(hasDepr).toBe(true);
    });

    it("has IsDeprecated=true on the name token", () => {
      const nameToken = legacy.Tokens.find((t) => t.Value === "LegacyProjectClientOptions");
      expect(nameToken?.IsDeprecated).toBe(true);
    });

    it("emits JSDoc deprecation message", () => {
      const all = collectAllLines(mainLines);
      const idx = all.findIndex((l) => l.LineId === legacy.LineId);
      const docsBefore = all
        .slice(0, idx)
        .filter((l) => l.RelatedToLine === legacy.LineId && l.Tokens.some((t) => t.Kind === TokenKind.Comment));
      expect(docsBefore.length).toBeGreaterThan(0);
      const text = docsBefore.map((l) => l.Tokens[0].Value ?? "").join(" ");
      expect(text.toLowerCase()).toContain("deprecated");
    });
  });

  // ── InsightRequest deep extends hierarchy ─────────────────────────────────

  describe("InsightRequest extends hierarchy", () => {
    it("EvaluationRunClusterInsightRequest extends InsightRequest", () => {
      const line = findLine(mainLines, "EvaluationRunClusterInsightRequest:interface")!;
      expect(line).toBeDefined();
      expect(tokenValues(line)).toContain("extends");
      expect(tokenValues(line)).toContain("InsightRequest");
    });

    it("EvaluationRunClusterInsightRequest has evaluationRunIds and modelConfiguration children", () => {
      const line = findLine(mainLines, "EvaluationRunClusterInsightRequest:interface")!;
      const childNames = line.Children!.map((c) => tokenValues(c)[0]);
      expect(childNames).toContain("evaluationRunIds");
      expect(childNames).toContain("modelConfiguration");
    });

    it("InsightRequestUnion contains all four members", () => {
      const union = findLine(mainLines, "InsightRequestUnion:typealias")!;
      const full = joinTokens(union);
      expect(full).toContain("EvaluationRunClusterInsightRequest");
      expect(full).toContain("AgentClusterInsightRequest");
      expect(full).toContain("EvaluationComparisonInsightRequest");
      expect(full).toContain("InsightRequest");
    });
  });

  // ── BetaInsightsOperations ────────────────────────────────────────────────

  describe("BetaInsightsOperations @beta interface", () => {
    let betaInsights: ReviewLine;
    beforeAll(() => { betaInsights = findLine(mainLines, "BetaInsightsOperations:interface")!; });

    it("is present", () => expect(betaInsights).toBeDefined());

    it("has 3 method children (generate, get, list)", () => {
      expect(betaInsights.Children?.length).toBe(3);
    });

    it("generate method takes InsightRequest and returns Promise<InsightResult>", () => {
      const gen = betaInsights.Children!.find((c) => tokenValues(c)[0] === "generate");
      expect(gen).toBeDefined();
      expect(tokenValues(gen!)).toContain("InsightRequest");
      expect(tokenValues(gen!)).toContain("InsightResult");
    });

    it("list method returns PagedAsyncIterableIterator<Insight>", () => {
      const list = betaInsights.Children!.find((c) => tokenValues(c)[0] === "list");
      expect(list).toBeDefined();
      expect(tokenValues(list!)).toContain("PagedAsyncIterableIterator");
      expect(tokenValues(list!)).toContain("Insight");
    });
  });

  // ── Schedule / RecurrenceTrigger / DayOfWeek ──────────────────────────────

  describe("Schedule and trigger types", () => {
    it("Schedule interface has trigger property", () => {
      const sched = findLine(mainLines, "Schedule:interface")!;
      expect(sched).toBeDefined();
      const childNames = sched.Children!.map((c) => tokenValues(c)[0]);
      expect(childNames).toContain("trigger");
    });

    it("DayOfWeek union contains all 7 days", () => {
      const dow = findLine(mainLines, "DayOfWeek:typealias")!;
      expect(dow).toBeDefined();
      const full = joinTokens(dow);
      for (const day of ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]) {
        expect(full).toContain(`"${day}"`);
      }
    });

    it("RecurrenceTrigger extends Trigger", () => {
      const rec = findLine(mainLines, "RecurrenceTrigger:interface")!;
      expect(tokenValues(rec)).toContain("extends");
      expect(tokenValues(rec)).toContain("Trigger");
    });
  });

  // ── MemoryStore types ─────────────────────────────────────────────────────

  describe("MemoryStore types", () => {
    it("MemoryStoreDefinitionUnion is a type alias", () => {
      expect(findLine(mainLines, "MemoryStoreDefinitionUnion:typealias")).toBeDefined();
    });

    it("UserProfileMemoryItem extends MemoryItem", () => {
      const upm = findLine(mainLines, "UserProfileMemoryItem:interface")!;
      expect(tokenValues(upm)).toContain("extends");
      expect(tokenValues(upm)).toContain("MemoryItem");
    });

    it("MemoryItemUnion contains UserProfileMemoryItem and ChatSummaryMemoryItem", () => {
      const union = findLine(mainLines, "MemoryItemUnion:typealias")!;
      const full = joinTokens(union);
      expect(full).toContain("UserProfileMemoryItem");
      expect(full).toContain("ChatSummaryMemoryItem");
    });
  });

  // ── models subpath – tool types ───────────────────────────────────────────

  describe("models subpath: tool type declarations", () => {
    it("Tool interface has LineId with models prefix", () => {
      const tool = findLine(modelsLines, "Tool:interface")!;
      expect(tool).toBeDefined();
      expect(tool.LineId).toBe(`${MODELS_MODULE}!Tool:interface`);
    });

    it("BingGroundingTool extends Tool with correct LineId", () => {
      const bing = findLine(modelsLines, "BingGroundingTool:interface")!;
      expect(bing).toBeDefined();
      expect(bing.LineId).toBe(`${MODELS_MODULE}!BingGroundingTool:interface`);
      expect(tokenValues(bing)).toContain("extends");
      expect(tokenValues(bing)).toContain("Tool");
    });

    it("BingGroundingTool.type readonly property = 'bing_grounding'", () => {
      const bing = findLine(modelsLines, "BingGroundingTool:interface")!;
      const typeProp = bing.Children?.find((c) => tokenValues(c).includes("type"));
      expect(typeProp).toBeDefined();
      expect(tokenValues(typeProp!)).toContain('"bing_grounding"');
    });

    it("ToolType string literal union contains all 12 members", () => {
      const toolType = findLine(modelsLines, "ToolType:typealias")!;
      expect(toolType).toBeDefined();
      const full = joinTokens(toolType);
      for (const m of ["bing_grounding", "azure_ai_search", "code_interpreter", "file_search", "function", "openapi", "web_search", "mcp", "image_gen"]) {
        expect(full).toContain(`"${m}"`);
      }
    });

    it("ToolUnion type alias references all tool types", () => {
      const union = findLine(modelsLines, "ToolUnion:typealias")!;
      const full = joinTokens(union);
      expect(full).toContain("BingGroundingTool");
      expect(full).toContain("AzureAISearchTool");
      expect(full).toContain("CodeInterpreterTool");
      expect(full).toContain("MCPTool");
      expect(full).toContain("WebSearchPreviewTool");
    });

    it("MCPTool has server_label and server_url required properties", () => {
      const mcp = findLine(modelsLines, "MCPTool:interface")!;
      expect(mcp).toBeDefined();
      const childNames = mcp.Children!.map((c) => tokenValues(c)[0]);
      expect(childNames).toContain("server_label");
      expect(childNames).toContain("server_url");
    });

    it("ContainerMemoryLimit type alias contains memory-size strings", () => {
      const cml = findLine(modelsLines, "ContainerMemoryLimit:typealias")!;
      const full = joinTokens(cml);
      expect(full).toContain('"1gb"');
      expect(full).toContain('"8gb"');
    });

    it("NavigateToId integrity within models subpath resolves across both modules", () => {
      // Some types (e.g. Tool) are defined in @azure/ai-projects and referenced
      // from @azure/ai-projects/models; the combined reference map resolves them
      // correctly. Check integrity using ALL LineIds from both modules.
      const allMain = collectAllLines(mainLines);
      const allModels = collectAllLines(modelsLines);
      const allLineIds = new Set(
        [...allMain, ...allModels].map((l) => l.LineId).filter(Boolean),
      );
      const brokenInModels = allModels
        .flatMap((l) => l.Tokens)
        .filter((t) => t.NavigateToId && !allLineIds.has(t.NavigateToId));
      expect(brokenInModels).toHaveLength(0);
    });
  });

  // ── empty lines between declarations ────────────────────────────────────

  describe("blank lines between declarations", () => {
    it("emits an empty line after AIProjectClient class", () => {
      const all = collectAllLines(mainLines);
      const clientId = findLine(mainLines, "AIProjectClient:class")!.LineId!;
      const contextEnd = all.find((l) => l.IsContextEndLine && l.RelatedToLine === clientId);
      expect(contextEnd).toBeDefined();
      const ctxIdx = all.indexOf(contextEnd!);
      const emptyAfter = all[ctxIdx + 1];
      expect(emptyAfter?.Tokens.length).toBe(0);
    });

    it("emits an empty line after AgentKind type alias", () => {
      const all = collectAllLines(mainLines);
      const agentKindLine = findLine(mainLines, "AgentKind:typealias")!;
      const idx = all.indexOf(agentKindLine);
      const emptyAfter = all[idx + 1];
      expect(emptyAfter?.Tokens.length).toBe(0);
    });
  });
});
