import * as fs from "node:fs/promises";
import type { SampleIdea } from "./types.ts";

/**
 * Convert a title to PascalCase format suitable for Java class names
 * e.g., "Azure Blob Storage Java Sample" -> "AzureBlobStorageExample"
 */
function toPascalCaseExample(title: string): string {
    const pascalCase = title
        .toLowerCase()
        .replace(/[^a-z0-9\s]/g, "") // Remove special characters
        .split(/\s+/) // Split on whitespace
        .filter((word) => word.length > 0) // Remove empty strings
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1)) // Capitalise first letter
        .join("");

    return `${pascalCase}Example`;
}

/**
 * Generate appropriate filename based on language
 * @param title The title from the markdown file
 * @param language The target programming language (optional, defaults to generic kebab-case)
 */
function generateFileName(title: string, language?: string): string {
    if (language?.toLowerCase() === "java") {
        return toPascalCaseExample(title);
    }

    // Default behaviour for other languages (kebab-case)
    return (
        title
            .toLowerCase()
            .replace(/[^a-z0-9\s]/g, "")
            .replace(/\s+/g, "-")
            .substring(0, 50) || "user-sample"
    );
}

/**
 * Parses a user prompt from a markdown file and converts it to a SampleIdea
 * @param promptPath Path to the markdown file containing the user prompt
 * @param language Optional language to determine filename format
 * @returns A SampleIdea object based on the user prompt
 */
export async function parseUserPrompt(
    promptPath: string,
    language?: string,
): Promise<SampleIdea> {
    try {
        const content = await fs.readFile(promptPath, "utf-8");

        // Extract title from first # heading or use filename
        const titleMatch = content.match(/^#\s+(.+)/m);
        const title = titleMatch
            ? titleMatch[1].trim()
            : "User Provided Sample";

        // Use the entire content as description, cleaning up markdown
        const description = content
            .replace(/^#\s+.+$/gm, "") // Remove title heading
            .trim();

        // Generate a filename based on the title and language
        const fileName = generateFileName(title, language);

        // Create a basic SampleIdea structure
        // Since this is user-provided, we don't have specific API requests
        // The actual sample generation will be based on the description
        const sampleIdea: SampleIdea = {
            name: title,
            description,
            fileName,
            requests: [], // Will be populated during code generation based on the prompt
            prerequisites: {
                setup: "Follow the user's requirements as described in the prompt.",
            },
        };

        return sampleIdea;
    } catch (error) {
        throw new Error(
            `Failed to parse user prompt from ${promptPath}: ${error}`,
        );
    }
}
