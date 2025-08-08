import { type Sample } from "./types.ts";
import fs from "node:fs/promises";
import path from "node:path";

export async function saveSamples(inputs: {
    samples: Sample[];
    samplesFolder: string;
}): Promise<void> {
    const { samples, samplesFolder } = inputs;
    await fs.mkdir(samplesFolder, { recursive: true });
    for (const sample of samples) {
        const filePath = path.join(samplesFolder, sample.fileName);
        await fs.writeFile(filePath, sample.content, {
            encoding: "utf-8",
            mode: sample.executable ? 0o755 : 0o644,
        });
    }
}
