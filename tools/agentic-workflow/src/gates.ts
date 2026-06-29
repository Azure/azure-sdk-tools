/**
 * Parse the machine-readable `stages:` gate block from a generated `plan.md`.
 *
 * The orchestrator does NOT run the gate commands (thinnerplan §3): the implement agent runs them
 * in-session and reports pass/fail. This module only *extracts and shapes* the stages so the
 * orchestrator knows how many stages exist, their ids, expected files, and the commands the agent
 * is expected to have run.
 */
import { parse as parseYaml } from "yaml";
import type { Stage } from "./types.js";

/** Extract the first fenced ```yaml block that contains a top-level `stages:` key. */
export function extractGateBlock(planMarkdown: string): string | undefined {
    const fenceRe = /```ya?ml\s*\n([\s\S]*?)```/g;
    let m: RegExpExecArray | null;
    while ((m = fenceRe.exec(planMarkdown)) !== null) {
        const body = m[1];
        if (/^\s*stages\s*:/m.test(body)) {
            return body;
        }
    }
    return undefined;
}

/**
 * Parse plan stages. Throws a readable error if the block is missing or malformed so the plan
 * phase can be retried in a fresh session.
 */
export function parsePlanStages(planMarkdown: string): Stage[] {
    const block = extractGateBlock(planMarkdown);
    if (!block) {
        throw new Error("plan.md has no machine-readable ```yaml stages: block");
    }
    let doc: unknown;
    try {
        doc = parseYaml(block);
    } catch (e) {
        throw new Error(`plan.md gate block is not valid YAML: ${(e as Error).message}`);
    }
    const stagesRaw = (doc as { stages?: unknown })?.stages;
    if (!Array.isArray(stagesRaw) || stagesRaw.length === 0) {
        throw new Error("plan.md gate block must contain a non-empty `stages` array");
    }

    return stagesRaw.map((raw, i) => normalizeStage(raw, i));
}

function normalizeStage(raw: unknown, index: number): Stage {
    const s = (raw ?? {}) as Record<string, unknown>;
    const id = typeof s.id === "string" && s.id.trim() ? s.id.trim() : `stage-${index + 1}`;
    const gateRaw = (s.gate ?? {}) as Record<string, unknown>;
    const commands = Array.isArray(gateRaw.commands)
        ? gateRaw.commands.filter((c): c is string => typeof c === "string")
        : [];
    if (commands.length === 0) {
        throw new Error(`stage "${id}" has no gate.commands`);
    }
    const steps = Array.isArray(s.steps)
        ? s.steps.map((st) => {
              const so = (st ?? {}) as Record<string, unknown>;
              return {
                  id: typeof so.id === "string" ? so.id : "",
                  description: typeof so.description === "string" ? so.description : "",
              };
          })
        : [];

    return {
        id,
        expected_files: toStringArray(s.expected_files),
        context_needed: toStringArray(s.context_needed),
        steps,
        gate: {
            id: typeof gateRaw.id === "string" ? gateRaw.id : `gate-${index + 1}`,
            commands,
            expected: typeof gateRaw.expected === "string" ? gateRaw.expected : "exit_code_0",
        },
    };
}

function toStringArray(v: unknown): string[] {
    return Array.isArray(v) ? v.filter((x): x is string => typeof x === "string") : [];
}
