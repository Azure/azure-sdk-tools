import { mkdir, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createDashboardBundle } from "./dashboard-bundle.js";
import { publishLocalBlob } from "./local-blob-store.js";

export async function publishPipelineBundle({ blobRoot, inputDirectory, manifest }) {
  const workRoot = await mkdtemp(join(tmpdir(), "vally-dashboard-publish-"));
  try {
    const bundlePath = join(workRoot, "dashboard-bundle.zip");
    await createDashboardBundle({ inputDirectory, outputPath: bundlePath, manifest });
    await mkdir(blobRoot, { recursive: true });
    return await publishLocalBlob({ blobRoot, bundlePath, manifest });
  } finally {
    await rm(workRoot, { force: true, recursive: true });
  }
}