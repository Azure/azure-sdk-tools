import { describe, it, expect, beforeAll } from "vitest";
import { getMavenCoordinatesFromAPI } from "../../../genaisrc/src/java/typecheck.js";

type GlobalWithHost = typeof globalThis & { host: PromptHost };

describe("getMavenCoordinatesFromAPI (real HTTP)", () => {
    beforeAll(() => {
        (globalThis as GlobalWithHost).host = {
            fetch: fetch.bind(globalThis),
        } as PromptHost;
    });
    it("returns valid coordinates for a known class", async () => {
        const javaClass = "com.google.common.collect.ImmutableList";
        const result = await getMavenCoordinatesFromAPI(javaClass);
        expect(result).toBeDefined();
        expect(result).toHaveProperty("groupId");
        expect(result).toHaveProperty("artifactId");
        expect(result?.version).toMatch(/^\d+\.\d+\.\d+/);
    });

    it("returns undefined for a non-existent class", async () => {
        const javaClass = "com.this.does.not.exist.FooBarBazQux";
        const result = await getMavenCoordinatesFromAPI(javaClass);
        expect(result).toBeUndefined();
    });
});
