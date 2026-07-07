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
        expect(ccr?.authorReplies).toContain("Fixed in a later commit, thanks.");
    });
});