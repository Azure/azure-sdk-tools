import { writeFile } from "fs/promises";
import { Logger } from "./log.js";
import { stringify as stringifyYaml } from "yaml";
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
 * Creates a tsp_client_metadata.yaml file with information about the tsp-client command run.
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
    Logger.info("Creating tsp_client_metadata.yaml file...");

    // Create the metadata object
    const metadata: TspClientMetadata = {
      version: packageJson.version,
      dateCreatedOrModified: new Date().toISOString(),
      emitterPackageJsonContent: JSON.parse(await readFile(emitterPackageJsonPath, "utf8")),
    };

    // Convert the metadata to YAML format with proper formatting
    const yamlContent = stringifyYaml(
      {
        version: metadata.version,
        "date-created-or-modified": metadata.dateCreatedOrModified,
        "emitter-package-json-content": metadata.emitterPackageJsonContent,
      },
      {
        indent: 2,
        lineWidth: 0, // No line wrapping
        minContentWidth: 0,
      },
    );

    // Write the metadata file
    const metadataFilePath = joinPaths(outputDir, "tsp_client_metadata.yaml");
    await writeFile(metadataFilePath, yamlContent, "utf8");

    Logger.info(`Successfully created tsp_client_metadata.yaml at ${metadataFilePath}`);
  } catch (error) {
    Logger.error(`Error creating tsp_client_metadata.yaml: ${error}`);
    throw error;
  }
}
