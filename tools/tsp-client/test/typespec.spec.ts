import { assert } from "chai";
import { compileTsp } from "../src/typespec.js";
import { describe, it } from "node:test";
import { joinPaths } from "@typespec/compiler";

describe("Check diagnostic reporting", function () {
  it("Check diagnostic format", async function () {
        compileTsp({
            emitterPackage: "@azure-tools/typespec-ts",
            outputPath: joinPaths(process.cwd(), "examples"),
            resolvedMainFilePath: joinPaths(process.cwd(), "examples", "specification", "diagnostics", "main.tsp"),
            additionalEmitterOptions: "",
            saveInputs: false
        }).then(
            () => {
                assert.fail("Expected error but got none");
            },
            (e) => {
                assert.ok(e.message.includes("test/examples/specification/diagnostics/main.tsp:38:19 - error @typespec/versioning/using-versioned-library: Namespace 'Contoso.WidgetManager' is referencing types from versioned namespace 'Azure.Core' but didn't specify which versions with @useDependency."));
            }
        );
  });
});
