import { describe, expect, it } from "vitest";

import { classifyPr } from "../scripts/classify-pr.ts";
import type { PullRequestMetadata } from "../scripts/types.ts";

function meta(over: Partial<PullRequestMetadata>): PullRequestMetadata {
    return {
        number: 1,
        title: "some change",
        author: { login: "x", type: "User" },
        url: "u",
        state: "closed",
        createdAt: null,
        mergedAt: null,
        labels: [],
        linkedIssues: [],
        ...over,
    };
}

describe("classify-pr", () => {
    it("maps a bug label to bug-fix with source=label", () => {
        const c = classifyPr(meta({ labels: ["bug"] }));
        expect(c).toEqual({
            prType: "bug-fix",
            prTypeSource: "label",
            classificationStatus: "complete",
        });
    });

    it("maps a Conventional-Commit feat: title to feature with source=title", () => {
        const c = classifyPr(meta({ title: "feat(api): add retry" }));
        expect(c.prType).toBe("feature");
        expect(c.prTypeSource).toBe("title");
    });

    it("handles a breaking-change ! marker in the title prefix", () => {
        const c = classifyPr(meta({ title: "fix!: drop deprecated field" }));
        expect(c.prType).toBe("bug-fix");
        expect(c.prTypeSource).toBe("title");
    });

    it("falls back to linked-issue labels with source=issue", () => {
        const c = classifyPr(
            meta({
                title: "update things",
                linkedIssues: [{ number: 9, labels: ["regression"] }],
            }),
        );
        expect(c.prType).toBe("bug-fix");
        expect(c.prTypeSource).toBe("issue");
    });

    it("prefers label over title over issue (precedence)", () => {
        const c = classifyPr(
            meta({
                labels: ["documentation"],
                title: "feat: x",
                linkedIssues: [{ number: 9, labels: ["bug"] }],
            }),
        );
        expect(c.prType).toBe("docs");
        expect(c.prTypeSource).toBe("label");
    });

    it("returns needs-agent with null prType when no deterministic signal", () => {
        const c = classifyPr(meta({ title: "update the widget internals" }));
        expect(c.prType).toBeNull();
        expect(c.prTypeSource).toBe("unknown");
        expect(c.classificationStatus).toBe("needs-agent");
    });

    it("never produces a fake 'agent' PR type", () => {
        const c = classifyPr(meta({ title: "misc" }));
        expect(c.prType).not.toBe("agent");
    });
});
