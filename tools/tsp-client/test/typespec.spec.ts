import { assert, config } from "chai";
import { describe, it } from "mocha";
import { getEmitterOptions } from "../src/typespec.js";

config.truncateThreshold = 0;

describe("typespec", function () {
  it("emitter options conversion to record", async () => {
    const actual = await getEmitterOptions(".", ".", "typespec-foo", false, "typespec-foo.emitter-output-dir=.");
    const expected = {
      "typespec-foo": {
        "emitter-output-dir": "."
      }
    };
    assert.deepEqual(actual, expected);
  });
});
