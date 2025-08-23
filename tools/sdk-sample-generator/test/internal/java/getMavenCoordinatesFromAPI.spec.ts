import { describe, it, expect, beforeAll } from "vitest";
import { getMavenCoordinatesFromAPI } from "../../../genaisrc/src/java/typecheck.js";

type GlobalWithHost = typeof globalThis & { host: PromptHost };

describe("getMavenCoordinatesFromAPI (Azure SDK)", () => {
    beforeAll(() => {
        (globalThis as GlobalWithHost).host = {
            fetch: fetch.bind(globalThis),
        } as PromptHost;
    });

    it("returns valid coordinates for Azure Storage Blob SDK", async () => {
        const javaClass = "com.azure.storage.blob.BlobClient";
        const result = await getMavenCoordinatesFromAPI(javaClass);
        expect(result).toBeDefined();
        expect(result).toHaveProperty("groupId", "com.azure");
        expect(result).toHaveProperty("artifactId", "azure-storage-blob");
        expect(result?.version).toMatch(/^\d+\.\d+\.\d+/);
    });

    it("returns valid coordinates for Azure Identity SDK", async () => {
        const javaClass = "com.azure.identity.DefaultAzureCredential";
        const result = await getMavenCoordinatesFromAPI(javaClass);
        expect(result).toBeDefined();
        expect(result).toHaveProperty("groupId", "com.azure");
        expect(result).toHaveProperty("artifactId", "azure-identity");
        expect(result?.version).toMatch(/^\d+\.\d+\.\d+/);
    });

    it("returns undefined for JDK classes", async () => {
        const javaClass = "java.util.List";
        const result = await getMavenCoordinatesFromAPI(javaClass);
        expect(result).toBeUndefined();
    });

    it("returns undefined for non-Azure packages", async () => {
        const javaClass = "com.google.common.collect.ImmutableList";
        const result = await getMavenCoordinatesFromAPI(javaClass);
        expect(result).toBeUndefined();
    });

    it("returns undefined for non-existent Azure packages", async () => {
        const javaClass = "com.azure.nonexistent.FakeClient";
        const result = await getMavenCoordinatesFromAPI(javaClass);
        expect(result).toBeUndefined();
    });
});
