import { describe, it, expect, beforeEach, afterEach } from "vitest";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import { runWorkflow } from "../src/orchestrator.js";
import type { Harness, PhaseRequest } from "../src/harness.js";
import type { PhaseRunResult } from "../src/types.js";

const VALID_PLAN = `# Plan
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

function write(runDir: string, rel: string, content: string) {
    const p = path.join(runDir, rel);
    fs.mkdirSync(path.dirname(p), { recursive: true });
    fs.writeFileSync(p, content);
}

/** Programmable fake harness: a responder decides what each phase "writes" and returns. */
class FakeHarness implements Harness {
    calls: { label: string; readOnly: boolean }[] = [];
    counts: Record<string, number> = {};
    constructor(private responder: (req: PhaseRequest, n: number) => string) {}
    async runPhase(req: PhaseRequest): Promise<PhaseRunResult> {
        const key = req.label.replace(/\s*\(retry \d+\)/, "").split(":")[0];
        this.counts[key] = (this.counts[key] ?? 0) + 1;
        this.calls.push({ label: req.label, readOnly: req.readOnly });
        const finalText = this.responder(req, this.counts[key]);
        return { artifacts: [], finalText };
    }
    async stop() {}
}

let tmp: string;
beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), "aw-orch-"));
});
afterEach(() => {
    fs.rmSync(tmp, { recursive: true, force: true });
});

const baseOpts = (over: object = {}) => ({ task: "demo task", outRoot: tmp, judge: false, ...over });

describe("orchestrator — simple run", () => {
    it("runs assumptions -> plan -> implement and completes (exit 0)", async () => {
        const h = new FakeHarness((req) => {
            const rd = req.runDir;
            if (req.label.startsWith("assumptions")) write(rd, "assumptions.md", "- ok | high");
            if (req.label.startsWith("plan")) write(rd, "plan.md", VALID_PLAN);
            if (req.label.startsWith("implement")) return "STAGE_RESULT: pass";
            return "";
        });
        const res = await runWorkflow(baseOpts({ simple: true, runId: "r1" }), h);
        expect(res.exitCode).toBe(0);
        // research/classify/research-item skipped, not sent to harness
        expect(h.counts["research"]).toBeUndefined();
        expect(h.counts["classify"]).toBeUndefined();
        // synthesized subitems.json exists
        expect(fs.existsSync(path.join(res.runDir, "subitems.json"))).toBe(true);
        const state = JSON.parse(fs.readFileSync(path.join(res.runDir, "state.json"), "utf8"));
        expect(state.phases.implement).toBe("completed");
    });
});

describe("orchestrator — fresh-session retry", () => {
    it("retries the plan phase in a fresh session after a validation failure", async () => {
        const h = new FakeHarness((req, n) => {
            const rd = req.runDir;
            if (req.label.startsWith("assumptions")) write(rd, "assumptions.md", "- ok | high");
            if (req.label.startsWith("plan")) {
                // first attempt writes an invalid plan (no gate block), second writes valid
                write(rd, "plan.md", n === 1 ? "## Decisions\nbad" : VALID_PLAN);
            }
            if (req.label.startsWith("implement")) return "STAGE_RESULT: pass";
            return "";
        });
        const res = await runWorkflow(baseOpts({ simple: true }), h);
        expect(res.exitCode).toBe(0);
        expect(h.counts["plan"]).toBe(2); // failed once, retried fresh
    });
});

describe("orchestrator — failing gate halts", () => {
    it("halts (exit 1) when the agent reports a failing stage gate", async () => {
        const h = new FakeHarness((req) => {
            const rd = req.runDir;
            if (req.label.startsWith("assumptions")) write(rd, "assumptions.md", "- ok | high");
            if (req.label.startsWith("plan")) write(rd, "plan.md", VALID_PLAN);
            if (req.label.startsWith("implement")) return "STAGE_RESULT: fail — tests red";
            return "";
        });
        const res = await runWorkflow(baseOpts({ simple: true }), h);
        expect(res.exitCode).toBe(1);
        const state = JSON.parse(fs.readFileSync(path.join(res.runDir, "state.json"), "utf8"));
        expect(state.phases.implement).toBe("failed");
    });
});

describe("orchestrator — blocking clarification", () => {
    it("pauses (exit 10) on a blocking assumption", async () => {
        const h = new FakeHarness((req) => {
            if (req.label.startsWith("assumptions"))
                write(req.runDir, "assumptions.md", "- blocking: true | unsure about auth | low");
            return "";
        });
        const res = await runWorkflow(baseOpts({ simple: true }), h);
        expect(res.exitCode).toBe(10);
    });
});

describe("orchestrator — judge loop", () => {
    it("runs critique + revise on validated artifacts when judge is on", async () => {
        const h = new FakeHarness((req) => {
            const rd = req.runDir;
            if (req.label.startsWith("assumptions")) write(rd, "assumptions.md", "- ok | high");
            if (req.label.startsWith("plan")) write(rd, "plan.md", VALID_PLAN);
            if (req.label.startsWith("critique"))
                write(rd, `critiques/${req.label.split(":")[1]}.md`, "- looks fine | optional");
            if (req.label.startsWith("revise")) {
                /* keep artifact valid: rewrite plan identically */
                if (req.label.includes("plan")) write(rd, "plan.md", VALID_PLAN);
            }
            if (req.label.startsWith("implement")) return "STAGE_RESULT: pass";
            return "";
        });
        const res = await runWorkflow(baseOpts({ simple: true, judge: true }), h);
        expect(res.exitCode).toBe(0);
        expect(h.counts["critique"]).toBeGreaterThanOrEqual(1);
        expect(h.counts["revise"]).toBeGreaterThanOrEqual(1);
        // critique sessions are read-only
        expect(h.calls.filter((c) => c.label.startsWith("critique")).every((c) => c.readOnly)).toBe(true);
    });

    it("skips the judge loop with judge:false", async () => {
        const h = new FakeHarness((req) => {
            const rd = req.runDir;
            if (req.label.startsWith("assumptions")) write(rd, "assumptions.md", "- ok | high");
            if (req.label.startsWith("plan")) write(rd, "plan.md", VALID_PLAN);
            if (req.label.startsWith("implement")) return "STAGE_RESULT: pass";
            return "";
        });
        const res = await runWorkflow(baseOpts({ simple: true, judge: false }), h);
        expect(res.exitCode).toBe(0);
        expect(h.counts["critique"]).toBeUndefined();
    });
});

describe("orchestrator — resume", () => {
    it("skips completed phases on resume", async () => {
        // First run: implement fails, so plan/assumptions complete but implement does not.
        const failing = new FakeHarness((req) => {
            const rd = req.runDir;
            if (req.label.startsWith("assumptions")) write(rd, "assumptions.md", "- ok | high");
            if (req.label.startsWith("plan")) write(rd, "plan.md", VALID_PLAN);
            if (req.label.startsWith("implement")) return "STAGE_RESULT: fail";
            return "";
        });
        const first = await runWorkflow(baseOpts({ simple: true, runId: "resume-run" }), failing);
        expect(first.exitCode).toBe(1);

        // Resume with same runId: assumptions/plan already completed -> not re-sent.
        const passing = new FakeHarness((req) => {
            if (req.label.startsWith("implement")) return "STAGE_RESULT: pass";
            return "";
        });
        const second = await runWorkflow(baseOpts({ simple: true, runId: "resume-run" }), passing);
        expect(second.exitCode).toBe(0);
        expect(passing.counts["assumptions"]).toBeUndefined();
        expect(passing.counts["plan"]).toBeUndefined();
        expect(passing.counts["implement"]).toBe(1);
    });
});

describe("orchestrator — full pipeline (non-simple) with fan-out", () => {
    it("runs research -> classify -> research-item xN -> plan -> implement", async () => {
        const subitems = {
            task: "demo",
            classification: "feature",
            items: [
                { id: "a", type: "feature", title: "A", description: "a", dependsOn: [], overlapRisk: "low" },
                { id: "b", type: "feature", title: "B", description: "b", dependsOn: ["a"], overlapRisk: "low" },
            ],
        };
        const h = new FakeHarness((req) => {
            const rd = req.runDir;
            if (req.label.startsWith("research") && !req.label.startsWith("research-item")) {
                write(rd, "specs/architecture.md", "arch");
                write(rd, "specs/functional.md", "func");
                write(rd, "manifest.json", JSON.stringify({ apispec: { required: false, reason: "n/a" } }));
            }
            if (req.label.startsWith("classify")) write(rd, "subitems.json", JSON.stringify(subitems));
            if (req.label.startsWith("assumptions")) write(rd, "assumptions.md", "- ok | high");
            if (req.label.startsWith("research-item")) {
                const id = req.label.split(":")[1].replace(/\s.*/, "");
                write(rd, `research/${id}.md`, `note ${id}`);
            }
            if (req.label.startsWith("plan")) write(rd, "plan.md", VALID_PLAN);
            if (req.label.startsWith("implement")) return "STAGE_RESULT: pass";
            return "";
        });
        const res = await runWorkflow(baseOpts({ judge: false, runId: "full" }), h);
        expect(res.exitCode).toBe(0);
        expect(fs.existsSync(path.join(res.runDir, "research", "a.md"))).toBe(true);
        expect(fs.existsSync(path.join(res.runDir, "research", "b.md"))).toBe(true);
        expect(h.counts["research-item"]).toBe(2);
    });
});
