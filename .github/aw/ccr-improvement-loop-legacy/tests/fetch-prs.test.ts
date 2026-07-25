/**
 * fetch-prs.test.ts — regression guards for the raw-cache normalizers.
 *
 * The `mapReactions` case is a live-discovered bug: GitHub's REST comment
 * payloads inline `reactions` as a SUMMARY OBJECT
 * ({ total_count, "+1", heart, ... }), not an array — calling `.map` on it
 * crashed the fetch stage on real data. These tests pin both shapes.
 */
import { describe, expect, it } from "vitest";

import { buildSearch, mapReactions } from "../scripts/fetch-prs.ts";

describe("mapReactions", () => {
    it("expands a REST reaction summary object into content-only reactions", () => {
        const summary = {
            url: "https://api.github.com/...",
            total_count: 3,
            "+1": 2,
            "-1": 0,
            laugh: 0,
            hooray: 0,
            confused: 0,
            heart: 1,
            rocket: 0,
            eyes: 0,
        };
        const out = mapReactions(summary);
        expect(out.map((r) => r.content).sort()).toEqual(["+1", "heart"]);
        // The summary carries no per-user data, so user is null.
        expect(out.every((r) => r.user === null)).toBe(true);
    });

    it("still maps a legacy array shape", () => {
        const arr = [
            { content: "rocket", user: { login: "octocat" } as never },
        ];
        const out = mapReactions(arr);
        expect(out).toHaveLength(1);
        expect(out[0]?.content).toBe("rocket");
    });

    it("returns [] for null/undefined/empty summary", () => {
        expect(mapReactions(undefined)).toEqual([]);
        expect(mapReactions(null)).toEqual([]);
        expect(mapReactions({ total_count: 0, "+1": 0 })).toEqual([]);
    });
});

describe("buildSearch", () => {
    it("composes a merged-window qualifier", () => {
        expect(
            buildSearch({
                windowStart: "2026-06-01",
                windowEnd: "2026-06-30",
            } as never),
        ).toBe("merged:>=2026-06-01 merged:<=2026-06-30");
    });
});
