import { assert } from "chai";
import { describe, it } from "vitest";
import { compileTsp, discoverEntrypointFile } from "../src/typespec.js";
import { joinPaths, resolvePath } from "@typespec/compiler";

describe("Check diagnostic reporting", function () {
  it.skip("Check diagnostic format", async function () {
    const mainFile = await discoverEntrypointFile(
      resolvePath(process.cwd(), "test", "examples", "specification", "diagnostics"),
    );
    const resolvedMainFilePath = resolvePath(
      process.cwd(),
      "test",
      "examples",
      "specification",
      "diagnostics",
      mainFile,
    );
    try {
      const [succeeded, _] = await compileTsp({
        emitterPackage: "@azure-tools/typespec-ts",
        outputPath: joinPaths(process.cwd(), "examples"),
        resolvedMainFilePath: resolvedMainFilePath,
        additionalEmitterOptions: "",
        saveInputs: false,
      });

      // False is returned if diagnostics are reported.
      // TODO: add more checks for specific diagnostics reported.
      assert.isFalse(succeeded);
    } catch (e) {
      assert.fail(`Unexpected failure: ${e}`);
    }
  });

  it("Check discoverEntrypointFile()", async function () {
    let entrypointFile = await discoverEntrypointFile(
      joinPaths(process.cwd(), "test", "examples", "specification", "convert"),
      "Catalog.tsp",
    );
    assert.equal(entrypointFile, "Catalog.tsp");
    entrypointFile = await discoverEntrypointFile(
      joinPaths(process.cwd(), "test", "examples", "specification", "convert"),
    );
    assert.equal(entrypointFile, "client.tsp");
    entrypointFile = await discoverEntrypointFile(
      joinPaths(
        process.cwd(),
        "test",
        "examples",
        "specification",
        "contosowidgetmanager",
        "Contoso.WidgetManager",
      ),
    );
    assert.equal(entrypointFile, "client.tsp");
  });
});
