// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/**
 * Tests for a .d.ts file containing multiple `declare module` blocks including
 * third-party modules (openai, @azure/core-paging) alongside the owned module
 * (@azure/ai-projects).
 *
 * Fixture: test/dts/fixtures/real-world/ai-projects-openai.d.ts
 *   declare module "openai"             — 1 class, 11 interfaces
 *   declare module "@azure/core-paging" — 2 interfaces
 *   declare module "@azure/ai-projects" — 1 class, 8 interfaces
 *
 * This exercises the following parser and index behaviours:
 *
 *  • All `declare module` blocks — including third-party ones — are parsed and
 *    included in the review output (no filtering)
 *  • Each module's declarations get the correct per-module LineId prefix
 *    (e.g. "openai!", "@azure/core-paging!", "@azure/ai-projects!")
 *  • Cross-module NavigateToId: @azure/ai-projects types that reference openai
 *    or @azure/core-paging types get NavigateToId pointing into those modules
 *  • generateApiViewFromDts emits a "Declared Modules" summary section that
 *    lists every module with a NavigateToId link to its Subpath-export section
 *  • Declaration order in the file is preserved in the review output
 */

import path from "node:path";
import { describe, it, beforeAll, expect } from "vitest";
import { parseDtsFile, ParsedModule } from "../../src/dts/parser.js";
import { generateApiViewFromDts } from "../../src/dts/index.js";
import { ReviewLine, ReviewToken, TokenKind } from "../../src/models.js";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const FIXTURE = path.join(
    import.meta.dirname,
    "fixtures",
    "real-world",
    "ai-projects-openai.d.ts",
);
const PACKAGE = "@azure/ai-projects";
const OPENAI_MODULE = "openai";
const CORE_PAGING_MODULE = "@azure/core-paging";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function collectAll(lines: ReviewLine[]): ReviewLine[] {
    const all: ReviewLine[] = [];
    for (const l of lines) {
        all.push(l);
        if (l.Children?.length) all.push(...collectAll(l.Children));
    }
    return all;
}

function findLine(lines: ReviewLine[], idSuffix: string): ReviewLine | undefined {
    for (const l of lines) {
        if (l.LineId?.endsWith(idSuffix)) return l;
        if (l.Children?.length) {
            const r = findLine(l.Children, idSuffix);
            if (r) return r;
        }
    }
    return undefined;
}

function allTokens(lines: ReviewLine[]): ReviewToken[] {
    return collectAll(lines).flatMap((l) => l.Tokens ?? []);
}

function joinTokens(line: ReviewLine): string {
    return line.Tokens.map((t) => t.Value ?? "")
        .filter(Boolean)
        .join(" ");
}

// ---------------------------------------------------------------------------
// parseDtsFile: module detection
// ---------------------------------------------------------------------------

describe("ai-projects-openai.d.ts — parseDtsFile", () => {
    let parsed: Map<string, ParsedModule>;
    let openaiLines: ReviewLine[];
    let corePageLines: ReviewLine[];
    let mainLines: ReviewLine[];

    beforeAll(() => {
        parsed = parseDtsFile({ filePath: FIXTURE, packageName: PACKAGE });
        openaiLines = parsed.get(OPENAI_MODULE)!.lines;
        corePageLines = parsed.get(CORE_PAGING_MODULE)!.lines;
        mainLines = parsed.get(PACKAGE)!.lines;
    });

    describe("module detection", () => {
        it("parses without throwing", () => {
            expect(() => parseDtsFile({ filePath: FIXTURE, packageName: PACKAGE })).not.toThrow();
        });

        it("produces exactly three entry points", () => {
            expect(parsed.size).toBe(3);
        });

        it("contains the openai third-party module", () => {
            expect(parsed.has(OPENAI_MODULE)).toBe(true);
        });

        it("contains the @azure/core-paging third-party module", () => {
            expect(parsed.has(CORE_PAGING_MODULE)).toBe(true);
        });

        it("contains the owned @azure/ai-projects module", () => {
            expect(parsed.has(PACKAGE)).toBe(true);
        });

        it("does NOT create a '.' fallback entry", () => {
            expect(parsed.has(".")).toBe(false);
        });

        it("preserves module declaration order from the file", () => {
            const keys = [...parsed.keys()];
            expect(keys[0]).toBe(OPENAI_MODULE);
            expect(keys[1]).toBe(CORE_PAGING_MODULE);
            expect(keys[2]).toBe(PACKAGE);
        });

        it("all three modules have declarations", () => {
            expect(openaiLines.length).toBeGreaterThan(0);
            expect(corePageLines.length).toBeGreaterThan(0);
            expect(mainLines.length).toBeGreaterThan(0);
        });
    });

    // ── openai module declarations ─────────────────────────────────────────

    describe("openai module — LineId prefix isolation", () => {
        it("OpenAI class has openai! prefix", () => {
            const line = findLine(openaiLines, "OpenAI:class");
            expect(line?.LineId).toBe("openai!OpenAI:class");
        });

        it("OpenAI constructor has openai! prefix", () => {
            const line = findLine(openaiLines, "OpenAI.constructor:constructor");
            expect(line?.LineId).toBe("openai!OpenAI.constructor:constructor");
        });

        it("OpenAI.assistants property has openai! prefix", () => {
            const line = findLine(openaiLines, "OpenAI.assistants:property");
            expect(line?.LineId).toBe("openai!OpenAI.assistants:property");
        });

        it("OpenAIOptions interface has openai! prefix", () => {
            const line = findLine(openaiLines, "OpenAIOptions:interface");
            expect(line?.LineId).toBe("openai!OpenAIOptions:interface");
        });

        it("AssistantsAPI interface has openai! prefix", () => {
            expect(findLine(openaiLines, "AssistantsAPI:interface")?.LineId).toBe(
                "openai!AssistantsAPI:interface",
            );
        });

        it("ChatAPI interface has openai! prefix", () => {
            expect(findLine(openaiLines, "ChatAPI:interface")?.LineId).toBe("openai!ChatAPI:interface");
        });

        it("ChatCompletionsAPI interface has openai! prefix", () => {
            expect(findLine(openaiLines, "ChatCompletionsAPI:interface")?.LineId).toBe(
                "openai!ChatCompletionsAPI:interface",
            );
        });

        it("Assistant interface has openai! prefix", () => {
            expect(findLine(openaiLines, "Assistant:interface")?.LineId).toBe(
                "openai!Assistant:interface",
            );
        });

        it("AssistantsPage interface has openai! prefix", () => {
            expect(findLine(openaiLines, "AssistantsPage:interface")?.LineId).toBe(
                "openai!AssistantsPage:interface",
            );
        });

        it("ChatCompletion interface has openai! prefix", () => {
            expect(findLine(openaiLines, "ChatCompletion:interface")?.LineId).toBe(
                "openai!ChatCompletion:interface",
            );
        });

        it("no openai! LineId uses the packageName @azure/ai-projects instead", () => {
            const allOpenai = collectAll(openaiLines);
            const wrongPrefix = allOpenai.filter(
                (l) => l.LineId && l.LineId.startsWith("@azure/ai-projects!"),
            );
            expect(wrongPrefix).toHaveLength(0);
        });

        it("openai module has 1 class declaration", () => {
            const classes = collectAll(openaiLines).filter((l) => l.LineId?.endsWith(":class"));
            expect(classes).toHaveLength(1);
        });

        it("openai module has the expected interface count", () => {
            const ifaces = collectAll(openaiLines).filter((l) => l.LineId?.endsWith(":interface"));
            // AssistantsAPI, ChatAPI, ChatCompletionsAPI, OpenAIOptions, Assistant,
            // AssistantCreateParams, ChatCompletion, ChatCompletionCreateParams,
            // ChatCompletionChoice, ChatCompletionMessage, ChatCompletionMessageParam, AssistantsPage
            expect(ifaces.length).toBeGreaterThanOrEqual(11);
        });
    });

    describe("openai module — token content", () => {
        it("OpenAI class declaration emits export + class + OpenAI keywords", () => {
            const line = findLine(openaiLines, "OpenAI:class")!;
            const tokens = line.Tokens.map((t) => t.Value);
            expect(tokens).toContain("export");
            expect(tokens).toContain("class");
            expect(tokens).toContain("OpenAI");
        });

        it("OpenAIOptions.apiKey property emits apiKey and string", () => {
            const all = collectAll(openaiLines);
            const line = all.find(
                (l) =>
                    l.Tokens.some((t) => t.Value === "apiKey") &&
                    l.Tokens.some((t) => t.Value === "string"),
            );
            expect(line).toBeDefined();
        });

        it("AssistantsAPI.create method includes Promise return type", () => {
            const all = collectAll(openaiLines);
            const line = all.find(
                (l) =>
                    l.Tokens.some((t) => t.Value === "create") &&
                    l.Tokens.some((t) => t.Value === "Promise"),
            );
            expect(line).toBeDefined();
        });
    });

    // ── @azure/core-paging module declarations ────────────────────────────

    describe("@azure/core-paging module — LineId prefix isolation", () => {
        it("PagedAsyncIterableIterator has @azure/core-paging! prefix", () => {
            const line = findLine(corePageLines, "PagedAsyncIterableIterator:interface");
            expect(line?.LineId).toBe("@azure/core-paging!PagedAsyncIterableIterator:interface");
        });

        it("PageSettings has @azure/core-paging! prefix", () => {
            const line = findLine(corePageLines, "PageSettings:interface");
            expect(line?.LineId).toBe("@azure/core-paging!PageSettings:interface");
        });

        it("no @azure/core-paging LineId uses the wrong @azure/ai-projects! prefix", () => {
            const wrongPrefix = collectAll(corePageLines).filter(
                (l) => l.LineId && l.LineId.startsWith("@azure/ai-projects!"),
            );
            expect(wrongPrefix).toHaveLength(0);
        });

        it("no @azure/core-paging LineId uses the wrong openai! prefix", () => {
            const wrongPrefix = collectAll(corePageLines).filter(
                (l) => l.LineId && l.LineId.startsWith("openai!"),
            );
            expect(wrongPrefix).toHaveLength(0);
        });

        it("@azure/core-paging module has exactly 2 interface declarations", () => {
            const ifaces = collectAll(corePageLines).filter((l) => l.LineId?.endsWith(":interface"));
            expect(ifaces).toHaveLength(2);
        });
    });

    describe("@azure/core-paging module — token content", () => {
        it("PagedAsyncIterableIterator has TElement generic parameter", () => {
            const line = findLine(corePageLines, "PagedAsyncIterableIterator:interface")!;
            const tokens = line.Tokens.map((t) => t.Value);
            expect(tokens).toContain("TElement");
        });

        it("PagedAsyncIterableIterator has TPage generic parameter", () => {
            const line = findLine(corePageLines, "PagedAsyncIterableIterator:interface")!;
            const tokens = line.Tokens.map((t) => t.Value);
            expect(tokens).toContain("TPage");
        });

        it("PageSettings.continuationToken property is emitted", () => {
            const all = collectAll(corePageLines);
            const line = all.find((l) => l.Tokens.some((t) => t.Value === "continuationToken"));
            expect(line).toBeDefined();
        });
    });

    // ── @azure/ai-projects module declarations ────────────────────────────

    describe("@azure/ai-projects module — LineId prefix isolation", () => {
        it("AIProjectsClient class has @azure/ai-projects! prefix", () => {
            expect(findLine(mainLines, "AIProjectsClient:class")?.LineId).toBe(
                "@azure/ai-projects!AIProjectsClient:class",
            );
        });

        it("AIProjectsClient.getOpenAIClient method has @azure/ai-projects! prefix", () => {
            expect(findLine(mainLines, "AIProjectsClient.getOpenAIClient:method")?.LineId).toBe(
                "@azure/ai-projects!AIProjectsClient.getOpenAIClient:method",
            );
        });

        it("AgentsOperations interface has @azure/ai-projects! prefix", () => {
            expect(findLine(mainLines, "AgentsOperations:interface")?.LineId).toBe(
                "@azure/ai-projects!AgentsOperations:interface",
            );
        });

        it("Agent interface has @azure/ai-projects! prefix", () => {
            expect(findLine(mainLines, "Agent:interface")?.LineId).toBe(
                "@azure/ai-projects!Agent:interface",
            );
        });

        it("TokenCredential interface has @azure/ai-projects! prefix", () => {
            expect(findLine(mainLines, "TokenCredential:interface")?.LineId).toBe(
                "@azure/ai-projects!TokenCredential:interface",
            );
        });

        it("AccessToken interface has @azure/ai-projects! prefix", () => {
            expect(findLine(mainLines, "AccessToken:interface")?.LineId).toBe(
                "@azure/ai-projects!AccessToken:interface",
            );
        });

        it("no @azure/ai-projects LineId uses the wrong openai! prefix", () => {
            const wrongPrefix = collectAll(mainLines).filter(
                (l) => l.LineId && l.LineId.startsWith("openai!"),
            );
            expect(wrongPrefix).toHaveLength(0);
        });

        it("no @azure/ai-projects LineId uses the @azure/core-paging! prefix", () => {
            const wrongPrefix = collectAll(mainLines).filter(
                (l) => l.LineId && l.LineId.startsWith("@azure/core-paging!"),
            );
            expect(wrongPrefix).toHaveLength(0);
        });

        it("@azure/ai-projects module has exactly 1 class declaration", () => {
            const classes = collectAll(mainLines).filter((l) => l.LineId?.endsWith(":class"));
            expect(classes).toHaveLength(1);
        });
    });

    // ── Cross-module NavigateToId: @azure/ai-projects → openai ───────────

    describe("cross-module NavigateToId: @azure/ai-projects → openai", () => {
        it("getOpenAIClient return type OpenAI navigates to openai!OpenAI:class", () => {
            const method = findLine(mainLines, "AIProjectsClient.getOpenAIClient:method")!;
            const openaiToken = method.Tokens.find(
                (t) => t.Value === "OpenAI" && t.NavigateToId === "openai!OpenAI:class",
            );
            expect(openaiToken).toBeDefined();
        });

        it("getOpenAIClient return type token has TypeName kind", () => {
            const method = findLine(mainLines, "AIProjectsClient.getOpenAIClient:method")!;
            const openaiToken = method.Tokens.find((t) => t.NavigateToId === "openai!OpenAI:class");
            expect(openaiToken?.Kind).toBe(TokenKind.TypeName);
        });
    });

    // ── Cross-module NavigateToId: @azure/ai-projects → @azure/core-paging

    describe("cross-module NavigateToId: @azure/ai-projects → @azure/core-paging", () => {
        it("AgentsOperations.listAgents return type navigates to @azure/core-paging!", () => {
            const all = collectAll(mainLines);
            const listLine = all.find(
                (l) =>
                    l.Tokens.some((t) => t.Value === "listAgents") &&
                    l.Tokens.some(
                        (t) =>
                            t.NavigateToId === "@azure/core-paging!PagedAsyncIterableIterator:interface",
                    ),
            );
            expect(listLine).toBeDefined();
        });

        it("PagedAsyncIterableIterator NavigateToId points to @azure/core-paging! not openai!", () => {
            const tokens = allTokens(mainLines);
            const pageToken = tokens.find((t) => t.Value === "PagedAsyncIterableIterator");
            expect(pageToken?.NavigateToId).toBe(
                "@azure/core-paging!PagedAsyncIterableIterator:interface",
            );
        });
    });

    // ── Cross-module NavigateToId within openai module ────────────────────

    describe("cross-module NavigateToId within openai module (self-referential)", () => {
        it("OpenAI constructor OpenAIOptions parameter navigates to openai!OpenAIOptions:interface", () => {
            const ctor = findLine(openaiLines, "OpenAI.constructor:constructor")!;
            const optToken = ctor.Tokens.find(
                (t) => t.Value === "OpenAIOptions" && t.NavigateToId === "openai!OpenAIOptions:interface",
            );
            expect(optToken).toBeDefined();
        });

        it("OpenAI.assistants property type AssistantsAPI navigates to openai!AssistantsAPI:interface", () => {
            const prop = findLine(openaiLines, "OpenAI.assistants:property")!;
            const navToken = prop.Tokens.find(
                (t) => t.Value === "AssistantsAPI" && t.NavigateToId === "openai!AssistantsAPI:interface",
            );
            expect(navToken).toBeDefined();
        });

        it("AssistantsAPI.create return type navigates to openai!Assistant:interface", () => {
            const all = collectAll(openaiLines);
            const createLine = all.find(
                (l) =>
                    l.Tokens.some((t) => t.Value === "create") &&
                    l.Tokens.some((t) => t.NavigateToId === "openai!Assistant:interface"),
            );
            expect(createLine).toBeDefined();
        });
    });

    // ── Combined reference map: no duplicate IDs across modules ──────────

    describe("combined reference map: no LineId collision across modules", () => {
        it("openai!OpenAI:class and @azure/ai-projects!AIProjectsClient:class are distinct", () => {
            const openaiClass = findLine(openaiLines, "openai!OpenAI:class");
            const mainClass = findLine(mainLines, "@azure/ai-projects!AIProjectsClient:class");
            expect(openaiClass?.LineId).not.toBe(mainClass?.LineId);
        });

        it("openai!Assistant:interface and @azure/ai-projects!Agent:interface are distinct", () => {
            const openaiAssistant = findLine(openaiLines, "openai!Assistant:interface");
            const mainAgent = findLine(mainLines, "@azure/ai-projects!Agent:interface");
            expect(openaiAssistant?.LineId).not.toBe(mainAgent?.LineId);
        });

        it("all LineIds across all three modules are unique", () => {
            const allIds: string[] = [];
            for (const mod of parsed.values()) {
                collectAll(mod.lines)
                    .filter((l) => l.LineId)
                    .forEach((l) => allIds.push(l.LineId!));
            }
            const unique = new Set(allIds);
            expect(unique.size).toBe(allIds.length);
        });
    });
});

// ---------------------------------------------------------------------------
// generateApiViewFromDts: full CodeFile structure
// ---------------------------------------------------------------------------

describe("ai-projects-openai.d.ts — generateApiViewFromDts", () => {
    let reviewLines: ReviewLine[];

    beforeAll(async () => {
        const codeFile = await generateApiViewFromDts({
            dtsFilePath: FIXTURE,
            packageName: PACKAGE,
            packageVersion: "1.0.0-beta.1",
            parserVersion: "2.0.0",
        });
        reviewLines = codeFile.ReviewLines;
    });

    // ── ReviewLines top-level structure ───────────────────────────────────

    describe("ReviewLines structure", () => {
        it("has the correct number of top-level review lines", () => {
            // [0] Modules, [1] Modules-closer,
            // [2] Subpath-export-openai, [3] openai-closer,
            // [4] Subpath-export-@azure/core-paging, [5] core-paging-closer,
            // [6] Subpath-export-@azure/ai-projects, [7] ai-projects-closer
            // (no Dependencies section because there is no adjacent package.json)
            expect(reviewLines.length).toBe(8);
        });

        it("first line is the Modules summary section", () => {
            expect(reviewLines[0].LineId).toBe("Modules");
        });

        it("second line is the closer for the Modules section", () => {
            expect(reviewLines[1].RelatedToLine).toBe("Modules");
            expect(reviewLines[1].Tokens).toHaveLength(0);
        });

        it("openai Subpath-export is at index 2", () => {
            expect(reviewLines[2].LineId).toBe("Subpath-export-openai");
        });

        it("openai closer is at index 3", () => {
            expect(reviewLines[3].RelatedToLine).toBe("Subpath-export-openai");
        });

        it("@azure/core-paging Subpath-export is at index 4", () => {
            expect(reviewLines[4].LineId).toBe("Subpath-export-@azure/core-paging");
        });

        it("@azure/core-paging closer is at index 5", () => {
            expect(reviewLines[5].RelatedToLine).toBe("Subpath-export-@azure/core-paging");
        });

        it("@azure/ai-projects Subpath-export is at index 6", () => {
            expect(reviewLines[6].LineId).toBe("Subpath-export-@azure/ai-projects");
        });

        it("@azure/ai-projects closer is at index 7", () => {
            expect(reviewLines[7].RelatedToLine).toBe("Subpath-export-@azure/ai-projects");
        });
    });

    // ── Declared Modules section ──────────────────────────────────────────

    describe("Declared Modules section", () => {
        it("Modules section has NavigationDisplayName 'Modules'", () => {
            const token = reviewLines[0].Tokens.find((t) => t.NavigationDisplayName === "Modules");
            expect(token).toBeDefined();
        });

        it("Modules section token has Comment kind", () => {
            const token = reviewLines[0].Tokens.find((t) => t.NavigationDisplayName === "Modules");
            expect(token?.Kind).toBe(TokenKind.Comment);
        });

        it("Modules section value is '// Declared Modules'", () => {
            expect(reviewLines[0].Tokens[0].Value).toBe("// Declared Modules");
        });

        it("Modules section has 3 children (one per module)", () => {
            expect(reviewLines[0].Children).toHaveLength(3);
        });

        it("first child links to openai", () => {
            const child = reviewLines[0].Children![0];
            expect(child.Tokens[0].Value).toBe('"openai"');
            expect(child.Tokens[0].NavigateToId).toBe("Subpath-export-openai");
        });

        it("second child links to @azure/core-paging", () => {
            const child = reviewLines[0].Children![1];
            expect(child.Tokens[0].Value).toBe('"@azure/core-paging"');
            expect(child.Tokens[0].NavigateToId).toBe("Subpath-export-@azure/core-paging");
        });

        it("third child links to @azure/ai-projects", () => {
            const child = reviewLines[0].Children![2];
            expect(child.Tokens[0].Value).toBe('"@azure/ai-projects"');
            expect(child.Tokens[0].NavigateToId).toBe("Subpath-export-@azure/ai-projects");
        });

        it("module list children are SkipDiff", () => {
            for (const child of reviewLines[0].Children!) {
                expect(child.Tokens[0].SkipDiff).toBe(true);
            }
        });

        it("module list children have StringLiteral kind", () => {
            for (const child of reviewLines[0].Children!) {
                expect(child.Tokens[0].Kind).toBe(TokenKind.StringLiteral);
            }
        });
    });

    // ── Subpath-export sections: all modules present ──────────────────────

    describe("Subpath-export sections", () => {
        it("openai Subpath-export has NavigationDisplayName '\"openai\"'", () => {
            const token = reviewLines[2].Tokens.find((t) => t.NavigationDisplayName);
            expect(token?.NavigationDisplayName).toBe('"openai"');
        });

        it("@azure/core-paging Subpath-export has correct NavigationDisplayName", () => {
            const token = reviewLines[4].Tokens.find((t) => t.NavigationDisplayName);
            expect(token?.NavigationDisplayName).toBe('"@azure/core-paging"');
        });

        it("@azure/ai-projects Subpath-export has correct NavigationDisplayName", () => {
            const token = reviewLines[6].Tokens.find((t) => t.NavigationDisplayName);
            expect(token?.NavigationDisplayName).toBe('"@azure/ai-projects"');
        });

        it("openai section has children (declarations)", () => {
            expect(reviewLines[2].Children!.length).toBeGreaterThan(0);
        });

        it("@azure/core-paging section has children (declarations)", () => {
            expect(reviewLines[4].Children!.length).toBeGreaterThan(0);
        });

        it("@azure/ai-projects section has children (declarations)", () => {
            expect(reviewLines[6].Children!.length).toBeGreaterThan(0);
        });

        it("openai section contains the OpenAI class", () => {
            const openaiChildren = collectAll(reviewLines[2].Children!);
            const openaiClass = openaiChildren.find((l) => l.LineId === "openai!OpenAI:class");
            expect(openaiClass).toBeDefined();
        });

        it("@azure/core-paging section contains PagedAsyncIterableIterator", () => {
            const children = collectAll(reviewLines[4].Children!);
            const line = children.find(
                (l) => l.LineId === "@azure/core-paging!PagedAsyncIterableIterator:interface",
            );
            expect(line).toBeDefined();
        });

        it("@azure/ai-projects section contains AIProjectsClient class", () => {
            const children = collectAll(reviewLines[6].Children!);
            const line = children.find(
                (l) => l.LineId === "@azure/ai-projects!AIProjectsClient:class",
            );
            expect(line).toBeDefined();
        });
    });

    // ── Cross-module NavigateToId in the CodeFile output ─────────────────

    describe("cross-module NavigateToId in review output", () => {
        it("getOpenAIClient return type navigates to openai!OpenAI:class in the review", () => {
            const aiProjectsChildren = collectAll(reviewLines[6].Children!);
            const method = aiProjectsChildren.find(
                (l) => l.LineId === "@azure/ai-projects!AIProjectsClient.getOpenAIClient:method",
            )!;
            const token = method.Tokens.find((t) => t.NavigateToId === "openai!OpenAI:class");
            expect(token).toBeDefined();
        });

        it("listAgents return PagedAsyncIterableIterator navigates to @azure/core-paging!", () => {
            const aiProjectsChildren = collectAll(reviewLines[6].Children!);
            const listLine = aiProjectsChildren.find(
                (l) =>
                    l.Tokens.some((t) => t.Value === "listAgents") &&
                    l.Tokens.some(
                        (t) =>
                            t.NavigateToId ===
                            "@azure/core-paging!PagedAsyncIterableIterator:interface",
                    ),
            );
            expect(listLine).toBeDefined();
        });

        it("openai!OpenAI:class NavigateToId target exists in the review", () => {
            const openaiChildren = collectAll(reviewLines[2].Children!);
            const target = openaiChildren.find((l) => l.LineId === "openai!OpenAI:class");
            expect(target).toBeDefined();
        });

        it("@azure/core-paging!PagedAsyncIterableIterator:interface NavigateToId target exists", () => {
            const coreChildren = collectAll(reviewLines[4].Children!);
            const target = coreChildren.find(
                (l) => l.LineId === "@azure/core-paging!PagedAsyncIterableIterator:interface",
            );
            expect(target).toBeDefined();
        });

        it("all NavigateToId values in @azure/ai-projects section resolve to existing LineIds", () => {
            // Collect all LineIds across the entire review
            const allLineIds = new Set<string>();
            for (const top of reviewLines) {
                collectAll(top.Children ?? [top]).forEach((l) => {
                    if (l.LineId) allLineIds.add(l.LineId);
                });
                if (top.LineId) allLineIds.add(top.LineId);
            }

            const aiProjectsChildren = collectAll(reviewLines[6].Children!);
            const brokenRefs: string[] = [];
            for (const line of aiProjectsChildren) {
                for (const token of line.Tokens ?? []) {
                    if (token.NavigateToId && !allLineIds.has(token.NavigateToId)) {
                        brokenRefs.push(token.NavigateToId);
                    }
                }
            }
            expect(brokenRefs).toHaveLength(0);
        });
    });

    // ── CodeFile metadata ──────────────────────────────────────────────────

    describe("CodeFile metadata", () => {
        it("PackageName is the supplied package name", async () => {
            const codeFile = await generateApiViewFromDts({
                dtsFilePath: FIXTURE,
                packageName: PACKAGE,
                packageVersion: "1.0.0-beta.1",
                parserVersion: "2.0.0",
            });
            expect(codeFile.PackageName).toBe(PACKAGE);
        });

        it("PackageVersion is the supplied version", async () => {
            const codeFile = await generateApiViewFromDts({
                dtsFilePath: FIXTURE,
                packageName: PACKAGE,
                packageVersion: "1.0.0-beta.1",
                parserVersion: "2.0.0",
            });
            expect(codeFile.PackageVersion).toBe("1.0.0-beta.1");
        });

        it("Language is JavaScript", async () => {
            const codeFile = await generateApiViewFromDts({
                dtsFilePath: FIXTURE,
                packageName: PACKAGE,
                packageVersion: "1.0.0-beta.1",
                parserVersion: "2.0.0",
            });
            expect(codeFile.Language).toBe("JavaScript");
        });
    });
});
