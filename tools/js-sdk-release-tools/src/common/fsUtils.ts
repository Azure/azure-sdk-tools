import { promises as fs } from 'node:fs';
import path from 'node:path';
import unixify from 'unixify';

/**
 * A Node.js-based implementation of glob functionality
 * @param baseDir The base directory to search in
 * @param pattern The glob pattern to match
 * @returns A Promise that resolves to an array of matching file paths
 */
export async function findFiles(baseDir: string, pattern: string): Promise<string[]> {
    // Convert glob pattern to regex
    const regexPattern = new RegExp(
        `^${pattern
            .replace(/\//g, '[\\\\/]')
            .replace(/\./g, '\\.')
            .replace(/\*\*/g, '.*')
            .replace(/\*/g, '[^\\\\/]*')}$`
    );

    const results: string[] = [];

    // Recursively search for files
    async function scanDir(dir: string, remainingDepth = 20): Promise<void> {
        if (remainingDepth <= 0) return;

        try {
            const entries = await fs.readdir(dir, { withFileTypes: true });

            for (const entry of entries) {
                const fullPath = path.join(dir, entry.name);
                const relativePath = path.relative(baseDir, fullPath);
                const unixPath = unixify(relativePath);

                if (regexPattern.test(unixPath)) {
                    results.push(fullPath);
                }

                if (entry.isDirectory()) {
                    await scanDir(fullPath, remainingDepth - 1);
                }
            }
        } catch (error) {
            // Silently ignore if directory doesn't exist or cannot be read
        }
    }

    await scanDir(baseDir);
    return results;
}