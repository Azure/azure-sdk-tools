/**
 * utils.ts — Shared utility routines (logging, child-process, gh/git wrappers,
 * bounded concurrency, content hashing).
 */
import { spawn, spawnSync } from "node:child_process";
import type { SpawnSyncOptions } from "node:child_process";
import { createHash } from "node:crypto";

import type { User } from "./types.ts";

export function parsePositiveInt(value: string, flagName: string): number {
    const parsed = Number.parseInt(value, 10);
    if (!Number.isFinite(parsed) || parsed <= 0) {
        throw new Error(`${flagName} must be a positive integer`);
    }
    return parsed;
}

export interface Logger {
    /** Whether info() lines are emitted. error() always emits regardless. */
    enabled: boolean;
    /** Print a prefixed progress/diagnostic line to stderr (suppressed when disabled). */
    info(message: string): void;
    /** Print a prefixed error line to stderr (always emitted). */
    error(message: string): void;
}

/**
 * Build a stderr logger that prefixes every line with the given script name.
 * Human-readable diagnostics always go to stderr so `--json` stdout stays clean.
 */
export function makeLogger(prefix: string, enabled = true): Logger {
    return {
        enabled,
        info(message: string): void {
            if (!this.enabled) return;
            process.stderr.write(`${prefix}: ${message}\n`);
        },
        error(message: string): void {
            process.stderr.write(`${prefix}: ${message}\n`);
        },
    };
}

export type SyncCommandSpec = {
    command: string;
    args: string[];
} & Pick<SpawnSyncOptions, "cwd" | "maxBuffer" | "stdio" | "input">;

/**
 * Run a command synchronously and return combined stdout+stderr output.
 * Throws on ENOENT and non-zero exit statuses.
 */
export function runSync(spec: SyncCommandSpec): string {
    const { command, args, cwd, maxBuffer, stdio, input } = spec;
    const result = spawnSync(command, args, {
        cwd,
        input,
        encoding: "utf8",
        maxBuffer,
        stdio: stdio ?? ["inherit", "pipe", "pipe"],
    });

    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- spawn output may be null
    const output = `${result.stdout ?? ""}${result.stderr ?? ""}`;

    if (result.error) {
        if ((result.error as NodeJS.ErrnoException).code === "ENOENT") {
            throw new Error(`${command} not found on PATH`);
        }
        throw result.error;
    }

    if (result.status !== 0) {
        throw new Error(
            `command failed with exit code ${result.status}.\nCommand line: (${command} ${args.join(" ")})\nOutput:${output}`,
        );
    }

    return output;
}

export function runGitSync(cwd: string, ...args: string[]): string {
    return runSync({ command: "git", args, cwd });
}

export function runGhSync(cwd: string, ...args: string[]): string {
    return runSync({ command: "gh", args, cwd });
}

export function sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

/** sha256 hex digest of a string (used for prompt/vocabulary/content hashes). */
export function sha256(input: string): string {
    return `sha256:${createHash("sha256").update(input, "utf8").digest("hex")}`;
}

// eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T is an ergonomic cast for callers
export function ghJsonSync<T>(args: string[]): T {
    let stdout: string;
    try {
        stdout = runSync({
            command: "gh",
            args,
            maxBuffer: 256 * 1024 * 1024,
            stdio: ["ignore", "pipe", "pipe"],
        });
    } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        throw new Error(`gh ${args.join(" ")} failed: ${msg}`);
    }

    try {
        return JSON.parse(stdout) as T;
    } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        throw new Error(
            `failed to parse JSON from gh ${args.join(" ")}: ${msg}`,
        );
    }
}

// eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T is an ergonomic cast for callers
export function ghApiJsonSync<T>(endpoint: string): T {
    return ghJsonSync<T>(["api", endpoint]);
}

/**
 * Collapse the output of `gh api --paginate --slurp` (a JSON array with one
 * element per page) back to what callers expect: a single page is unwrapped,
 * all-array pages are flattened, all-object pages are shallow-merged
 * (array-valued keys concatenated), and anything else is returned as-is.
 */
// eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T is an ergonomic cast for callers
export function flattenSlurpedPages<T>(parsed: unknown): T {
    if (!Array.isArray(parsed)) return parsed as T;
    const pages: unknown[] = parsed;
    if (pages.length === 1) return pages[0] as T;
    if (pages.every((p): p is unknown[] => Array.isArray(p))) {
        return pages.flat() as T;
    }
    if (
        pages.every(
            (page): page is Record<string, unknown> =>
                page !== null &&
                typeof page === "object" &&
                !Array.isArray(page),
        )
    ) {
        const [first, ...rest] = pages;
        const merged: Record<string, unknown> = { ...first };
        for (const page of rest) {
            for (const [key, value] of Object.entries(page)) {
                const existing = merged[key];
                if (Array.isArray(existing) && Array.isArray(value)) {
                    merged[key] = [
                        ...(existing as unknown[]),
                        ...(value as unknown[]),
                    ];
                } else {
                    merged[key] = value;
                }
            }
        }
        return merged as T;
    }
    return pages as T;
}

/**
 * Synchronous, fully-paginated variant of {@link ghApiJsonSync}. Runs
 * `gh api --paginate --slurp` so list endpoints (comments, reviews, files,
 * commit→PR mappings) are never silently truncated at GitHub's 30-item page
 * default. Use this for any endpoint that can return more than one page.
 */
// eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T is an ergonomic cast for callers
export function ghApiJsonPaginatedSync<T>(endpoint: string): T {
    const parsed = ghJsonSync<unknown>([
        "api",
        "--paginate",
        "--slurp",
        endpoint,
    ]);
    return flattenSlurpedPages<T>(parsed);
}

/**
 * A counting semaphore that bounds how many async operations run at once.
 * Used to cap concurrent `gh` API requests process-wide: GitHub's secondary
 * rate limits punish bursts / high concurrency independently of the primary
 * hourly quota, so the ceiling matters even when quota remains.
 */
export class Semaphore {
    private inFlight = 0;
    private readonly waiters: (() => void)[] = [];
    private readonly max: number;

    constructor(max: number) {
        if (!Number.isFinite(max) || max < 1) {
            throw new Error("Semaphore max must be >= 1");
        }
        this.max = max;
    }

    /** Number of currently acquired (in-flight) slots. */
    get active(): number {
        return this.inFlight;
    }

    /** Run `fn` once a slot is free, releasing the slot when it settles. */
    async run<T>(fn: () => Promise<T>): Promise<T> {
        await this.acquire();
        try {
            return await fn();
        } finally {
            this.release();
        }
    }

    private acquire(): Promise<void> {
        if (this.inFlight < this.max) {
            this.inFlight++;
            return Promise.resolve();
        }
        return new Promise<void>((resolve) => this.waiters.push(resolve));
    }

    private release(): void {
        const next = this.waiters.shift();
        if (next) {
            // Hand the slot straight to the next waiter (inFlight unchanged).
            next();
        } else {
            this.inFlight--;
        }
    }
}

/**
 * Extract a wait duration (ms) from a gh/GitHub error carrying a `Retry-After`
 * header or a "retry after N seconds" phrase. Returns null when no explicit
 * delay is advertised so callers fall back to exponential backoff. Honoring the
 * server-advertised delay is the correct response to a secondary rate limit.
 */
export function parseRetryAfterMs(message: string): number | null {
    const header = /retry-after:\s*(\d+)/i.exec(message);
    if (header) return Number(header[1]) * 1000;
    const phrase = /retry after (\d+) second/i.exec(message);
    if (phrase) return Number(phrase[1]) * 1000;
    const wait = /wait (\d+) second/i.exec(message);
    if (wait) return Number(wait[1]) * 1000;
    return null;
}

/**
 * Global ceiling on concurrent `gh` API requests (REST + GraphQL). Kept well
 * under GitHub's ~100-concurrent guidance so parallel fan-out (fetch-prs) never
 * trips secondary rate limits. Override with CCR_GH_MAX_CONCURRENCY.
 */
const GH_MAX_CONCURRENCY = (() => {
    const raw = process.env.CCR_GH_MAX_CONCURRENCY;
    const n = raw ? Number.parseInt(raw, 10) : Number.NaN;
    return Number.isFinite(n) && n >= 1 ? n : 8;
})();

/** Shared limiter every async `gh` request is admitted through. */
export const ghRequestLimiter = new Semaphore(GH_MAX_CONCURRENCY);

/**
 * Async, paginated variant of {@link ghApiJsonSync}. Every request is admitted
 * through the shared {@link ghRequestLimiter}, so concurrent callers (the
 * fetch-prs fan-out) stay under the secondary-rate-limit ceiling. Retries with
 * header-aware backoff on secondary-rate-limit / 5xx errors.
 */
export async function ghApiJsonAsync<T>(
    endpoint: string,
    opts: { maxRetries?: number } = {},
): Promise<T> {
    return withGhRetry(opts.maxRetries ?? 4, () =>
        ghRequestLimiter.run(() =>
            spawnGhJson<T>(
                ["api", "--paginate", "--slurp", endpoint],
                endpoint,
            ),
        ),
    );
}

/**
 * Async GraphQL request, admitted through the same {@link ghRequestLimiter} and
 * retry path as REST so it counts against the same concurrency ceiling. `fields`
 * are the `gh api graphql` arguments after the subcommand (e.g. `-f query=…`).
 */
export async function ghApiGraphqlAsync<T>(
    fields: string[],
    opts: { maxRetries?: number } = {},
): Promise<T> {
    return withGhRetry(opts.maxRetries ?? 4, () =>
        ghRequestLimiter.run(() =>
            spawnGhJson<T>(["api", "graphql", ...fields], "graphql"),
        ),
    );
}

async function withGhRetry<T>(
    maxRetries: number,
    fn: () => Promise<T>,
): Promise<T> {
    let attempt = 0;
    for (;;) {
        try {
            return await fn();
        } catch (err) {
            attempt++;
            const msg = err instanceof Error ? err.message : String(err);
            const retriable =
                /rate limit|secondary rate|abuse|was submitted too quickly|HTTP 5\d\d|timeout/i.test(
                    msg,
                );
            if (!retriable || attempt > maxRetries) throw err;
            // Prefer the server-advertised Retry-After; else exponential backoff.
            const backoffMs =
                parseRetryAfterMs(msg) ??
                Math.min(60_000, 1000 * 2 ** (attempt - 1));
            await sleep(backoffMs);
        }
    }
}

function spawnGhJson<T>(args: string[], label: string): Promise<T> {
    return new Promise((resolve, reject) => {
        const child = spawn("gh", args, { stdio: ["ignore", "pipe", "pipe"] });

        let stdout = "";
        let stderr = "";

        child.stdout.on("data", (chunk: Buffer) => {
            stdout += chunk.toString("utf8");
        });
        child.stderr.on("data", (chunk: Buffer) => {
            stderr += chunk.toString("utf8");
        });
        child.on("error", (err) => {
            const e = err as NodeJS.ErrnoException;
            if (e.code === "ENOENT") {
                reject(new Error("gh CLI not found on PATH"));
                return;
            }
            reject(err);
        });
        child.on("close", (code) => {
            if (code !== 0) {
                reject(new Error(`gh api ${label} failed: ${stderr.trim()}`));
                return;
            }
            try {
                const parsed: unknown = JSON.parse(stdout);
                // `--paginate --slurp` wraps pages in one array; GraphQL returns
                // a lone object. flattenSlurpedPages handles both.
                resolve(flattenSlurpedPages<T>(parsed));
            } catch (err) {
                reject(err instanceof Error ? err : new Error(String(err)));
            }
        });
    });
}

/** Check if a user is a bot account (type=Bot or login ends with [bot]). */
export function isBot(user: User | null): boolean {
    if (!user) return false;
    if (user.type === "Bot") return true;
    return /\[bot\]$/i.test(user.login || "");
}

/** Check if a user is a human (opposite of isBot). */
export function isHumanUser(user: User | null): boolean {
    if (!user) return false;
    if (user.type === "Bot") return false;
    return !/\[bot\]$/i.test(user.login);
}

export type ConcurrencyProgress<T> = (event: {
    type: "start" | "finish";
    item: T;
    completed: number;
    active: number;
    total: number;
}) => void;

/**
 * Run an async worker over items with a bounded number of in-flight tasks.
 * Results are returned in input order.
 */
export async function runWithConcurrency<T, R>(
    items: T[],
    concurrency: number,
    worker: (item: T) => Promise<R>,
    onProgress?: ConcurrencyProgress<T>,
): Promise<R[]> {
    const results: R[] = new Array<R>(items.length);
    let nextIndex = 0;
    let completed = 0;
    let active = 0;
    const total = items.length;

    async function runWorker(): Promise<void> {
        for (;;) {
            const idx = nextIndex;
            nextIndex++;
            if (idx >= items.length) return;
            // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- bounds-checked above
            const item = items[idx]!;
            active++;
            onProgress?.({ type: "start", item, completed, active, total });
            try {
                results[idx] = await worker(item);
            } finally {
                active--;
                completed++;
                onProgress?.({
                    type: "finish",
                    item,
                    completed,
                    active,
                    total,
                });
            }
        }
    }

    const workers: Promise<void>[] = [];
    const size = Math.min(concurrency, items.length);
    for (let i = 0; i < size; i++) workers.push(runWorker());
    await Promise.all(workers);
    return results;
}

/** Hours between two ISO timestamps, or null if either is missing/invalid. */
export function hoursBetween(
    startIso: string | null | undefined,
    endIso: string | null | undefined,
): number | null {
    if (!startIso || !endIso) return null;
    const start = Date.parse(startIso);
    const end = Date.parse(endIso);
    if (Number.isNaN(start) || Number.isNaN(end)) return null;
    return Math.round(((end - start) / 3_600_000) * 100) / 100;
}
