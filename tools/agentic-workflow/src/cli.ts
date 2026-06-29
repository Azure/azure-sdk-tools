#!/usr/bin/env node
/**
 * cli.ts — the primary, headless entry point (thinnerplan T3.1).
 *
 * Commands:
 *   run [task] [--simple] [--no-judge] [--judge-model <m>] [--out <dir>] [--run-id <id>]
 *   resume [run-id] [--out <dir>]
 *
 * Exit codes: 0 done / 10 paused / 1 fail / 2 usage.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { ARTIFACT_ROOT, repoRoot } from "./artifacts.js";
import { SdkHarness } from "./harness.js";
import { runWorkflow, type RunResult } from "./orchestrator.js";
import { loadState } from "./state.js";
import type { RunOptions } from "./types.js";

interface ParsedArgs {
    command: string;
    positional: string[];
    flags: Record<string, string | boolean>;
}

function parseArgs(argv: string[]): ParsedArgs {
    const [command = "run", ...rest] = argv;
    const positional: string[] = [];
    const flags: Record<string, string | boolean> = {};
    for (let i = 0; i < rest.length; i++) {
        const a = rest[i];
        if (a.startsWith("--")) {
            const key = a.slice(2);
            const next = rest[i + 1];
            if (next !== undefined && !next.startsWith("--")) {
                flags[key] = next;
                i++;
            } else {
                flags[key] = true;
            }
        } else {
            positional.push(a);
        }
    }
    return { command, positional, flags };
}

const USAGE = `agentic-workflow — research -> plan -> implement, one fresh session per phase

Usage:
  agentic-workflow run "<task>" [--simple] [--no-judge] [--judge-model <m>] [--out <dir>] [--run-id <id>]
  agentic-workflow resume [<run-id>] [--out <dir>]

Options:
  --simple              Skip research, classify, per-item research (assumptions -> plan -> implement).
  --no-judge            Disable the reflexive critique/revise judge loop (on by default).
  --judge-model <m>     Alternate model used for the critique session.
  --out <dir>           Working-dir root (default: ./.agentic-workflow).
  --run-id <id>         Explicit run id (default: timestamp + task slug).

Exit codes: 0 done, 10 paused (resume/clarify), 1 failure, 2 usage.`;

function findResumableRun(outRoot: string): string[] {
    if (!fs.existsSync(outRoot)) {
        return [];
    }
    const incomplete: string[] = [];
    for (const entry of fs.readdirSync(outRoot, { withFileTypes: true })) {
        if (!entry.isDirectory()) continue;
        const state = loadState(path.join(outRoot, entry.name));
        if (state && Object.values(state.phases).some((s) => s !== "completed")) {
            incomplete.push(entry.name);
        }
    }
    return incomplete;
}

async function main(): Promise<number> {
    const { command, positional, flags } = parseArgs(process.argv.slice(2));

    if (command === "help" || flags.help) {
        console.log(USAGE);
        return 0;
    }

    const outRoot = typeof flags.out === "string" ? flags.out : undefined;
    const resolvedRoot = path.resolve(repoRoot(), outRoot ?? ARTIFACT_ROOT);

    let opts: RunOptions;
    if (command === "run") {
        const task = positional.join(" ").trim();
        if (!task) {
            console.error("error: `run` requires a task description.\n\n" + USAGE);
            return 2;
        }
        opts = {
            task,
            simple: flags.simple === true,
            judge: flags["no-judge"] !== true,
            judgeModel: typeof flags["judge-model"] === "string" ? flags["judge-model"] : undefined,
            outRoot,
            runId: typeof flags["run-id"] === "string" ? flags["run-id"] : undefined,
        };
    } else if (command === "resume") {
        let runId = positional[0];
        if (!runId) {
            const candidates = findResumableRun(resolvedRoot);
            if (candidates.length === 0) {
                console.error("error: no incomplete runs to resume under " + resolvedRoot);
                return 2;
            }
            if (candidates.length > 1) {
                console.error("error: multiple incomplete runs — pass an explicit run-id:");
                for (const c of candidates) {
                    const st = loadState(path.join(resolvedRoot, c));
                    const last = st
                        ? Object.entries(st.phases)
                              .filter(([, v]) => v === "completed")
                              .map(([k]) => k)
                              .pop()
                        : "?";
                    console.error(`  ${c}  (task: ${st?.task ?? "?"}, last completed: ${last ?? "none"})`);
                }
                return 2;
            }
            runId = candidates[0];
        }
        const state = loadState(path.join(resolvedRoot, runId));
        if (!state) {
            console.error(`error: no run state found for "${runId}" under ${resolvedRoot}`);
            return 2;
        }
        opts = {
            task: state.task,
            simple: state.simple,
            judge: state.judge,
            judgeModel: state.judgeModel,
            outRoot,
            runId,
        };
    } else {
        console.error(`error: unknown command "${command}".\n\n` + USAGE);
        return 2;
    }

    const harness = new SdkHarness({ workingDirectory: process.cwd() });
    let result: RunResult;
    try {
        result = await runWorkflow(opts, harness);
    } finally {
        await harness.stop();
    }

    console.log(`\n${result.message}`);
    console.log(`run-id: ${result.runId}`);
    console.log(`artifacts: ${result.runDir}`);
    return result.exitCode;
}

main()
    .then((code) => process.exit(code))
    .catch((err) => {
        console.error(err);
        process.exit(1);
    });
