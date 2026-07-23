import { describe, it } from "vitest";
import { doesFileExist, isValidUrl } from "../src/network.js";
import { assert } from "chai";

describe("Verify network functions", () => {
  it("Check doesFileExist true", async () => {
    const exists = await doesFileExist("./test/examples/tspconfig-custom-service-dir.yaml");
    assert.isTrue(exists);
  });

  it("Check doesFileExist false", async () => {
    const exists = await doesFileExist("./test/examples/fake_file.yaml");
    assert.isFalse(exists);
  });

  it("Check isValidUrl true", async () => {
    assert.isTrue(
      isValidUrl("https://github.com/Azure/azure-sdk-tools/blob/main/tools/tsp-client/README.md"),
    );
  });

  it("Check isValidUrl false", async () => {
    assert.isFalse(isValidUrl("./test/examples/tspconfig-custom-service-dir.yaml"));
  });
});
