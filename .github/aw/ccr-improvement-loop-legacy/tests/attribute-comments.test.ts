import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { loadConfig } from "../scripts/config.ts";
import {
    attributePr,
    makeFindingId,
    makeRowId,
} from "../scripts/attribute-comments.ts";
import type { AttributedComment, PullRequestData } from "../scripts/types.ts";

const here = dirname(fileURLToPath(import.meta.url));
const cfg = loadConfig();

function loadSample(): PullRequestData {
    return JSON.parse(
        readFileSync(join(here, "fixtures", "pr-sample.json"), "utf8"),
    ) as PullRequestData;
}

function allIds(data: PullRequestData): Set<number> {
    return new Set<number>([
        ...data.inline.map((c) => c.id),
        ...data.reviews.map((r) => r.id),
        ...data.issue.map((c) => c.id),
    ]);
}

function byId(rows: AttributedComment[], id: number): AttributedComment {
    const r = rows.find((x) => x.externalId === id);
    if (!r) throw new Error(`no row for ${id}`);
    return r;
}

describe("attribute-comments (fixture)", () => {
    const data = loadSample();
    const rows = attributePr(data, allIds(data), cfg);

    it("classifies the CCR comment author kind as ccr", () => {
        expect(byId(rows, 2).authorKind).toBe("ccr");
    });

    it("classifies Copilot inline comment author kind as ccr", () => {
        const data = loadSample();
        const ccrInline = data.inline.find((c) => c.id === 2);
        if (!ccrInline) throw new Error("missing fixture CCR inline comment");
        ccrInline.user = { login: "Copilot", type: "Bot" };
        const rows = attributePr(data, allIds(data), cfg);
        expect(byId(rows, 2).authorKind).toBe("ccr");
    });

    it("classifies a repo member as human", () => {
        expect(byId(rows, 1).authorKind).toBe("human");
    });

    it("tags inline replies as kind=reply and top-level as ask", () => {
        expect(byId(rows, 4).kind).toBe("reply");
        expect(byId(rows, 1).kind).toBe("ask");
    });

    it("tags an author-reply-phrase issue comment as reply, not ask", () => {
        const replyData: PullRequestData = {
            rawSchemaVersion: "1.0",
            pr: {
                number: 100,
                title: "t",
                author: { login: "author", type: "User" },
                url: "https://x/pull/100",
                state: "closed",
                isDraft: false,
                additions: 1,
                deletions: 0,
                createdAt: "2026-01-01T00:00:00Z",
                mergedAt: "2026-01-02T00:00:00Z",
                labels: [],
                linkedIssues: [],
            },
            reviews: [],
            inline: [],
            issue: [
                {
                    id: 900,
                    body: "Fixed, thanks for catching that.",
                    createdAt: "2026-01-01T02:00:00Z",
                    user: { login: "reviewer", type: "User" },
                    authorAssociation: "MEMBER",
                    reactions: [],
                },
            ],
            commits: [],
        };
        const replyRows = attributePr(replyData, new Set([900]), cfg);
        expect(byId(replyRows, 900).kind).toBe("reply");
    });

    it("tags review summaries as kind=summary with no line anchor", () => {
        expect(byId(rows, 50).kind).toBe("summary");
        expect(byId(rows, 50).path).toBeNull();
    });

    it("marks generated/vendored paths excluded and ineligible", () => {
        const c6 = byId(rows, 6);
        expect(c6.pathExcluded).toBe(true);
        expect(c6.ccrSawCode).toBe(false);
    });

    it("gates ccrSawCode to human asks CCR could have seen", () => {
        // CCR (id 2) reviewed at 09:00; the src/foo.ts commit lands at 11:00,
        // after the human ask, so CCR saw the version the ask anchors to.
        expect(byId(rows, 1).ccrSawCode).toBe(true);
        // replies and summaries are never eligible.
        expect(byId(rows, 4).ccrSawCode).toBe(false);
        expect(byId(rows, 50).ccrSawCode).toBe(false);
    });

    it("leaves judge verdicts null until Step 3", () => {
        const c1 = byId(rows, 1);
        expect(c1.ccrOutcome).toBeNull();
        expect(c1.ccrAddressedConcern).toBeNull();
        expect(c1.isGap).toBeNull();
    });
});

describe("attribute-comments (ccrSawCode timing)", () => {
    function makeData(opts: {
        ccrReviewTimes: string[];
        pathCommitAt: string | null;
        humanAt: string;
    }): PullRequestData {
        return {
            rawSchemaVersion: "1.0",
            pr: {
                number: 1,
                title: "t",
                author: { login: "alice", type: "User" },
                url: "https://github.com/o/r/pull/1",
                state: "closed",
                createdAt: "2026-06-01T00:00:00Z",
                mergedAt: null,
            },
            reviews: [],
            inline: [
                {
                    id: 1,
                    path: "f.ts",
                    line: 10,
                    originalLine: 10,
                    body: "please handle the error here",
                    createdAt: opts.humanAt,
                    user: { login: "bob", type: "User" },
                    authorAssociation: "MEMBER",
                },
                ...opts.ccrReviewTimes.map((t, i) => ({
                    id: 100 + i,
                    path: "f.ts",
                    line: 10,
                    originalLine: 10,
                    body: "ccr note",
                    createdAt: t,
                    user: {
                        login: "copilot-pull-request-reviewer[bot]",
                        type: "Bot" as const,
                    },
                    authorAssociation: "NONE",
                })),
            ],
            issue: [],
            commits: opts.pathCommitAt
                ? [
                      {
                          sha: "c1",
                          committedAt: opts.pathCommitAt,
                          files: ["f.ts"],
                      },
                  ]
                : [],
        };
    }

    function sawCode(data: PullRequestData): boolean {
        const ids = new Set(data.inline.map((c) => c.id));
        return byId(attributePr(data, ids, cfg), 1).ccrSawCode;
    }

    it("true when a CCR review lands at/after the commented code version", () => {
        expect(
            sawCode(
                makeData({
                    ccrReviewTimes: [
                        "2026-06-01T09:00:00Z",
                        "2026-06-01T10:30:00Z",
                    ],
                    pathCommitAt: "2026-06-01T10:00:00Z",
                    humanAt: "2026-06-01T11:00:00Z",
                }),
            ),
        ).toBe(true);
    });

    it("false when CCR only reviewed an older version of the code", () => {
        expect(
            sawCode(
                makeData({
                    ccrReviewTimes: ["2026-06-01T09:00:00Z"],
                    pathCommitAt: "2026-06-01T10:00:00Z",
                    humanAt: "2026-06-01T11:00:00Z",
                }),
            ),
        ).toBe(false);
    });

    it("false when CCR only reviewed after the human ask", () => {
        expect(
            sawCode(
                makeData({
                    ccrReviewTimes: ["2026-06-01T12:00:00Z"],
                    pathCommitAt: "2026-06-01T10:00:00Z",
                    humanAt: "2026-06-01T11:00:00Z",
                }),
            ),
        ).toBe(false);
    });

    it("false when CCR never reviewed the PR", () => {
        expect(
            sawCode(
                makeData({
                    ccrReviewTimes: [],
                    pathCommitAt: "2026-06-01T10:00:00Z",
                    humanAt: "2026-06-01T11:00:00Z",
                }),
            ),
        ).toBe(false);
    });
});

describe("findingId de-duplication", () => {
    it("collapses two comments on the same (pr, author, path, line) to one finding", () => {
        const a = makeFindingId(100, "carol", "src/bar.ts", 10, 10);
        const b = makeFindingId(100, "carol", "src/bar.ts", 10, 10);
        expect(a).toBe(b);
    });

    it("distinguishes different authors / lines", () => {
        expect(makeFindingId(100, "carol", "src/bar.ts", 10, 10)).not.toBe(
            makeFindingId(100, "dave", "src/bar.ts", 10, 10),
        );
        expect(makeFindingId(100, "carol", "src/bar.ts", 10, 10)).not.toBe(
            makeFindingId(100, "carol", "src/bar.ts", 11, 11),
        );
    });
});

describe("rowId uniqueness", () => {
    function sameLineData(): PullRequestData {
        return {
            rawSchemaVersion: "1.0",
            pr: {
                number: 100,
                title: "t",
                author: { login: "author", type: "User" },
                url: "https://x/pull/100",
                state: "closed",
                createdAt: "2026-01-01T00:00:00Z",
                mergedAt: "2026-01-02T00:00:00Z",
            },
            reviews: [
                {
                    id: 500,
                    state: "COMMENTED",
                    body: "Overall looks fine, minor asks below.",
                    submittedAt: "2026-01-01T01:00:00Z",
                    user: { login: "reviewer", type: "User" },
                    authorAssociation: "MEMBER",
                },
                {
                    id: 501,
                    state: "COMMENTED",
                    body: "Second pass, still a couple things to address.",
                    submittedAt: "2026-01-01T02:00:00Z",
                    user: { login: "reviewer", type: "User" },
                    authorAssociation: "MEMBER",
                },
            ],
            inline: [
                {
                    id: 800,
                    path: "src/bar.ts",
                    line: 10,
                    originalLine: 10,
                    body: "please validate the input here",
                    createdAt: "2026-01-01T01:05:00Z",
                    user: { login: "carol", type: "User" },
                    authorAssociation: "MEMBER",
                },
                {
                    id: 801,
                    path: "src/bar.ts",
                    line: 10,
                    originalLine: 10,
                    body: "also handle the error path on this line",
                    createdAt: "2026-01-01T01:06:00Z",
                    user: { login: "carol", type: "User" },
                    authorAssociation: "MEMBER",
                },
            ],
            issue: [],
            commits: [],
        };
    }

    it("gives two same-author/path/line comments different rowIds", () => {
        const rows = attributePr(sameLineData(), new Set([800, 801]), cfg);
        const a = byId(rows, 800);
        const b = byId(rows, 801);
        expect(a.rowId).not.toBe(b.rowId);
    });

    it("may still share findingId for those same-anchor comments", () => {
        const rows = attributePr(sameLineData(), new Set([800, 801]), cfg);
        expect(byId(rows, 800).findingId).toBe(byId(rows, 801).findingId);
    });

    it("gives pathless review summaries unique rowIds", () => {
        const rows = attributePr(sameLineData(), new Set([500, 501]), cfg);
        const a = byId(rows, 500);
        const b = byId(rows, 501);
        expect(a.path).toBeNull();
        expect(b.path).toBeNull();
        expect(a.rowId).not.toBe(b.rowId);
    });

    it("makeRowId encodes pr, source, and externalId", () => {
        expect(makeRowId(100, "inline", 800)).toBe("100:inline:800");
        expect(makeRowId(100, "review", 800)).not.toBe(
            makeRowId(100, "inline", 800),
        );
    });
});
