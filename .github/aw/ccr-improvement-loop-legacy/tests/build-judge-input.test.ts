import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { attributePr } from "../scripts/attribute-comments.ts";
import { buildJudgeInputForPr } from "../scripts/build-judge-input.ts";
import { loadConfig } from "../scripts/config.ts";
import type { PullRequestData } from "../scripts/types.ts";

const here = dirname(fileURLToPath(import.meta.url));
const fixture = JSON.parse(
    readFileSync(join(here, "fixtures", "pr-sample.json"), "utf8"),
) as PullRequestData;
const cfg = loadConfig();

describe("build-judge-input", () => {
    it("builds gap-candidate and ccr-comment evidence items", () => {
        const data = structuredClone(fixture);
        const c1 = data.commits[0];
        if (!c1) throw new Error("fixture missing commit");
        c1.patches = {
            "src/foo.ts": "@@ -38,6 +38,9 @@\n+  if (!resp) return undefined;",
        };
        const reply = data.inline.find((c) => c.id === 4);
        if (!reply) throw new Error("fixture missing reply");
        reply.inReplyToId = 2;
        data.inline.push({
            id: 7,
            path: "src/foo.ts",
            line: 43,
            originalLine: 43,
            body: "A later CCR comment should not count as a prior catch.",
            diffHunk: "@@ -38,6 +38,8 @@\n+  const v = resp.value;",
            createdAt: "2026-06-01T10:30:00Z",
            user: { login: "copilot-pull-request-reviewer[bot]", type: "Bot" },
            authorAssociation: "NONE",
        });

        const rows = attributePr(
            data,
            new Set(data.inline.map((c) => c.id)),
            cfg,
        );
        const items = buildJudgeInputForPr(data, rows, {
            maxBodyChars: 2000,
            maxDiffChars: 4000,
        });

        const gap = items.find((i) => i.purpose === "gap-candidate");
        expect(gap?.diffHunk).toContain("const v = resp.value");
        expect(gap?.ccrComments?.[0]?.body).toContain("nil response");
        expect(gap?.ccrComments).toHaveLength(1);

        const ccr = items.find((i) => i.purpose === "ccr-comment");
        expect(ccr?.postCommentDiff).toContain("if (!resp)");
        expect(ccr?.authorReplies).toContain(
            "Fixed in a later commit, thanks.",
        );
    });

    it("gives judge input items unique ids for same-line comments", () => {
        const data: PullRequestData = {
            rawSchemaVersion: "1.0",
            pr: {
                number: 200,
                title: "t",
                author: { login: "author", type: "User" },
                url: "https://x/pull/200",
                state: "closed",
                createdAt: "2026-01-01T00:00:00Z",
                mergedAt: "2026-01-02T00:00:00Z",
            },
            reviews: [],
            inline: [
                {
                    id: 810,
                    path: "src/bar.ts",
                    line: 12,
                    originalLine: 12,
                    body: "please validate the input on this line",
                    diffHunk: "@@ -10,3 +10,4 @@\n+  const x = req.body;",
                    createdAt: "2026-01-01T01:05:00Z",
                    user: { login: "carol", type: "User" },
                    authorAssociation: "MEMBER",
                },
                {
                    id: 811,
                    path: "src/bar.ts",
                    line: 12,
                    originalLine: 12,
                    body: "also handle the error path on this same line",
                    diffHunk: "@@ -10,3 +10,4 @@\n+  const x = req.body;",
                    createdAt: "2026-01-01T01:06:00Z",
                    user: { login: "carol", type: "User" },
                    authorAssociation: "MEMBER",
                },
            ],
            issue: [],
            commits: [],
        };

        const rows = attributePr(data, new Set([810, 811]), cfg);
        // Same anchor → shared findingId, but distinct rowId.
        expect(rows[0]?.findingId).toBe(rows[1]?.findingId);

        const items = buildJudgeInputForPr(data, rows, {
            maxBodyChars: 2000,
            maxDiffChars: 4000,
        });
        const ids = items.map((i) => i.id);
        expect(ids).toHaveLength(2);
        expect(new Set(ids).size).toBe(ids.length);
    });
});
