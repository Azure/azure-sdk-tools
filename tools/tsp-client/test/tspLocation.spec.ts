import { assert } from "chai";
import { normalizeDirectory, readTspLocation } from "../src/fs.js";
import { describe, it } from "node:test";
import { writeTspLocationYaml } from "../src/utils.js";
import { stat } from "node:fs/promises";

describe("Verify tsp-location.yaml", function () {
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

  it("Write tsp-location.yaml", async function () {
    const tspLocation = {
      directory: "specification/contosowidgetmanager/Contoso.WidgetManager",
      commit: "1234567",
      repo: "foo",
      additionalDirectories: [
        "specification/contosowidgetmanager/Contoso.WidgetManager.Shared",
        "specification/contosowidgetmanager/Contoso.WidgetManager.Utils",
      ],
    };
    await writeTspLocationYaml(tspLocation, "./test/examples/");

    const tspLocationFile = await stat("./test/examples/tsp-location.yaml");
    assert.isTrue(tspLocationFile.isFile());
  });
});
