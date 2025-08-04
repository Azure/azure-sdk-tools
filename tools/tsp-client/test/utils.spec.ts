import { describe, it } from "vitest";
import {
  formatAdditionalDirectories,
  getAdditionalDirectoryName,
  getServiceDir,
  makeSparseSpecDir,
  updateExistingTspLocation,
} from "../src/utils.js";
import { removeDirectory } from "../src/fs.js";
import { parse as parseYaml } from "yaml";
import { assert } from "chai";
import { readFile, stat } from "node:fs/promises";
import { joinPaths } from "@typespec/compiler";
import { cwd } from "node:process";

describe("get the right service dir from tspconfig.yaml", function () {
  it("Get custom emitter service-dir", async function () {
    const data = await readFile("./test/examples/tspconfig-custom-service-dir.yaml", "utf8");
    const configYaml = parseYaml(data);
    const serviceDir = getServiceDir(configYaml, "@azure-tools/typespec-ts");
    assert.strictEqual(serviceDir, "sdk/contosowidgetmanager/widget");
  });

  it("Get default service-dir", async function () {
    const data = await readFile("./test/examples/tspconfig-custom-service-dir.yaml", "utf8");
    const configYaml = parseYaml(data);
    const serviceDir = getServiceDir(configYaml, "@azure-tools/typespec-python");
    assert.strictEqual(serviceDir, "sdk/contosowidgetmanager");
  });
});

describe("Verify other utils functions", function () {
  it("Check makeSparseSpecDir", async function () {
    const specDir = await makeSparseSpecDir("./test/examples/");
    assert.ok(specDir.startsWith("./test/examples/../sparse-spec"));

    const createdDir = await stat(specDir);
    assert.ok(createdDir.isDirectory());

    await removeDirectory(specDir);
  });

  it("Check getAdditionalDirectoryName", function () {
    const dir = getAdditionalDirectoryName("/specification/foo");
    assert.equal(dir, "foo");

    const dir2 = getAdditionalDirectoryName("/specification/foo/");
    assert.equal(dir2, "foo");
  });

  it("Check formatAdditionalDirectories", function () {
    const result = formatAdditionalDirectories(["/specification/foo", "/specification/bar"]);
    const expected = "\n- /specification/foo\n- /specification/bar\n";
    assert.equal(result, expected);
  });

  it("Check updateExistingTspLocation update some properties", async function () {
    const tspLocationData = {
      directory: "test-directory",
      commit: "1234567890abcdef",
      repo: "Azure/foo-repo",
      additionalDirectories: [],
    };
    const newPackageDir = joinPaths(cwd(), "test/examples/sdk/local-spec-sdk");
    const updatedTspLocationData = await updateExistingTspLocation(tspLocationData, newPackageDir);
    // Verify that directory, commit, repo, and additionalDirectories are updated correctly
    // Verify that the entrypointFile remains unchanged
    assert.deepEqual(updatedTspLocationData, {
      directory: "test-directory",
      commit: "1234567890abcdef",
      repo: "Azure/foo-repo",
      additionalDirectories: [],
      entrypointFile: "foo.tsp",
    });
  });

  it("Check updateExistingTspLocation with extra property", async function () {
    const tspLocationData = {
      directory: "test-directory",
      commit: "1234567890abcdef",
      repo: "Azure/foo-repo",
      additionalDirectories: [],
      emitterPackageJsonPath: "example.json",
    };
    const newPackageDir = joinPaths(cwd(), "test/examples/sdk/local-spec-sdk");
    const updatedTspLocationData = await updateExistingTspLocation(tspLocationData, newPackageDir);
    // Verify that directory, commit, repo, and additionalDirectories are updated correctly
    // Verify that the entrypointFile remains unchanged
    assert.deepEqual(updatedTspLocationData, {
      directory: "test-directory",
      commit: "1234567890abcdef",
      repo: "Azure/foo-repo",
      additionalDirectories: [],
      entrypointFile: "foo.tsp",
      emitterPackageJsonPath: "example.json",
    });
  });
});
