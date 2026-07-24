/**
 * pr-evidence.test.ts — Phase 0 golden oracle for the #4 fixed-read layers.
 *
 * The committed snapshots under tests/fixtures/oracle/ are the equivalence target
 * the Phase 2 REST→GraphQL migration must reproduce after normalization. If the
 * deterministic evidence extraction or the judge-input assembly drifts, this
 * fails — flagging silent metric drift before it ships.
 */
import { execFileSync } from "node:child_process";
import * as fs from "node:fs";
import * as os from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

import { afterAll, beforeAll, describe, expect, it } from "vitest";

import { extractPrEvidence } from "../scripts/pr-evidence.ts";
import type { PullRequestData } from "../scripts/types.ts";

const here = dirname(fileURLToPath(import.meta.url));
const scripts = join(here, "..", "scripts");
const fixture = join(here, "fixtures", "pr-sample.json");
const readJson = (path: string): unknown =>
    JSON.parse(fs.readFileSync(path, "utf8"));

const prData = readJson(fixture) as PullRequestData;

describe("golden oracle — per-PR normalized evidence (#4 fixed reads)", () => {
    it("matches the committed evidence snapshot", () => {
        const oracle = readJson(
            join(here, "fixtures", "oracle", "pr-100.evidence.json"),
        );
        expect(extractPrEvidence(prData)).toEqual(oracle);
    });

    it("evidence extraction is deterministic (source-order independent)", () => {
        const shuffled: PullRequestData = {
            ...prData,
            reviews: [...prData.reviews].reverse(),
            inline: [...prData.inline].reverse(),
            issue: [...prData.issue].reverse(),
        };
        expect(extractPrEvidence(shuffled)).toEqual(extractPrEvidence(prData));
    });
});

describe("golden oracle — judge-input over the real prep pipeline", () => {
    let cache: string;

    beforeAll(() => {
        cache = fs.mkdtempSync(join(os.tmpdir(), "ccr-oracle-"));
        const run = (script: string, args: string[]): void => {
            execFileSync("node", [join(scripts, script), ...args], {
                stdio: ["ignore", "ignore", "pipe"],
            });
        };
        run("classify-pr.ts", ["--glob", fixture, "--cache-dir", cache]);
        run("filter-comments.ts", ["--glob", fixture, "--cache-dir", cache]);
        run("attribute-comments.ts", [
            "--glob",
            fixture,
            "--filtered",
            join(cache, "filtered.json"),
            "--cache-dir",
            cache,
        ]);
        run("build-judge-input.ts", [
            "--glob",
            fixture,
            "--attributed",
            join(cache, "attributed.json"),
            "--cache-dir",
            cache,
        ]);
    });

    afterAll(() => {
        fs.rmSync(cache, { recursive: true, force: true });
    });

    it("matches the committed judge-input snapshot", () => {
        const snapshot = readJson(
            join(here, "fixtures", "oracle", "pr-100.judge-input.json"),
        );
        const produced = readJson(join(cache, "judge-input.json"));
        expect(produced).toEqual(snapshot);
    });
});
