/**
 * Shared types for the agentic workflow thin spine.
 * Kept dependency-free and small on purpose — the durable contract lives here.
 */

export type PhaseId = "research" | "assumptions" | "classify" | "research-item" | "plan" | "implement";

export type PhaseStatus = "pending" | "in_progress" | "failed" | "completed";

export interface SubItem {
    id: string;
    type: "feature" | "bug" | "refactor";
    title: string;
    description: string;
    rationale?: string;
    dependsOn: string[];
    expectedFilesOrAreas?: string[];
    acceptanceCriteria?: string[];
    nonGoals?: string[];
    overlapRisk: "low" | "medium" | "high";
}

export interface SubItems {
    task: string;
    classification: "feature" | "bug" | "refactor" | "mixed";
    items: SubItem[];
}

export interface GateSpec {
    id: string;
    commands: string[];
    expected: string;
}

export interface Stage {
    id: string;
    expected_files: string[];
    context_needed: string[];
    steps: { id: string; description: string }[];
    gate: GateSpec;
}

export interface ValidationResult {
    ok: boolean;
    errors: string[];
}

/** Persisted run state (state.json), written atomically after each phase. */
export interface RunState {
    schemaVersion: 1;
    runId: string;
    task: string;
    simple: boolean;
    judge: boolean;
    judgeModel?: string;
    phases: Record<string, PhaseStatus>;
    items?: Record<string, PhaseStatus>;
    createdAt: string;
    updatedAt: string;
}

/** Result of running a single phase through the harness adapter. */
export interface PhaseRunResult {
    artifacts: string[];
    /** Final assistant text, used to read agent-reported gate/stage results. */
    finalText: string;
}

export interface RunOptions {
    task: string;
    runId?: string;
    simple?: boolean;
    judge?: boolean;
    judgeModel?: string;
    outRoot?: string;
    concurrency?: number;
    maxRetries?: number;
    /** Inject a harness for testing; defaults to the real SDK harness. */
    dryRun?: boolean;
}
