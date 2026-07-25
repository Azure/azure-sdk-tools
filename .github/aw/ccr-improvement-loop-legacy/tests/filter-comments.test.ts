import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import {
    classifyComment,
    filterPullRequestData,
    inferCommentKind,
    isQuotedOnly,
} from "../scripts/filter-comments.ts";
import type { FilterOpts, PullRequestData } from "../scripts/types.ts";

const here = dirname(fileURLToPath(import.meta.url));

const opts: FilterOpts = {
    includeSelf: false,
    minLength: 20,
    defaultBots: true,
    ccrLogins: new Set(["copilot-pull-request-reviewer[bot]", "copilot"]),
    automationLogins: new Set(["dependabot[bot]", "azure-sdk"]),
};

describe("filter-comments", () => {
    const data = JSON.parse(
        readFileSync(join(here, "fixtures", "pr-sample.json"), "utf8"),
    ) as PullRequestData;

    it("keeps CCR comments (subject of usefulness metrics)", () => {
        const verdict = classifyComment({
            comment: {
                body: "guard nil",
                user: {
                    login: "copilot-pull-request-reviewer[bot]",
                    type: "Bot",
                },
                authorAssociation: "NONE",
            },
            prAuthor: "alice",
            opts,
        });
        expect(verdict).toBe("keep");
    });

    it("keeps Copilot inline comments whose login is not bracketed as a bot", () => {
        const verdict = classifyComment({
            comment: {
                body: "This inline code review comment is long enough to keep.",
                user: { login: "Copilot", type: "Bot" },
                authorAssociation: "NONE",
            },
            prAuthor: "alice",
            opts,
        });
        expect(verdict).toBe("keep");
    });

    it("drops non-CCR automation accounts", () => {
        const verdict = classifyComment({
            comment: {
                body: "Bumps lodash from 1 to 2.",
                user: { login: "dependabot[bot]", type: "Bot" },
                authorAssociation: "NONE",
            },
            prAuthor: "alice",
            opts,
        });
        expect(verdict).toBe("automation");
    });

    it("drops external (non OWNER/MEMBER/COLLABORATOR) feedback", () => {
        const verdict = classifyComment({
            comment: {
                body: "this is a long enough drive-by external comment body",
                user: { login: "stranger", type: "User" },
                authorAssociation: "CONTRIBUTOR",
            },
            prAuthor: "alice",
            opts,
        });
        expect(verdict).toBe("association");
    });

    it("drops short / boilerplate comments", () => {
        expect(
            classifyComment({
                comment: {
                    body: "+1",
                    user: { login: "bob", type: "User" },
                    authorAssociation: "MEMBER",
                },
                prAuthor: "alice",
                opts,
            }),
        ).toBe("short");
    });

    it("tags review bodies as summary and acks as reply", () => {
        expect(inferCommentKind("review", "Looks good")).toBe("summary");
        expect(inferCommentKind("inline", "Fixed, thanks")).toBe("reply");
        expect(inferCommentKind("inline", "Please add a null check here")).toBe(
            "ask",
        );
    });

    it("detects quoted-only bodies", () => {
        expect(isQuotedOnly("> just a quote\n> more quote")).toBe(true);
        expect(isQuotedOnly("> quote\nreal reply")).toBe(false);
    });

    it("filters the fixture PR end to end (drops noise, keeps signal)", () => {
        const result = filterPullRequestData(data, opts);
        const kept = result.kept;
        // dependabot + the "+1" issue comment are dropped.
        expect(kept.some((c) => c.user === "dependabot[bot]")).toBe(false);
        expect(kept.some((c) => c.body === "+1")).toBe(false);
        // CCR + human substantive comments survive.
        expect(
            kept.some((c) => c.user === "copilot-pull-request-reviewer[bot]"),
        ).toBe(true);
        expect(kept.some((c) => c.user === "carol")).toBe(true);
        expect(result.dropped.automation).toBeGreaterThanOrEqual(1);
    });
});
