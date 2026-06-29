import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { spawnSync } from "node:child_process";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import { ensureRunDir, atomicWrite, resolveInRunDir, makeRunId, slugify, appendArtifact } from "../src/artifacts.js";

let tmp: string;

beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), "aw-test-"));
});
afterEach(() => {
    fs.rmSync(tmp, { recursive: true, force: true });
});

describe("slugify / makeRunId", () => {
    it("slugifies a task", () => {
        expect(slugify("Add Rate Limiting!! to API")).toBe("add-rate-limiting-to-api");
    });
    it("builds a timestamped run id", () => {
        const id = makeRunId("Fix bug", new Date(2026, 5, 29, 13, 10));
        expect(id).toBe("20260629-1310-fix-bug");
    });
});

describe("ensureRunDir (non-git)", () => {
    it("creates the run dir and an inner .gitignore, no crash without git", () => {
        const { dir, root } = ensureRunDir("run-1", { cwd: tmp });
        expect(fs.existsSync(dir)).toBe(true);
        const inner = fs.readFileSync(path.join(root, ".gitignore"), "utf8");
        expect(inner).toContain("*");
        expect(inner).toContain("!.gitignore");
    });
});

describe("ensureRunDir (git)", () => {
    it("adds the scratch root to .git/info/exclude without touching tracked .gitignore", () => {
        spawnSync("git", ["init", "-q"], { cwd: tmp });
        const { root } = ensureRunDir("run-1", { cwd: tmp });
        const exclude = fs.readFileSync(path.join(tmp, ".git", "info", "exclude"), "utf8");
        expect(exclude).toContain(".agentic-workflow/");
        // tracked root .gitignore is NOT created/modified
        expect(fs.existsSync(path.join(tmp, ".gitignore"))).toBe(false);
        // idempotent
        ensureRunDir("run-2", { cwd: tmp });
        const exclude2 = fs.readFileSync(path.join(tmp, ".git", "info", "exclude"), "utf8");
        expect(exclude2.match(/\.agentic-workflow\//g)).toHaveLength(1);
        void root;
    });
});

describe("atomicWrite / appendArtifact", () => {
    it("writes and appends", () => {
        const f = path.join(tmp, "a", "b.md");
        atomicWrite(f, "hello");
        expect(fs.readFileSync(f, "utf8")).toBe("hello");
        appendArtifact(f, "\nmore");
        expect(fs.readFileSync(f, "utf8")).toBe("hello\nmore");
    });
});

describe("resolveInRunDir", () => {
    it("resolves a safe relative path", () => {
        const p = resolveInRunDir(tmp, "specs/architecture.md");
        expect(p).toBe(path.join(tmp, "specs", "architecture.md"));
    });
    it("rejects traversal and absolute paths", () => {
        expect(() => resolveInRunDir(tmp, "../escape.md")).toThrow(/escapes/);
        expect(() => resolveInRunDir(tmp, "/etc/passwd")).toThrow(/escapes/);
        expect(() => resolveInRunDir(tmp, "a/../../b")).toThrow(/escapes/);
    });
});
