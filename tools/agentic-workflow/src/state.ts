/**
 * Run state persistence. `state.json` is written atomically after every phase so `resume` can
 * continue from the last completed phase. No content-hash revalidation engine yet (deferred per
 * thinnerplan §1) — resume re-runs from the last completed phase.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { atomicWrite } from "./artifacts.js";
import type { PhaseStatus, RunState } from "./types.js";

export const PHASE_ORDER = ["research", "assumptions", "classify", "research-item", "plan", "implement"] as const;

export function statePath(runDir: string): string {
    return path.join(runDir, "state.json");
}

export function loadState(runDir: string): RunState | undefined {
    const p = statePath(runDir);
    if (!fs.existsSync(p)) {
        return undefined;
    }
    try {
        return JSON.parse(fs.readFileSync(p, "utf8")) as RunState;
    } catch {
        return undefined;
    }
}

export function initState(
    runDir: string,
    init: Omit<RunState, "schemaVersion" | "phases" | "createdAt" | "updatedAt">,
): RunState {
    const now = new Date().toISOString();
    const state: RunState = {
        schemaVersion: 1,
        phases: Object.fromEntries(PHASE_ORDER.map((p) => [p, "pending"])) as Record<string, PhaseStatus>,
        createdAt: now,
        updatedAt: now,
        ...init,
    };
    saveState(runDir, state);
    return state;
}

export function saveState(runDir: string, state: RunState): void {
    state.updatedAt = new Date().toISOString();
    atomicWrite(statePath(runDir), JSON.stringify(state, null, 2));
}

export function setPhase(runDir: string, state: RunState, phase: string, status: PhaseStatus): void {
    state.phases[phase] = status;
    saveState(runDir, state);
}
