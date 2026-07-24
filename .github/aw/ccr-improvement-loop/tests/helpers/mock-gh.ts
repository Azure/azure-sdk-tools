/**
 * mock-gh.ts — a reusable, injectable `gh` recorder for the offline test suite.
 *
 * Builds a throwaway executable named `gh` on a temp `PATH` entry that (a)
 * appends every invocation's argv (as a JSON line) to a record file, and (b)
 * answers the REST/GraphQL reads `fetch-prs.ts` issues with either a committed
 * fixture or a synthetic-but-valid default. Because the scripts shell out to
 * `gh` (utils.ts), swapping the binary on `PATH` is the least-invasive seam —
 * no production code needs a client parameter.
 *
 * The recorder is the shared instrument Gate 1's zero-refetch / poison-cache
 * tests assert against, and Phases 2 (GraphQL migration) and 4 reuse it to prove
 * request-count deltas.
 *
 * Response routing (matches utils.ts `--paginate --slurp` semantics):
 *   - `gh api graphql …`                        → `{}` (thread resolution is
 *                                                  best-effort; empty is valid).
 *   - `gh api …/pulls/<n>`                       → `[ <pr object> ]` (1 slurp page)
 *   - `gh api …/commits/<sha>`                   → `[ { files: [] } ]`
 *   - `gh api …/{reviews,comments,commits}`      → `[ [] ]` (1 empty page)
 *   - `gh pr list …`                             → `[]`
 * A fixtures dir may override any endpoint (file = endpoint with `/`→`_`, `.json`).
 * Per-endpoint forced failures are configured via `fail` (substring match).
 */
import {
    chmodSync,
    existsSync,
    mkdtempSync,
    readFileSync,
    rmSync,
    writeFileSync,
} from "node:fs";
import * as os from "node:os";
import * as path from "node:path";

export interface MockGhOptions {
    /** Directory of `<endpoint-with-slashes-as-underscores>.json` overrides. */
    fixturesDir?: string;
    /** Endpoint substrings that must make `gh api` exit non-zero (mid-fetch failure). */
    fail?: string[];
}

export interface MockGh {
    /** Temp dir holding the `gh` shim; prepend to PATH. */
    binDir: string;
    /** File the shim appends argv JSON-lines to. */
    recordFile: string;
    /** Env overlay (PATH with binDir prepended + record/fixtures/fail vars). */
    env: Record<string, string>;
    /** All recorded invocations, each as an argv array. */
    calls(): string[][];
    /** Only `gh api …` invocations (the request-count surface). */
    apiCalls(): string[][];
    /** Truncate the record file. */
    reset(): void;
    /** Remove the temp bin dir. */
    cleanup(): void;
}

const SHIM = String.raw`#!/usr/bin/env node
"use strict";
const fs = require("node:fs");
const path = require("node:path");
const argv = process.argv.slice(2);
const rec = process.env.CCR_GH_RECORD;
if (rec) fs.appendFileSync(rec, JSON.stringify(argv) + "\n");

const fixturesDir = process.env.CCR_GH_FIXTURES || "";
const failList = (process.env.CCR_GH_FAIL || "")
    .split("\n")
    .map((s) => s.trim())
    .filter(Boolean);

function out(obj) {
    process.stdout.write(JSON.stringify(obj));
    process.exit(0);
}
function die(msg) {
    process.stderr.write("mock-gh: " + msg + "\n");
    process.exit(1);
}
// Forced-failure hook (poison-cache tests): match against the whole argv so a
// substring like "graphql" or "/commits/" can abort a mid-fetch run.
const joined = argv.join(" ");
for (const f of failList) {
    if (joined.includes(f)) die("forced failure on " + f);
}
function fieldValue(key) {
    for (const a of argv) {
        if (a.indexOf(key + "=") === 0) return a.slice(key.length + 1);
    }
    return "";
}
function endpointOf(a) {
    for (let i = 1; i < a.length; i++) {
        if (!a[i].startsWith("-") && a[i] !== "graphql") return a[i];
    }
    return "";
}
function loadFixture(endpoint) {
    if (!fixturesDir) return null;
    const file = path.join(fixturesDir, endpoint.replace(/\//g, "_") + ".json");
    if (!fs.existsSync(file)) return null;
    return JSON.parse(fs.readFileSync(file, "utf8"));
}
function emptyPage() {
    return { nodes: [], pageInfo: { hasNextPage: false, endCursor: null } };
}

if (argv[0] === "pr" && argv[1] === "list") out([]);

if (argv[0] === "api" && argv.includes("graphql")) {
    const query = fieldValue("query");
    // The production per-PR GraphQL assembler's base query. Return a minimal but
    // VALID pullRequest with one commit (so the GraphQL path still issues its one
    // REST commit-detail read) and empty connections (no pagination follow-ups).
    if (query.indexOf("# op:base") >= 0 || query.indexOf("pullRequest(number") >= 0) {
        const n = Number(fieldValue("num")) || 0;
        out({
            data: {
                rateLimit: { cost: 1 },
                repository: {
                    pullRequest: {
                        number: n,
                        title: "PR " + n,
                        url: "https://github.com/o/r/pull/" + n,
                        state: "MERGED",
                        createdAt: "2026-06-02T00:00:00Z",
                        mergedAt: "2026-06-03T00:00:00Z",
                        additions: 1,
                        deletions: 0,
                        isDraft: false,
                        author: { login: "octocat", __typename: "User" },
                        labels: emptyPage(),
                        reviews: emptyPage(),
                        comments: emptyPage(),
                        commits: {
                            nodes: [
                                {
                                    commit: {
                                        oid: "abc123",
                                        committedDate: "2026-06-02T08:00:00Z",
                                        authoredDate: null,
                                    },
                                },
                            ],
                            pageInfo: { hasNextPage: false, endCursor: null },
                        },
                        reviewThreads: emptyPage(),
                        closingIssuesReferences: emptyPage(),
                    },
                },
            },
        });
    }
    // Focused pagination / legacy thread-resolution queries: empty (never reached
    // when base reports no next pages), returned valid-empty for safety.
    out({ data: { rateLimit: { cost: 1 }, repository: { pullRequest: {} } } });
}

if (argv[0] === "api") {
    const endpoint = endpointOf(argv);
    const fx = loadFixture(endpoint);
    // Object endpoints: single PR, or a single commit's files.
    const mPull = /\/pulls\/(\d+)$/.exec(endpoint);
    if (mPull) {
        if (fx) out([fx]);
        const n = Number(mPull[1]);
        out([
            {
                number: n,
                title: "PR " + n,
                user: { login: "octocat", type: "User" },
                html_url: "https://github.com/o/r/pull/" + n,
                state: "closed",
                draft: false,
                additions: 1,
                deletions: 0,
                created_at: "2026-06-02T00:00:00Z",
                merged_at: "2026-06-03T00:00:00Z",
                labels: [],
            },
        ]);
    }
    if (/\/commits\/[0-9a-f]+$/.test(endpoint)) out([fx || { files: [] }]);
    // Array (list) endpoints: reviews / comments / commits — one slurp page.
    out([fx || []]);
}

die("unhandled argv " + JSON.stringify(argv));
`;

/** Stand up a fresh mock-gh recorder on a temp PATH entry. */
export function setupMockGh(opts: MockGhOptions = {}): MockGh {
    const binDir = mkdtempSync(path.join(os.tmpdir(), "ccr-mockgh-"));
    const ghPath = path.join(binDir, "gh");
    writeFileSync(ghPath, SHIM);
    chmodSync(ghPath, 0o755);
    const recordFile = path.join(binDir, "calls.jsonl");
    writeFileSync(recordFile, "");

    const env: Record<string, string> = {
        PATH: `${binDir}${path.delimiter}${process.env.PATH ?? ""}`,
        CCR_GH_RECORD: recordFile,
    };
    if (opts.fixturesDir) env.CCR_GH_FIXTURES = opts.fixturesDir;
    if (opts.fail && opts.fail.length > 0)
        env.CCR_GH_FAIL = opts.fail.join("\n");

    function calls(): string[][] {
        if (!existsSync(recordFile)) return [];
        return readFileSync(recordFile, "utf8")
            .split("\n")
            .filter((l) => l.trim() !== "")
            .map((l) => JSON.parse(l) as string[]);
    }

    return {
        binDir,
        recordFile,
        env,
        calls,
        apiCalls: () => calls().filter((a) => a[0] === "api"),
        reset: () => {
            writeFileSync(recordFile, "");
        },
        cleanup: () => {
            rmSync(binDir, { recursive: true, force: true });
        },
    };
}
