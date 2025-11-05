import { afterAll, afterEach, beforeAll, beforeEach, describe, it } from "vitest";
import { createTspClientMetadata } from "../src/metadata.js";
import { removeDirectory, ensureDirectory } from "../src/fs.js";
import { assert } from "chai";
import { cp, readFile, rm, stat, writeFile } from "fs/promises";
import { joinPaths } from "@typespec/compiler";
import * as yaml from "yaml";
import { getRepoRoot } from "../src/git.js";
import { cwd } from "process";

describe.sequential("tsp-client metadata generation", function () {
  const testOutputDir = "./test/test-output-metadata";
  const testEmitterPackageJsonPath = "./test/test-output-metadata/test-emitter-package.json";
  let repoRoot = "";

  beforeEach(async function () {
    repoRoot = await getRepoRoot(cwd());
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

  afterEach(async function () {
    // Clean up test files
    await removeDirectory(testOutputDir).catch(() => {});
  });

  it("should create tsp-client-metadata.yaml with correct structure", async function () {
    await cp(
      joinPaths(cwd(), "test/utils/tsp-client-config-metadata.yaml"),
      joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"),
    );
    await createTspClientMetadata(testOutputDir, repoRoot, testEmitterPackageJsonPath);

    const metadataPath = joinPaths(testOutputDir, "tsp-client-metadata.yaml");
    const metadataContent = await readFile(metadataPath, "utf8");
    const metadata = yaml.parse(metadataContent);

    // Verify structure matches the template
    assert.exists(metadata.version);
    assert.exists(metadata["dateCreatedOrModified"]);
    assert.exists(metadata["emitterPackageJsonPath"]);
    assert.exists(metadata["emitterPackageJsonContent"]);

    // Verify content - version comes from packageJson now
    assert.isString(metadata.version);
    assert.isString(metadata["dateCreatedOrModified"]);
    assert.isString(metadata["emitterPackageJsonContent"]);

    // Verify emitter package content structure (parse the JSON string)
    const emitterContent = JSON.parse(metadata["emitterPackageJsonContent"]);
    assert.strictEqual(emitterContent.name, "test-emitter");
    assert.strictEqual(emitterContent.version, "1.0.0");
    assert.exists(emitterContent.dependencies);
    assert.equal(
      metadata.emitterPackageJsonPath,
      "tools/tsp-client/test/test-output-metadata/test-emitter-package.json",
    );
  });

  it("should handle date format correctly", async function () {
    await cp(
      joinPaths(cwd(), "test/utils/tsp-client-config-metadata.yaml"),
      joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"),
    );
    await createTspClientMetadata(testOutputDir, repoRoot, testEmitterPackageJsonPath);

    const metadataPath = joinPaths(testOutputDir, "tsp-client-metadata.yaml");
    const metadataContent = await readFile(metadataPath, "utf8");
    const metadata = yaml.parse(metadataContent);

    // Verify date is valid ISO string
    const dateString = metadata["dateCreatedOrModified"];
    const parsedDate = new Date(dateString);
    assert.isFalse(isNaN(parsedDate.getTime()), "Date should be valid ISO string");
    assert.equal(
      metadata.emitterPackageJsonPath,
      "tools/tsp-client/test/test-output-metadata/test-emitter-package.json",
    );
  });

  it("verify that metadata file isnt created if there's no tsp-client-config.yaml", async function () {
    await rm(joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"));

    await createTspClientMetadata(testOutputDir, repoRoot, testEmitterPackageJsonPath);

    // Verify that the metadata file was NOT created
    try {
      await stat(joinPaths(testOutputDir, "tsp-client-metadata.yaml"));
      // If stat succeeds, the file exists - this should fail the test
      assert.fail("Expected metadata file to not exist, but it was found");
    } catch (error: any) {
      // If stat throws an error, the file doesn't exist - this is what we expect
      assert.isTrue(error.code === "ENOENT", "Expected file not found error");
    }
  });

  it("verify that the metadata file isnt created if generateMetadata doesnt exist in tsp-client-config.yaml", async function () {
    await cp(
      joinPaths(cwd(), "test/utils/tsp-client-config.yaml"),
      joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"),
    );

    await createTspClientMetadata(testOutputDir, repoRoot, testEmitterPackageJsonPath);

    // Verify that the metadata file was NOT created
    try {
      await stat(joinPaths(testOutputDir, "tsp-client-metadata.yaml"));
      // If stat succeeds, the file exists - this should fail the test
      assert.fail("Expected metadata file to not exist, but it was found");
    } catch (error: any) {
      // If stat throws an error, the file doesn't exist - this is what we expect
      assert.isTrue(error.code === "ENOENT", "Expected file not found error");
    }
  });

  it("verify that metadata file isnt created if generateMetadata is set to false in tsp-client-config.yaml", async function () {
    await cp(
      joinPaths(cwd(), "test/utils/tsp-client-config-metadata-false.yaml"),
      joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"),
    );

    await createTspClientMetadata(testOutputDir, repoRoot, testEmitterPackageJsonPath);

    // Verify that the metadata file was NOT created
    try {
      await stat(joinPaths(testOutputDir, "tsp-client-metadata.yaml"));
      // If stat succeeds, the file exists - this should fail the test
      assert.fail("Expected metadata file to not exist, but it was found");
    } catch (error: any) {
      // If stat throws an error, the file doesn't exist - this is what we expect
      assert.isTrue(error.code === "ENOENT", "Expected file not found error");
    }
  });

  it('verify that metadata file isnt created if generateMetadata is set to \"false\" in tsp-client-config.yaml', async function () {
    await cp(
      joinPaths(cwd(), "test/utils/tsp-client-config-metadata-false-string.yaml"),
      joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"),
    );

    await createTspClientMetadata(testOutputDir, repoRoot, testEmitterPackageJsonPath);

    // Verify that the metadata file was NOT created
    try {
      await stat(joinPaths(testOutputDir, "tsp-client-metadata.yaml"));
      // If stat succeeds, the file exists - this should fail the test
      assert.fail("Expected metadata file to not exist, but it was found");
    } catch (error: any) {
      // If stat throws an error, the file doesn't exist - this is what we expect
      assert.isTrue(error.code === "ENOENT", "Expected file not found error");
    }
  });

  it("verify that metadata file isnt created if generateMetadata is set to random string in tsp-client-config.yaml", async function () {
    await cp(
      joinPaths(cwd(), "test/utils/tsp-client-config-metadata-random-string.yaml"),
      joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"),
    );

    await createTspClientMetadata(testOutputDir, repoRoot, testEmitterPackageJsonPath);

    // Verify that the metadata file was NOT created
    try {
      await stat(joinPaths(testOutputDir, "tsp-client-metadata.yaml"));
      // If stat succeeds, the file exists - this should fail the test
      assert.fail("Expected metadata file to not exist, but it was found");
    } catch (error: any) {
      // If stat throws an error, the file doesn't exist - this is what we expect
      assert.isTrue(error.code === "ENOENT", "Expected file not found error");
    }
  });
});
