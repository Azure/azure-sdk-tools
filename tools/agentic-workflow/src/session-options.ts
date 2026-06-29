/**
 * Shared session wiring: the `write_artifact` custom tool, the `onPreToolUse` read-only policy,
 * the `onErrorOccurred` retry hook, and event -> `execution-log.jsonl` capture (with trivial
 * secret redaction baked in from the start).
 *
 * This module builds plain config fragments; it does NOT import the SDK directly so it can be
 * unit-tested. `harness.ts` is the only file that imports `@github/copilot-sdk` and stitches
 * these fragments into a real session.
 */
import { defineTool, type Tool } from "@github/copilot-sdk";
import * as fs from "node:fs";
import { appendArtifact, atomicWrite, resolveInRunDir } from "./artifacts.js";

/** Tool-name fragments that mutate state or execute code. Denied in read-only phases. */
const MUTATING_TOOL_RE =
    /(shell|bash|process|execute|run_command|str_replace|apply_patch|create_file|write_file|edit_file|delete|remove|^edit$|^create$|^write$)/i;

/** Trivial redaction: mask obvious secret-looking tokens before they hit the log. */
export function redact(text: string): string {
    if (!text) {
        return text;
    }
    return text
        .replace(/\bgh[pousr]_[A-Za-z0-9]{20,}\b/g, "[REDACTED_GH_TOKEN]")
        .replace(/\bsk-[A-Za-z0-9]{20,}\b/g, "[REDACTED_API_KEY]")
        .replace(/\bAKIA[0-9A-Z]{16}\b/g, "[REDACTED_AWS_KEY]")
        .replace(/\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b/g, "[REDACTED_JWT]")
        .replace(/(?<=(?:password|secret|token|api[_-]?key)["'\s:=]{1,4})(?!\[REDACTED)[^\s"']{8,}/gi, "[REDACTED]");
}

/** Append one structured, redacted JSONL record to the run's execution log. */
export function logEvent(logPath: string, record: Record<string, unknown>): void {
    const line = redact(JSON.stringify({ ts: new Date().toISOString(), ...record })) + "\n";
    appendArtifact(logPath, line);
}

/**
 * Build the `write_artifact` tool bound to a run dir. It is the only sanctioned write path for
 * read-only phases (path-traversal guarded). Supports append mode for `execution-log.md` /
 * `handoff.md` in the implement phase.
 */
export function makeWriteArtifactTool(runDir: string, logPath: string): Tool {
    return defineTool("write_artifact", {
        description:
            "Persist a workflow artifact to the run directory. This is the ONLY way to write " +
            "specs/notes/plan/log files. `path` is relative to the run dir.",
        parameters: {
            type: "object",
            properties: {
                path: { type: "string", description: "Relative path within the run dir, e.g. specs/architecture.md" },
                content: { type: "string", description: "Full file content (or text to append when append=true)" },
                append: { type: "boolean", description: "Append instead of overwrite (for logs/handoff)" },
            },
            required: ["path", "content"],
        },
        skipPermission: true,
        handler: (args: unknown) => {
            const {
                path: rel,
                content,
                append,
            } = (args ?? {}) as {
                path?: string;
                content?: string;
                append?: boolean;
            };
            if (typeof rel !== "string" || typeof content !== "string") {
                return {
                    content: [{ type: "text", text: "write_artifact: `path` and `content` are required strings" }],
                };
            }
            let abs: string;
            try {
                abs = resolveInRunDir(runDir, rel);
            } catch (e) {
                return { content: [{ type: "text", text: (e as Error).message }] };
            }
            if (append) {
                appendArtifact(abs, content.endsWith("\n") ? content : content + "\n");
            } else {
                atomicWrite(abs, content);
            }
            logEvent(logPath, { kind: "artifact_write", path: rel, append: !!append, bytes: content.length });
            return { content: [{ type: "text", text: `wrote ${rel} (${content.length} bytes)` }] };
        },
    });
}

export interface PolicyOptions {
    /** Read-only phases deny mutating/shell tools via onPreToolUse. */
    readOnly: boolean;
    logPath: string;
}

/** Build the `onPreToolUse` hook enforcing the per-phase policy (deny in read-only phases). */
export function makePreToolUseHook(opts: PolicyOptions) {
    return (input: { toolName: string; toolArgs?: unknown }) => {
        const name = input.toolName ?? "";
        if (name === "write_artifact") {
            return { permissionDecision: "allow" as const };
        }
        if (opts.readOnly && MUTATING_TOOL_RE.test(name)) {
            logEvent(opts.logPath, { kind: "policy_deny", tool: name, reason: "read-only phase" });
            return {
                permissionDecision: "deny" as const,
                permissionDecisionReason:
                    "This phase is read-only. Use the write_artifact tool to persist artifacts; " +
                    "source edits and shell are not permitted here.",
            };
        }
        return { permissionDecision: "allow" as const };
    };
}

/** Build the `onErrorOccurred` hook: retry transient model/tool errors, abort on the rest. */
export function makeErrorHook(logPath: string) {
    return (input: { errorContext?: string; error?: unknown }) => {
        const transient = input.errorContext === "model_call" || input.errorContext === "tool_execution";
        logEvent(logPath, { kind: "error", context: input.errorContext, transient });
        return { errorHandling: (transient ? "retry" : "abort") as "retry" | "abort" };
    };
}

/** Ensure a log file exists so appends start from a known place. */
export function ensureLog(logPath: string): void {
    if (!fs.existsSync(logPath)) {
        atomicWrite(logPath, "");
    }
}
