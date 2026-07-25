import { describe, expect, it } from "vitest";

import { globToRegExp, isExcludedPath, loadConfig } from "../scripts/config.ts";

describe("config", () => {
    it("loads and validates the shipped config.json", () => {
        const cfg = loadConfig();
        expect(cfg.ccrLogins.length).toBeGreaterThan(0);
        expect(cfg.minPrs).toBeGreaterThan(0);
    });

    it("globToRegExp matches ** across segments", () => {
        expect(
            globToRegExp("**/generated/**").test("a/b/generated/c/d.ts"),
        ).toBe(true);
        expect(globToRegExp("**/*.lock").test("pnpm.lock")).toBe(true);
        expect(globToRegExp("**/*.lock").test("a/b/pnpm.lock")).toBe(true);
    });

    it("globToRegExp * does not cross a path segment", () => {
        expect(globToRegExp("*.min.*").test("a.min.js")).toBe(true);
        expect(globToRegExp("*.min.*").test("dir/a.min.js")).toBe(false);
    });

    it("isExcludedPath uses the configured globs", () => {
        const ex = ["**/*.lock", "**/generated/**"];
        expect(isExcludedPath("src/generated/api.ts", ex)).toBe(true);
        expect(isExcludedPath("src/client.ts", ex)).toBe(false);
        expect(isExcludedPath(null, ex)).toBe(false);
    });
});
