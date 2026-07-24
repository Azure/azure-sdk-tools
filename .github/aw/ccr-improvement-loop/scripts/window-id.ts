/**
 * window-id.ts — the canonical window identity used everywhere a run/window must
 * be named or de-duplicated.
 *
 * `computeWindowId` is the single encoding reused for run records (`run.id`), the
 * emitted artifact filename (`run-<windowId>.json`), and — in later phases —
 * ledger records, skip entries, and cache keys. Encoding **all** identity-bearing
 * inputs (repo, window bounds, cohort, and the raw-fetch schema version) into one
 * stable string is what makes completion/idempotency well-defined: two runs that
 * differ in any of those inputs get distinct ids and never collide, while two runs
 * of the exact same work share an id and supersede cleanly.
 *
 * Pure, no IO — safe to import from browser-free Node scripts and tests.
 */
import { RAW_SCHEMA_VERSION } from "./run-schema.ts";

/**
 * A run's PR-selection cohort. `"uncapped"` is the default full-cohort run;
 * `"cap-<N>"` is an opt-in per-window PR cap. A capped run is a DIFFERENT window
 * identity than an uncapped run over the same repo/dates (they measure different
 * PR sets), so a capped run must never supersede an uncapped one.
 */
export type Cohort = "uncapped" | `cap-${number}`;

/** Split "owner/repo" into parts, rejecting anything malformed. */
export function ownerRepoOf(repo: string): { owner: string; repo: string } {
    const [owner, name] = repo.split("/");
    if (!owner || !name) {
        throw new Error(`--repo must be "owner/repo", got "${repo}"`);
    }
    return { owner, repo: name };
}

/** Derive the cohort token from a `--max-prs` value (0/negative ⇒ uncapped). */
export function cohortOf(maxPrs: number): Cohort {
    return Number.isFinite(maxPrs) && maxPrs > 0
        ? (`cap-${String(Math.trunc(maxPrs))}` as Cohort)
        : "uncapped";
}

export interface WindowIdInput {
    /** "owner/repo". */
    repo: string;
    /** Window start, YYYY-MM-DD. */
    windowStart: string;
    /** Window end (settle cutoff), YYYY-MM-DD. */
    windowEnd: string;
    /** PR-selection cohort. */
    cohort: Cohort;
    /** Raw-fetch schema version; defaults to the current RAW_SCHEMA_VERSION. */
    rawSchemaVersion?: string;
}

/**
 * The canonical window identity:
 * `${owner}_${repo}__${windowStart}__${windowEnd}__${cohort}__raw${RAW_SCHEMA_VERSION}`.
 * Stable for identical inputs; distinct whenever repo, either bound, cohort, or
 * the raw schema version changes.
 */
export function computeWindowId(input: WindowIdInput): string {
    const { owner, repo } = ownerRepoOf(input.repo);
    const raw = input.rawSchemaVersion ?? RAW_SCHEMA_VERSION;
    return `${owner}_${repo}__${input.windowStart}__${input.windowEnd}__${input.cohort}__raw${raw}`;
}

/** Artifact basename (no extension) for a run/window id: `run-<windowId>`. */
export function runArtifactBase(windowId: string): string {
    return `run-${windowId}`;
}
