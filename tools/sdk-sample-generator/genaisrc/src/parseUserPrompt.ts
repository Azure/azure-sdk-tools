import * as fs from "node:fs/promises";
import type { SampleIdea } from "./types.ts";

/**
 * Parses a user prompt from a markdown file and converts it to a SampleIdea
 * @param promptPath Path to the markdown file containing the user prompt
 * @returns A SampleIdea object based on the user prompt
 */
export async function parseUserPrompt(promptPath: string): Promise<SampleIdea> {
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

        // Generate a filename based on the title
        const fileName =
            title
                .toLowerCase()
                .replace(/[^a-z0-9\s]/g, "")
                .replace(/\s+/g, "-")
                .substring(0, 50) || "user-sample";

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
