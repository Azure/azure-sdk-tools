import { describe, it } from "node:test";
import { getServiceDir } from "../src/utils.js";
import { parse as parseYaml } from "yaml";
import { assert } from "chai";
import { readFile } from "node:fs/promises";

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
