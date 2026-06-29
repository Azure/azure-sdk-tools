import { describe, it, expect } from "vitest";
import { render, loadTemplate, renderTemplate, type PromptName } from "../src/prompts.js";

const ALL: PromptName[] = [
    "01-research",
    "02-assumptions",
    "03-classify",
    "04-research-item",
    "05-plan",
    "06-implement",
    "critique",
    "revise",
];

describe("prompt rendering", () => {
    it("substitutes vars and blanks unknown keys", () => {
        expect(render("Task: {{task}} / {{missing}}", { task: "X" })).toBe("Task: X / ");
    });

    it("all 8 templates load and render run vars", () => {
        for (const name of ALL) {
            const raw = loadTemplate(name);
            expect(raw.length).toBeGreaterThan(0);
            const out = renderTemplate(name, {
                task: "demo task",
                item: "{}",
                itemId: "main",
                stage: "id: stage-1",
                artifactPath: "plan.md",
                artifactName: "plan",
                critiquePath: "critiques/plan.md",
            });
            // no unresolved placeholders remain
            expect(out).not.toMatch(/\{\{\s*[a-zA-Z0-9_]+\s*\}\}/);
        }
    });

    it("phase 1 keeps its read-only contract language", () => {
        const out = renderTemplate("01-research", { task: "t" });
        expect(out.toLowerCase()).toContain("read-only");
        expect(out).toContain("write_artifact");
    });
});
