/**
 * Shared utility routines for scripts in this folder.
 */
import { spawn, spawnSync } from "node:child_process";
import type { SpawnSyncOptions } from "node:child_process";
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
 * `info` lines are gated by `enabled`; `error` lines always print.
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
} & Pick<SpawnSyncOptions, "cwd" | "maxBuffer" | "stdio">;

/**
 * Run a command synchronously and return combined stdout+stderr output.
 * Throws on ENOENT and non-zero exit statuses.
 */
export function runSync(spec: SyncCommandSpec): string {
    const { command, args, cwd, maxBuffer, stdio } = spec;
    const result = spawnSync(command, args, {
        cwd,
        encoding: "utf8",
        maxBuffer,
        stdio: stdio ?? ["inherit", "pipe", "pipe"],
    });

    // spawnSync stdout/stderr are typed as string here but can be null at runtime.
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

export function formatElapsed(ms: number): string {
    const totalSec = Math.max(0, Math.floor(ms / 1000));
    const mins = Math.floor(totalSec / 60);
    const secs = totalSec % 60;
    return `${String(mins).padStart(2, "0")}:${String(secs).padStart(2, "0")}`;
}

// eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T is an ergonomic cast for callers
export function ghJsonSync<T>(args: string[]): T {
    let stdout: string;
    try {
        stdout = runSync({
            command: "gh",
            args,
            maxBuffer: 128 * 1024 * 1024,
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
 * Async, paginated variant of {@link ghApiJsonSync}. Runs `gh api --paginate`
 * via `spawn` so callers can overlap multiple requests (e.g. behind a
 * concurrency runner). Rejects on spawn errors, non-zero exit, and invalid
 * JSON.
 */
export function ghApiJsonAsync<T>(endpoint: string): Promise<T> {
    return new Promise((resolve, reject) => {
        const child = spawn("gh", ["api", "--paginate", endpoint], {
            stdio: ["ignore", "pipe", "pipe"],
        });

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
                reject(
                    new Error(`gh api ${endpoint} failed: ${stderr.trim()}`),
                );
                return;
            }
            try {
                resolve(JSON.parse(stdout) as T);
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
 * Results are returned in the same order as the input items. An optional
 * `onProgress` callback is invoked as each item starts and finishes.
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
