import { describe, it, expect } from "vitest";
import { parsePlanStages, extractGateBlock } from "../src/gates.js";

const plan = `
intro
\`\`\`yaml
stages:
  - id: stage-1
    expected_files: ["src/a.ts", "test/a.test.ts"]
    context_needed: ["src/types.ts"]
    steps:
      - { id: "1.1", description: "edit a" }
    gate:
      id: gate-1
      commands: ["npm test -- a", "npm run lint"]
      expected: exit_code_0
  - id: stage-2
    gate:
      commands: ["npm test -- b"]
\`\`\`
`;

describe("gates", () => {
    it("extracts the yaml stages block", () => {
        expect(extractGateBlock(plan)).toContain("stages:");
    });

    it("parses stages with defaults filled in", () => {
        const stages = parsePlanStages(plan);
        expect(stages).toHaveLength(2);
        expect(stages[0].id).toBe("stage-1");
        expect(stages[0].gate.commands).toEqual(["npm test -- a", "npm run lint"]);
        expect(stages[0].expected_files).toEqual(["src/a.ts", "test/a.test.ts"]);
        // stage-2 omitted optional fields -> defaults
        expect(stages[1].gate.id).toBe("gate-2");
        expect(stages[1].gate.expected).toBe("exit_code_0");
        expect(stages[1].expected_files).toEqual([]);
    });

    it("throws when no gate block exists", () => {
        expect(() => parsePlanStages("no block")).toThrow(/no machine-readable/);
    });

    it("throws when a stage has no commands", () => {
        const bad = "```yaml\nstages:\n  - id: s1\n    gate:\n      commands: []\n```";
        expect(() => parsePlanStages(bad)).toThrow(/no gate.commands/);
    });
});
