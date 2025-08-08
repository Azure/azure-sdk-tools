import { sampleIdeaSchemaDefinition } from "./sampleIdea.ts";
import type { SampleIdea } from "./types.ts";
import * as fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";

const sampleIdeasCacheFilename = "sample_ideas_cache.json";
const ONE_WEEK_MS = 7 * 24 * 60 * 60 * 1000;

async function loadIdeasFromCache(
    cacheFolder: string,
    spec: WorkspaceFile[],
): Promise<SampleIdea[] | undefined> {
    const cacheFolderParent = path.dirname(cacheFolder);
    const files = await fs.readdir(cacheFolderParent);

    // Sort folders by timestamp prefix descending (most recent first)
    const sortedFiles = files
        .filter((file) =>
            /^\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}-\d{3}Z-/.test(file),
        )
        .sort((a, b) => {
            // Extract timestamp part for comparison
            const getTs = (f: string) => f.split("-").slice(0, 7).join("-");
            return getTs(b).localeCompare(getTs(a));
        });

    const now = Date.now();

    // Compute a hash of the spec files for comparison
    const specHash = await computeSpecHash(spec);

    for (const file of sortedFiles) {
        const filePath = path.join(cacheFolderParent, file);
        const stats = await fs.stat(filePath);
        // Invalidate cache if older than one week
        if (now - stats.mtimeMs < ONE_WEEK_MS) {
            const cachePath = path.join(filePath, sampleIdeasCacheFilename);
            try {
                const cacheObj = JSON.parse(
                    await fs.readFile(cachePath, "utf-8"),
                );
                if (cacheObj.specHash !== specHash) continue; // Only load if spec matches
                return cacheObj.ideas;
            } catch (err: any) {
                // If file not found, continue to next most recent cache folder
                if (err.code !== "ENOENT") throw err;
            }
        } else {
            // If older than one week, delete the folder
            try {
                await fs.rm(filePath, { recursive: true });
            } catch (err: any) {
                if (err.code !== "ENOENT") throw err;
            }
        }
    }
    // If none found, optionally clean up old cache folders
    return undefined;
}

// Helper to compute a hash of the spec files' contents
async function computeSpecHash(spec: WorkspaceFile[]): Promise<string> {
    const hash = crypto.createHash("sha256");
    for (const file of spec) {
        hash.update(file.filename);
        hash.update(typeof file.content === "string" ? file.content : "");
    }
    return hash.digest("hex");
}

async function generateIdeas(inputs: {
    sampleCount: number;
    model: string;
    spec: WorkspaceFile[];
}): Promise<SampleIdea[]> {
    const { spec, model, sampleCount } = inputs;
    const res = await runPrompt(
        (ctx) => {
            const schema = ctx.defSchema(
                "SAMPLE_SCHEMA",
                sampleIdeaSchemaDefinition,
            );
            const data = ctx.def("FILE", spec);
            ctx.$`
  You are an expert coder.
  
  Your job is to generate a list of sample ideas for customer scenarios that use a service described in ${data}.
  The description of each sample idea must contain information about the specific input values
  that the sample should use, and the expected output.
      
  Generate ${sampleCount} sample ideas using JSON compliant with ${schema}.
  
  **Repeat this process:**  
  - Generate or revise the ideas.
  - Review the request path to make sure it is correct and complete.
  - Review the request query parameters to make sure they are correct and complete.
  - Review the request headers to make sure they are correct and complete.
  - Review the request body to make sure it is correct and complete.
  - Review the request method to make sure it is correct and complete.
  - If there are any issues, fix them.
  - Continue until the request conforms to API specification.

  **Rules:**  
  - The request path must be valid and complete as described in the API specification.
  - Do not ignore or drop any path segment.
  - The list of query parameters must be valid and complete as described in the API specification.
  - Do not ignore or drop any query parameter.
  - The list of headers must be valid and complete as described in the API specification.
  - Do not ignore or drop any header.
  - The request body must be valid and complete as described in the API specification.
  `;
        },
        {
            model,
        },
    );

    const sampleIdeas: SampleIdea[] = res.json?.filter(
        (item: SampleIdea) => item.name && item.fileName && item.description,
    );

    if (!sampleIdeas || sampleIdeas.length === 0) {
        throw new Error("No sample ideas found");
    }
    return sampleIdeas;
}

export async function generateOrLoadIdeas(inputs: {
    spec: WorkspaceFile[];
    model: string;
    samplesCount: number;
    useIdeasCache: boolean;
    cacheFolder: string;
}): Promise<SampleIdea[]> {
    const { spec, samplesCount, model, cacheFolder, useIdeasCache } = inputs;

    if (useIdeasCache) {
        try {
            const cachedIdeas = await loadIdeasFromCache(cacheFolder, spec);
            if (cachedIdeas) {
                return cachedIdeas;
            }
        } catch (error) {
            console.error("Failed to read cached ideas:", error);
        }
    }

    const sampleIdeas = await generateIdeas({
        model,
        sampleCount: samplesCount,
        spec,
    });

    // Save both the ideas and the spec hash in one file
    await fs.mkdir(cacheFolder, { recursive: true });
    const specHash = await computeSpecHash(spec);
    await fs.writeFile(
        path.join(cacheFolder, sampleIdeasCacheFilename),
        JSON.stringify({ specHash, ideas: sampleIdeas }, null, 2),
        "utf-8",
    );
    return sampleIdeas;
}
