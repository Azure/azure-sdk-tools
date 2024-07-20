import { assert } from "chai";
import { normalizeDirectory, readTspLocation } from "../src/fs.js";
import { describe, it } from "node:test";

describe("Parse tsp-location.yaml", function () {
  it("Normalize tsp-location.yaml directories", async function () {
    const tspLocation = await readTspLocation("./test/examples/sdk/badtsplocation");
    const directory = normalizeDirectory(tspLocation.directory);
    assert.equal(directory, "specification/contosowidgetmanager/Contoso.WidgetManager");
    const additionalDirectories = tspLocation.additionalDirectories?.map(normalizeDirectory);
    assert.equal(additionalDirectories?.length, 2);
    if (additionalDirectories) {
      assert.equal(
        additionalDirectories[0],
        "specification/contosowidgetmanager/Contoso.WidgetManager.Shared",
      );
      assert.equal(
        additionalDirectories[1],
        "specification/contosowidgetmanager/Contoso.WidgetManager.Utils",
      );
    }
  });
});
