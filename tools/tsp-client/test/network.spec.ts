import { assert } from "chai";
import { rewriteGitHubUrl } from "../src/network.js";

describe("Network", function () {
  it("rewriteGitHubUrl", function () {
    const initial =
      "https://github.com/Azure/azure-rest-api-specs/blob/main/specification/cognitiveservices/OpenAI.Inference/main.tsp";
    const expected =
      "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/specification/cognitiveservices/OpenAI.Inference/main.tsp";
    const actual = rewriteGitHubUrl(initial);
    assert.strictEqual(actual, expected);
  });
});
