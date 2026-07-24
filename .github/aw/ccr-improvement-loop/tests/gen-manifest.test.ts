/**
 * gen-manifest.test.ts — the dashboard manifest is the sole index the static
 * viewer has (it cannot list a directory), so its builder must be deterministic
 * and must include exactly the run files — never the manifest itself or stray
 * artifacts. These tests pin filtering, sorting, and de-duplication.
 */
import { describe, expect, it } from "vitest";

import { buildManifest, isRunFile } from "../scripts/gen-manifest.ts";

describe("isRunFile", () => {
    it("accepts only run-*.json", () => {
        expect(
            isRunFile("run-Azure_x__2026-01-01__2026-01-31__uncapped.json"),
        ).toBe(true);
        expect(isRunFile("manifest.json")).toBe(false);
        expect(isRunFile("run-x.txt")).toBe(false);
        expect(isRunFile("notrun-x.json")).toBe(false);
        expect(isRunFile("README.md")).toBe(false);
    });
});

describe("buildManifest", () => {
    it("keeps only run files, sorted, de-duplicated", () => {
        const m = buildManifest([
            "manifest.json",
            "run-b.json",
            "run-a.json",
            "run-b.json",
            "README.md",
            ".DS_Store",
        ]);
        expect(m).toEqual({ runs: ["run-a.json", "run-b.json"] });
    });

    it("sorts by byte order (stable, platform-independent)", () => {
        const m = buildManifest([
            "run-Azure_z.json",
            "run-Azure_a.json",
            "run-dotnet_m.json",
        ]);
        expect(m.runs).toEqual([
            "run-Azure_a.json",
            "run-Azure_z.json",
            "run-dotnet_m.json",
        ]);
    });

    it("yields an empty list when no run files are present", () => {
        expect(buildManifest(["manifest.json", "x.json"])).toEqual({
            runs: [],
        });
    });
});
