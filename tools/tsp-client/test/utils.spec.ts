import { describe, it } from "node:test";
import {
  formatAdditionalDirectories,
  getAdditionalDirectoryName,
  getServiceDir,
  makeSparseSpecDir,
} from "../src/utils.js";
import { parse as parseYaml } from "yaml";
import { assert } from "chai";
import { readFile, stat } from "node:fs/promises";

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
    const specDir = await makeSparseSpecDir("./test");
    assert.ok(specDir.startsWith("./test/../sparse-spec"));

    const createdDir = await stat(specDir);
    assert.ok(createdDir.isDirectory());
  });

  it("Check getAdditionalDirectoryName", async function () {
    const dir = getAdditionalDirectoryName("/specification/foo");
    assert.equal(dir, "foo");

    const dir2 = getAdditionalDirectoryName("/specification/foo/");
    assert.equal(dir2, "foo");
  });

  it("Check formatAdditionalDirectories", async function () {
    const result = formatAdditionalDirectories(["/specification/foo", "/specification/bar"]);
    const expected = "\n- /specification/foo\n- /specification/bar";
    assert.equal(result, expected);
  });
});
