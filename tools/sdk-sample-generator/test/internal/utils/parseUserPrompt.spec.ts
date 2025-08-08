import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { parseUserPrompt } from "../../../genaisrc/src/parseUserPrompt.ts";
import { vol } from "memfs";
import { vi } from "vitest";

vi.mock("node:fs/promises", async () => {
    const memfs = await import("memfs");
    return memfs.fs.promises;
});

describe("parseUserPrompt", () => {
    beforeEach(() => {
        vol.reset();
    });

    afterEach(() => {
        vol.reset();
    });

    it("should parse a markdown file with title and description", async () => {
        const promptContent = `# Create a Storage Blob Sample

This sample should demonstrate how to:
- Connect to Azure Storage
- Upload a blob
- Download a blob
- List blobs in a container`;

        const promptPath = "/user-prompt.md";
        vol.fromJSON({ [promptPath]: promptContent });

        const result = await parseUserPrompt(promptPath);

        expect(result.name).toBe("Create a Storage Blob Sample");
        expect(result.fileName).toBe("create-a-storage-blob-sample");
        expect(result.description).toContain("This sample should demonstrate");
        expect(result.description).toContain("Upload a blob");
        expect(result.requests).toEqual([]);
        expect(result.prerequisites?.setup).toContain(
            "Follow the user's requirements",
        );
    });

    it("should handle markdown without title", async () => {
        const promptContent = `This is a sample request without a title heading.

It should still work and generate a proper sample idea.`;

        const promptPath = "/no-title.md";
        vol.fromJSON({ [promptPath]: promptContent });

        const result = await parseUserPrompt(promptPath);

        expect(result.name).toBe("User Provided Sample");
        expect(result.fileName).toBe("user-provided-sample");
        expect(result.description).toContain("This is a sample request");
    });

    it("should generate clean filenames from titles", async () => {
        const promptContent = `# Create Azure Key Vault Secret (with special chars!)

Sample description here.`;

        const promptPath = "/special-chars.md";
        vol.fromJSON({ [promptPath]: promptContent });

        const result = await parseUserPrompt(promptPath);

        expect(result.fileName).toBe(
            "create-azure-key-vault-secret-with-special-chars",
        );
    });

    it("should throw error for non-existent file", async () => {
        const nonExistentPath = "/does-not-exist.md";

        await expect(parseUserPrompt(nonExistentPath)).rejects.toThrow(
            "Failed to parse user prompt",
        );
    });
});
