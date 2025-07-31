import { assert } from "chai";
import { describe, it } from "vitest";
import {
  compileTsp,
  discoverEntrypointFile,
  tryParseEmitterOptionAsObject,
} from "../src/typespec.js";
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

  it("Check discoverEntrypointFile() with unexpected entrypoint name", async function () {
    try {
      await discoverEntrypointFile(
        joinPaths(process.cwd(), "test", "examples", "specification", "unexpected-entrypoint-name"),
      );
    } catch (e) {
      assert.equal(e.message, "No main.tsp or client.tsp found");
    }
  });

  describe("tryParseEmitterOptionAsObject", function () {
    it("returns object for JSON object string", function () {
      const str = `{"name":"@azure/eventgrid-namespaces-2","version":"1.0.3"}`;
      const result = tryParseEmitterOptionAsObject(str);

      assert.isObject(result);
      assert.deepEqual(result, {
        name: "@azure/eventgrid-namespaces-2",
        version: "1.0.3",
      });
    });

    it("returns string for string 'true'", function () {
      const str = "true";
      const result = tryParseEmitterOptionAsObject(str);

      assert.isString(result);
      assert.strictEqual(result, "true");
    });

    it("returns string for string 'null'", function () {
      const str = "null";
      const result = tryParseEmitterOptionAsObject(str);

      assert.isString(result);
      assert.strictEqual(result, "null");
    });

    it("returns string for string '[]'", function () {
      const str = "[]";
      const result = tryParseEmitterOptionAsObject(str);

      assert.isString(result);
      assert.strictEqual(result, "[]");
    });

    it("returns string for a normal string", function () {
      const str = "azure";
      const result = tryParseEmitterOptionAsObject(str);

      assert.isString(result);
      assert.strictEqual(result, "azure");
    });

    it("returns string for a path string", function () {
      const str = "sdk/some/path/src/generated";
      const result = tryParseEmitterOptionAsObject(str);

      assert.isString(result);
      assert.strictEqual(result, "sdk/some/path/src/generated");
    });
  });
});
