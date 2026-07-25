/**
 * utils.test.ts — guards for the shared request limiter (Semaphore) and the
 * header-aware backoff parser that keep the fetch-prs fan-out under GitHub's
 * secondary-rate-limit concurrency ceiling.
 */
import { describe, expect, it } from "vitest";

import { Semaphore, parseRetryAfterMs } from "../scripts/utils.ts";

describe("Semaphore", () => {
    it("never exceeds the configured ceiling under heavy fan-out", async () => {
        const max = 3;
        const sem = new Semaphore(max);
        let active = 0;
        let peak = 0;

        const task = async (): Promise<void> => {
            active++;
            peak = Math.max(peak, active);
            await new Promise((r) => setTimeout(r, 5));
            active--;
        };

        await Promise.all(
            Array.from({ length: 30 }, () => sem.run(task)),
        );

        expect(peak).toBeLessThanOrEqual(max);
        expect(active).toBe(0);
    });

    it("returns worker results and releases slots on rejection", async () => {
        const sem = new Semaphore(2);
        const ok = await sem.run(() => Promise.resolve(42));
        expect(ok).toBe(42);

        await expect(
            sem.run(() => Promise.reject(new Error("boom"))),
        ).rejects.toThrow("boom");

        // Slot must be freed after the rejection, so further work proceeds.
        expect(await sem.run(() => Promise.resolve("free"))).toBe("free");
    });

    it("rejects an invalid ceiling", () => {
        expect(() => new Semaphore(0)).toThrow();
        expect(() => new Semaphore(Number.NaN)).toThrow();
    });
});

describe("parseRetryAfterMs", () => {
    it("reads a Retry-After header value (seconds → ms)", () => {
        expect(parseRetryAfterMs("HTTP 403: retry-after: 30")).toBe(30_000);
    });

    it("reads a 'retry after N seconds' phrase", () => {
        expect(
            parseRetryAfterMs("You have exceeded a secondary rate limit. Please retry after 12 seconds."),
        ).toBe(12_000);
    });

    it("reads a 'wait N seconds' phrase", () => {
        expect(parseRetryAfterMs("please wait 5 seconds before retrying")).toBe(
            5_000,
        );
    });

    it("returns null when no explicit delay is advertised", () => {
        expect(parseRetryAfterMs("secondary rate limit hit")).toBeNull();
    });
});
