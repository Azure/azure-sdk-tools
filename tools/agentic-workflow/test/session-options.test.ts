import { describe, it, expect, beforeEach, afterEach } from "vitest";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import { makePreToolUseHook, redact, logEvent, makeWriteArtifactTool } from "../src/session-options.js";

let tmp: string;
beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), "aw-so-"));
});
afterEach(() => {
    fs.rmSync(tmp, { recursive: true, force: true });
});

describe("onPreToolUse policy", () => {
    const hook = (readOnly: boolean) => makePreToolUseHook({ readOnly, logPath: path.join(tmp, "log.jsonl") });

    it("denies shell/edit tools in a read-only phase", () => {
        const h = hook(true);
        for (const tool of ["shell", "bash", "str_replace", "create_file", "edit_file", "delete_path"]) {
            expect(h({ toolName: tool }).permissionDecision).toBe("deny");
        }
    });

    it("allows read tools and write_artifact in a read-only phase", () => {
        const h = hook(true);
        for (const tool of ["view", "grep", "glob", "read_file", "write_artifact"]) {
            expect(h({ toolName: tool }).permissionDecision).toBe("allow");
        }
    });

    it("allows mutating tools in an implement (non-read-only) phase", () => {
        const h = hook(false);
        expect(h({ toolName: "shell" }).permissionDecision).toBe("allow");
        expect(h({ toolName: "str_replace" }).permissionDecision).toBe("allow");
    });
});

describe("redaction", () => {
    it("masks common secret shapes", () => {
        expect(redact("token ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ012345")).toContain("[REDACTED_GH_TOKEN]");
        expect(redact("key sk-ABCDEFGHIJKLMNOPQRSTUV")).toContain("[REDACTED_API_KEY]");
        expect(redact('password: "supersecretvalue"')).toContain("[REDACTED]");
    });
    it("logEvent writes redacted JSONL", () => {
        const log = path.join(tmp, "log.jsonl");
        logEvent(log, { kind: "x", token: "ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ012345" });
        const line = fs.readFileSync(log, "utf8");
        expect(line).toContain("[REDACTED_GH_TOKEN]");
        expect(JSON.parse(line)).toMatchObject({ kind: "x" });
    });
});

describe("write_artifact tool", () => {
    it("writes within the run dir and rejects traversal", async () => {
        const tool = makeWriteArtifactTool(tmp, path.join(tmp, "log.jsonl"));
        const handler = tool.handler as (a: unknown) => { content: { text: string }[] };
        handler({ path: "specs/a.md", content: "hi" });
        expect(fs.readFileSync(path.join(tmp, "specs", "a.md"), "utf8")).toBe("hi");
        const bad = handler({ path: "../escape.md", content: "x" });
        expect(bad.content[0].text).toContain("escapes run dir");
    });
});
