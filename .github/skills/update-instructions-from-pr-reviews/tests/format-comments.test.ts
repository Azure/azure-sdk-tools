import { describe, it } from "vitest";
import assert from "node:assert/strict";

import {
    extractChangedLines,
    formatComment,
    formatHunk,
    parseCli,
} from "../scripts/format-comments.ts";
import type { KeptComment } from "../scripts/types.ts";

describe("format-comments helpers", () => {
    it("parses CLI defaults and flags", () => {
        const opts = parseCli([
            "--input",
            "kept.json",
            "--no-hunk",
            "--max-hunk-lines",
            "5",
        ]);

        assert.equal(opts.input, "kept.json");
        assert.equal(opts.includeHunk, false);
        assert.equal(opts.maxHunkLines, 5);
    });

    it("extracts only changed lines and truncates", () => {
        const lines = extractChangedLines(
            [
                "@@ -1,4 +1,5 @@",
                " context",
                "- old one",
                "+ new one",
                " unchanged",
                "- old two",
                "+ new two",
            ].join("\n"),
            3,
        );

        assert.deepEqual(lines, [
            "- old one",
            "+ new one",
            "- old two",
            "… (1 more lines)",
        ]);
    });

    it("formats hunk blocks as quoted markdown", () => {
        const block = formatHunk("- old\n+ new", true, 10);
        assert.equal(block, "> - old\n> + new\n\n");
        assert.equal(formatHunk("- old\n+ new", false, 10), "");
    });

    it("formats a comment with location, hunk, and body", () => {
        const comment: KeptComment = {
            pr: 42,
            url: "https://github.com/owner/repo/pull/42",
            id: undefined,
            comment_url: undefined,
            source: "inline",
            kind: "ask",
            user: "carol",
            path: "src/service.ts",
            line: 12,
            diff_hunk: '- return err\n+ return fmt.Errorf("oops: %w", err)',
            body: "Wrap this with %w.",
        };

        const out = formatComment(comment, true, 10);
        assert.match(out, /^### \[carol\] src\/service\.ts:12/);
        assert.match(out, /> - return err/);
        assert.match(out, /Wrap this with %w\./);
    });

    it("formats comments without location cleanly", () => {
        const comment: KeptComment = {
            pr: 42,
            url: undefined,
            id: undefined,
            comment_url: undefined,
            source: "review",
            kind: "summary",
            user: "jongio",
            path: undefined,
            line: undefined,
            diff_hunk: undefined,
            body: "Top-level summary feedback.",
        };

        assert.equal(
            formatComment(comment, true, 10),
            "### [jongio]\n\nTop-level summary feedback.",
        );
    });
});
