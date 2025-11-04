import { writeFile } from "fs/promises";
import { Logger } from "./log.js";
import { joinPaths, normalizeSlashes } from "@typespec/compiler";
import { readFile } from "fs/promises";
import { getPackageJson } from "./utils.js";
import * as yaml from "yaml";
import { relative } from "path";

/**
 * Interface representing the metadata information for a tsp-client command run.
 */
interface TspClientMetadata {
  /** Version of the tsp-client tool */
  version: string;
  /** Date when the metadata file was created or last modified */
  dateCreatedOrModified: string;
  /** Path to the emitter-package.json file used to generate */
  emitterPackageJsonPath?: string;
  /** Optional: Content of the emitter-package.json file used to generate as JSON string */
  emitterPackageJsonContent?: string;
}

/**
 * Creates a tsp_client_metadata.yaml file with information about the tsp-client command run.
 *
 * @param outputDir - The directory where the metadata file will be created
 * @param emitterPackageJsonPath - Path to the emitter-package.json file
 */
export async function createTspClientMetadata(
  outputDir: string,
  repoRoot: string,
  emitterPackageJsonPath: string,
): Promise<void> {
  try {
    Logger.info("Creating tsp_client_metadata.yaml file...");

    // Get package.json information
    const packageJson = await getPackageJson();

    // Create the metadata object
    const metadata: TspClientMetadata = {
      version: packageJson.version,
      dateCreatedOrModified: new Date().toISOString(),
      emitterPackageJsonPath: normalizeSlashes(relative(repoRoot, emitterPackageJsonPath)),
      emitterPackageJsonContent: JSON.stringify(JSON.parse(await readFile(emitterPackageJsonPath, "utf8")), null, 2),
    };

    // Convert the metadata to YAML format
    const yamlContent = yaml.stringify(metadata);

    // Write the metadata file
    const metadataFilePath = joinPaths(outputDir, "tsp_client_metadata.yaml");
    await writeFile(metadataFilePath, yamlContent, "utf8");

    Logger.info(`Successfully created tsp_client_metadata.yaml at ${metadataFilePath}`);
  } catch (error) {
    Logger.error(`Error creating tsp_client_metadata.yaml: ${error}`);
    throw error;
  }
}
