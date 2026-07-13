/**
 * end-to-end.test.ts — the required cross-cutting fixture test. Drives the whole
 * deterministic path on one committed fixture, with NO network or secrets:
 *
 *   raw PR cache → classify → filter → attribute → (inline judged fields) →
 *   emit run JSON
 *
 * Its job is to catch schema/naming drift *between* the deterministic scripts
 * that per-stage unit tests miss — e.g. if `emit` and `attribute` disagree on a
 * field name. The judgment stages (judge / cluster / propose) are the agent's
 * job in the workflow; here we stand them in by writing the judge-derived fields
 * directly onto the attributed rows, exactly as the agent would.
 */
import { execFileSync } from "node:child_process";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import { fileURLToPath } from "node:url";

import { afterAll, beforeAll, describe, expect, it } from "vitest";

import { parseRun } from "../scripts/run-schema.ts";
import type { AttributedComment } from "../scripts/types.ts";

const here = path.dirname(fileURLToPath(import.meta.url));
const scripts = path.join(here, "..", "scripts");
const fixtureGlob = path.join(here, "fixtures", "pr-sample.json");

function runStage(script: string, args: string[]): void {
    execFileSync("node", [path.join(scripts, script), ...args], {
        stdio: ["ignore", "ignore", "pipe"],
    });
}

interface AttributedFile {
    comments: AttributedComment[];
}

describe("end-to-end deterministic pipeline (offline fixture)", () => {
    let cache: string;
    let attributed: AttributedFile;

    beforeAll(() => {
        cache = fs.mkdtempSync(path.join(os.tmpdir(), "ccr-e2e-"));
        runStage("classify-pr.ts", [
            "--glob",
            fixtureGlob,
            "--cache-dir",
            cache,
        ]);
        runStage("filter-comments.ts", [
            "--glob",
            fixtureGlob,
            "--cache-dir",
            cache,
        ]);
        runStage("attribute-comments.ts", [
            "--glob",
            fixtureGlob,
            "--filtered",
            path.join(cache, "filtered.json"),
            "--cache-dir",
            cache,
        ]);
        runStage("build-judge-input.ts", [
            "--glob",
            fixtureGlob,
            "--attributed",
            path.join(cache, "attributed.json"),
            "--cache-dir",
            cache,
        ]);
        attributed = JSON.parse(
            fs.readFileSync(path.join(cache, "attributed.json"), "utf8"),
        ) as AttributedFile;
    });

    afterAll(() => {
        fs.rmSync(cache, { recursive: true, force: true });
    });

    it("classify → filter → attribute produce consistent cache files", () => {
        expect(fs.existsSync(path.join(cache, "classified.json"))).toBe(true);
        expect(fs.existsSync(path.join(cache, "filtered.json"))).toBe(true);
        expect(fs.existsSync(path.join(cache, "judge-input.json"))).toBe(true);
        expect(Array.isArray(attributed.comments)).toBe(true);
    });

    it("agent-written judge fields satisfy the isGap/theme contract", () => {
        // Stand in for the agent judge stage: write judge-derived fields onto the
        // attributed rows exactly as the workflow instructs. isGap and theme follow
        // the derivation the kept scripts + schema expect.
        const judged: AttributedComment[] = attributed.comments.map((c) => {
            if (c.authorKind === "human" && c.kind === "ask") {
                const isSubstantive = true;
                const diffDetectable = true;
                return {
                    ...c,
                    isSubstantive,
                    diffDetectable,
                    severity: c.severity ?? "substantive",
                    category: "error-handling",
                    confidence: 0.9,
                    judgeStatus: "ok",
                    ccrAddressedConcern: false,
                    isGap: isSubstantive && diffDetectable && c.ccrSawCode,
                    theme: isSubstantive ? "error-handling" : null,
                };
            }
            return c;
        });
        fs.writeFileSync(
            path.join(cache, "attributed.json"),
            JSON.stringify({ comments: judged }, null, 2),
        );
        for (const c of judged) {
            if (c.isGap === true) {
                expect(c.isSubstantive).toBe(true);
                expect(c.diffDetectable).toBe(true);
                expect(c.ccrSawCode).toBe(true);
                expect(c.theme).not.toBeNull();
            }
        }
    });

    it("emit-run-json produces a schema-valid run from the judged cache", () => {
        const out = fs.mkdtempSync(path.join(os.tmpdir(), "ccr-e2e-out-"));
        const metaPath = path.join(out, "meta.json");
        fs.writeFileSync(
            metaPath,
            JSON.stringify({
                repo: "Azure/azure-sdk-for-go",
                windowStart: "2026-06-01",
                windowEnd: "2026-06-18",
                windowLagDays: 14,
                prState: "merged",
                model: "openai/gpt-4o",
                modelTool: "gh models",
                temperature: 0,
                matchedCcrLogin: "copilot-pull-request-reviewer[bot]",
                promptHashes: { judge: "sha256:abc" },
                vocabularyHash: "sha256:def",
                toolVersion: "1.0",
                ccrEnabledSince: null,
            }),
        );
        try {
            runStage("emit-run-json.ts", [
                "--meta",
                metaPath,
                "--classified",
                path.join(cache, "classified.json"),
                "--attributed",
                path.join(cache, "attributed.json"),
                "--glob",
                fixtureGlob,
                "--out-dir",
                out,
            ]);
            const files = fs
                .readdirSync(out)
                .filter((f) => f.startsWith("run-") && f.endsWith(".json"));
            expect(files).toHaveLength(1);
            const runText = fs.readFileSync(
                path.join(out, files[0] ?? ""),
                "utf8",
            );
            // parseRun throws on any schema/naming drift across stages.
            const run = parseRun(JSON.parse(runText));
            expect(run.schemaVersion).toBe("1.0");
            expect(run.metrics.rates.ccrRecallRate).toBeDefined();
            // CommentRow has no body field — emit must strip it.
            for (const c of run.comments) {
                expect("body" in c).toBe(false);
            }
        } finally {
            fs.rmSync(out, { recursive: true, force: true });
        }
    });
});
