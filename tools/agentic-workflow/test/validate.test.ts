import { describe, it, expect } from "vitest";
import { validateSubitems, validateSubitemsJson, validatePlan } from "../src/validate.js";

const goodItem = {
    id: "main",
    type: "feature",
    title: "Do the thing",
    description: "covers the thing",
    dependsOn: [],
    overlapRisk: "low",
};

describe("validateSubitems", () => {
    it("accepts a well-formed object", () => {
        const r = validateSubitems({ task: "t", classification: "feature", items: [goodItem] });
        expect(r.ok).toBe(true);
        expect(r.errors).toEqual([]);
    });

    it("rejects empty items", () => {
        const r = validateSubitems({ task: "t", classification: "feature", items: [] });
        expect(r.ok).toBe(false);
        expect(r.errors.join()).toContain("non-empty array");
    });

    it("rejects bad classification and non-kebab id", () => {
        const r = validateSubitems({
            task: "t",
            classification: "nope",
            items: [{ ...goodItem, id: "Bad_ID" }],
        });
        expect(r.ok).toBe(false);
        expect(r.errors.join()).toContain("classification");
        expect(r.errors.join()).toContain("kebab-case");
    });

    it("rejects duplicate ids and dangling dependsOn", () => {
        const r = validateSubitems({
            task: "t",
            classification: "mixed",
            items: [
                { ...goodItem, id: "a", dependsOn: ["ghost"] },
                { ...goodItem, id: "a" },
            ],
        });
        expect(r.ok).toBe(false);
        expect(r.errors.join()).toContain("duplicate id");
        expect(r.errors.join()).toContain("unknown item");
    });

    it("validateSubitemsJson reports invalid JSON", () => {
        const r = validateSubitemsJson("{ not json");
        expect(r.ok).toBe(false);
        expect(r.errors.join()).toContain("invalid JSON");
    });
});

const planWithGate = `
# Plan
## 0. Research reconciliation
single item.
## 1. Decisions and rationale
because.
## 3. Step-by-step implementation plan
do it.
## 9. Definition of done
done when tests pass.

\`\`\`yaml
stages:
  - id: stage-1
    expected_files: ["src/a.ts"]
    context_needed: []
    steps:
      - { id: "1.1", description: "x" }
    gate:
      id: gate-1
      commands: ["npm test"]
      expected: exit_code_0
\`\`\`
`;

describe("validatePlan", () => {
    it("accepts a plan with required sections and a gate block", () => {
        const r = validatePlan(planWithGate);
        expect(r.ok).toBe(true);
    });

    it("rejects a plan missing the gate block", () => {
        const r = validatePlan(
            "## Decisions and rationale\n## Step-by-step implementation plan\n## Definition of done\n",
        );
        expect(r.ok).toBe(false);
        expect(r.errors.join()).toContain("stages");
    });

    it("rejects a plan missing required sections", () => {
        const r = validatePlan("no sections here");
        expect(r.ok).toBe(false);
        expect(r.errors.join()).toContain("Definition of done");
    });
});
