import { afterAll, beforeAll, describe, it } from "vitest";
import { createTspClientMetadata } from "../src/metadata.js";
import { removeDirectory, ensureDirectory } from "../src/fs.js";
import { assert } from "chai";
import { readFile, writeFile, mkdir } from "fs/promises";
import { joinPaths } from "@typespec/compiler";

describe("tsp-client metadata generation", function () {
  const testOutputDir = "./test/test-output-metadata";
  const testEmitterPackageJsonPath = "./test/test-output-metadata/test-emitter-package.json";

  beforeAll(async function () {
    // Create test directory and emitter-package.json
    await ensureDirectory(testOutputDir);

    // Create a sample emitter-package.json for testing
    const sampleEmitterPackageJson = {
      name: "test-emitter",
      version: "1.0.0",
      dependencies: {
        "@azure-tools/typespec-ts": "^0.29.0",
        "@typespec/compiler": "^1.0.0",
      },
      devDependencies: {
        "@types/node": "^20.0.0",
      },
    };

    await writeFile(testEmitterPackageJsonPath, JSON.stringify(sampleEmitterPackageJson, null, 2));
  });

  afterAll(async function () {
    // Clean up test files
    await removeDirectory(testOutputDir).catch(() => {});
  });

  it("should create tsp_client_metadata.json with correct structure", async function () {
    await createTspClientMetadata(testOutputDir, testEmitterPackageJsonPath);

    const metadataPath = joinPaths(testOutputDir, "tsp_client_metadata.json");
    const metadataContent = await readFile(metadataPath, "utf8");
    const metadata = JSON.parse(metadataContent);

    // Verify structure matches the template
    assert.exists(metadata.version);
    assert.exists(metadata["date-created-or-modified"]);
    assert.exists(metadata["emitter-package-json-content"]);

    // Verify content - version comes from packageJson now
    assert.isString(metadata.version);
    assert.isString(metadata["date-created-or-modified"]);
    assert.isObject(metadata["emitter-package-json-content"]);

    // Verify emitter package content structure
    const emitterContent = metadata["emitter-package-json-content"];
    assert.strictEqual(emitterContent.name, "test-emitter");
    assert.strictEqual(emitterContent.version, "1.0.0");
    assert.exists(emitterContent.dependencies);
  });

  it("should handle date format correctly", async function () {
    await createTspClientMetadata(testOutputDir, testEmitterPackageJsonPath);

    const metadataPath = joinPaths(testOutputDir, "tsp_client_metadata.json");
    const metadataContent = await readFile(metadataPath, "utf8");
    const metadata = JSON.parse(metadataContent);

    // Verify date is valid ISO string
    const dateString = metadata["date-created-or-modified"];
    const parsedDate = new Date(dateString);
    assert.isFalse(isNaN(parsedDate.getTime()), "Date should be valid ISO string");
  });

  it("should create metadata without emitter-package.json content if path not provided", async function () {
    await createTspClientMetadata(testOutputDir);

    const metadataPath = joinPaths(testOutputDir, "tsp_client_metadata.json");
    const metadataContent = await readFile(metadataPath, "utf8");
    const metadata = JSON.parse(metadataContent);

    // Verify structure
    assert.exists(metadata.version);
    assert.exists(metadata["date-created-or-modified"]);
    assert.notExists(metadata["emitter-package-json-content"]);
  });
});
