import { writeFile } from "fs/promises";
import { Logger } from "./log.js";
import { joinPaths } from "@typespec/compiler";
import { readFile } from "fs/promises";
import { getPackageJson } from "./utils.js";
import * as yaml from "yaml";

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
  /** Optional: Content of the emitter-package.json file used to generate */
  emitterPackageJsonContent?: object;
}

/**
 * Creates a tsp_client_metadata.yaml file with information about the tsp-client command run.
 *
 * @param outputDir - The directory where the metadata file will be created
 * @param emitterPackageJsonPath - Path to the emitter-package.json file
 */
export async function createTspClientMetadata(
  outputDir: string,
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
      emitterPackageJsonPath,
      emitterPackageJsonContent: JSON.parse(await readFile(emitterPackageJsonPath, "utf8")),
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
