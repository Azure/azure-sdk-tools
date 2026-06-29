/**
 * harness.ts — *** the ONLY file that imports @github/copilot-sdk ***
 *
 * It wraps `createSession` with the shared session options (write_artifact tool, onPreToolUse
 * read-only policy, onErrorOccurred retry, event -> execution-log.jsonl capture) and exposes a
 * single stable internal interface, `Harness.runPhase`, that the orchestrator talks to. Harness
 * churn is contained here: re-point this file at a new SDK/primitive and the orchestrator is
 * unchanged.
 */
import { CopilotClient, approveAll } from "@github/copilot-sdk";
import type { PhaseRunResult } from "./types.js";
import {
    ensureLog,
    logEvent,
    makeErrorHook,
    makePreToolUseHook,
    makeWriteArtifactTool,
    redact,
} from "./session-options.js";

export interface PhaseRequest {
    /** Stable label for the log (e.g. "research", "plan", "implement:stage-1", "critique"). */
    label: string;
    prompt: string;
    runDir: string;
    /** Path to the run's execution-log.jsonl. */
    logPath: string;
    /** Read-only phases deny mutating/shell tools. Implement stages are NOT read-only. */
    readOnly: boolean;
    model?: string;
    /** Hard ceiling on a single phase session (ms). */
    timeoutMs?: number;
}

/** The stable seam between orchestrator and the SDK. Test doubles implement this. */
export interface Harness {
    runPhase(req: PhaseRequest): Promise<PhaseRunResult>;
    stop(): Promise<void>;
}

const DEFAULT_TIMEOUT_MS = 15 * 60 * 1000;

export class SdkHarness implements Harness {
    private client: CopilotClient | undefined;

    constructor(private readonly defaults: { workingDirectory?: string; stream?: boolean } = {}) {}

    private async getClient(): Promise<CopilotClient> {
        if (!this.client) {
            this.client = new CopilotClient({ workingDirectory: this.defaults.workingDirectory });
            await this.client.start();
        }
        return this.client;
    }

    async runPhase(req: PhaseRequest): Promise<PhaseRunResult> {
        ensureLog(req.logPath);
        const client = await this.getClient();
        logEvent(req.logPath, { kind: "phase_start", label: req.label, readOnly: req.readOnly, model: req.model });

        const streaming = this.defaults.stream === true;
        const write = (s: string) => process.stderr.write(s);
        if (streaming) {
            write(`\n=== ${req.label}${req.model ? ` (model: ${req.model})` : ""} ===\n`);
        }

        const session = await client.createSession({
            model: req.model,
            streaming,
            onPermissionRequest: approveAll,
            tools: [makeWriteArtifactTool(req.runDir, req.logPath)],
            hooks: {
                onPreToolUse: makePreToolUseHook({ readOnly: req.readOnly, logPath: req.logPath }),
                onErrorOccurred: makeErrorHook(req.logPath),
            },
        });

        let finalText = "";
        let toolCalls = 0;
        let midLine = false;
        session.on("assistant.message", (e) => {
            finalText += e.data?.content ?? "";
        });
        if (streaming) {
            session.on("assistant.message_delta", (e) => {
                const chunk = e.data?.deltaContent ?? "";
                if (chunk) {
                    write(chunk);
                    midLine = !chunk.endsWith("\n");
                }
            });
            session.on("tool.execution_start", (e) => {
                if (midLine) {
                    write("\n");
                    midLine = false;
                }
                write(`  [tool] ${e.data?.toolName ?? "?"}\n`);
            });
        }
        session.on("tool.execution_start", (e) => {
            logEvent(req.logPath, { kind: "tool_start", toolName: e.data?.toolName, toolCallId: e.data?.toolCallId });
        });
        session.on("tool.execution_complete", (e) => {
            toolCalls += 1;
            logEvent(req.logPath, { kind: "tool_complete", success: e.data?.success, toolCallId: e.data?.toolCallId });
        });

        const timeoutMs = req.timeoutMs ?? DEFAULT_TIMEOUT_MS;
        try {
            await new Promise<void>((resolve, reject) => {
                const timer = setTimeout(() => reject(new Error(`phase "${req.label}" timed out`)), timeoutMs);
                session.on("session.idle", () => {
                    clearTimeout(timer);
                    resolve();
                });
                session.on("session.error", (e) => {
                    logEvent(req.logPath, { kind: "session_error", detail: redact(JSON.stringify(e.data ?? {})) });
                });
                session.send({ prompt: req.prompt }).catch(reject);
            });
        } finally {
            await session.disconnect().catch(() => {});
        }

        if (streaming) {
            if (midLine) {
                write("\n");
            }
            write(`  [done] ${req.label} — ${toolCalls} tool call(s)\n`);
        }
        logEvent(req.logPath, { kind: "phase_end", label: req.label, toolCalls });
        return { artifacts: [], finalText };
    }

    async stop(): Promise<void> {
        if (this.client) {
            await this.client.stop().catch(() => {});
            this.client = undefined;
        }
    }
}
