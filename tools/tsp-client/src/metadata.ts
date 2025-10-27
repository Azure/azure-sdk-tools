import { writeFile } from "fs/promises";
import { Logger } from "./log.js";
import { joinPaths } from "@typespec/compiler";
import { readFile } from "fs/promises";
import { packageJson } from "./index.js";

/**
 * Interface representing the metadata information for a tsp-client command run.
 */
interface TspClientMetadata {
  /** Version of the tsp-client tool */
  version: string;
  /** Date when the metadata file was created or last modified */
  dateCreatedOrModified: string;
  /** JSON content of the emitter-package.json file used to generate */
  emitterPackageJsonContent: object;
}

/**
 * Creates a tsp_client_metadata.json file with information about the tsp-client command run.
 *
 * @param outputDir - The directory where the metadata file will be created
 * @param emitterPackageJsonPath - Path to the emitter-package.json file
 * @param tspClientVersion - Version of the tsp-client tool (optional, will read from package.json if not provided)
 */
export async function createTspClientMetadata(
  outputDir: string,
  emitterPackageJsonPath: string,
): Promise<void> {
  try {
    Logger.info("Creating tsp_client_metadata.json file...");

    // Create the metadata object
    const metadata: TspClientMetadata = {
      version: packageJson.version,
      dateCreatedOrModified: new Date().toISOString(),
      emitterPackageJsonContent: JSON.parse(await readFile(emitterPackageJsonPath, "utf8")),
    };

    // Convert the metadata to JSON format with proper formatting
    const jsonContent = JSON.stringify(
      {
        version: metadata.version,
        "date-created-or-modified": metadata.dateCreatedOrModified,
        "emitter-package-json-content": metadata.emitterPackageJsonContent,
      },
      null,
      2, // 2-space indentation for pretty formatting
    );

    // Write the metadata file
    const metadataFilePath = joinPaths(outputDir, "tsp_client_metadata.json");
    await writeFile(metadataFilePath, jsonContent, "utf8");

    Logger.info(`Successfully created tsp_client_metadata.json at ${metadataFilePath}`);
  } catch (error) {
    Logger.error(`Error creating tsp_client_metadata.json: ${error}`);
    throw error;
  }
}
