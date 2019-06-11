/**
 * @fileoverview Testing processors
 * @author Arpan Laha
 */

import plugin from "../src";
import { assert } from "chai";

/**
 * Test structure taken from https://github.com/azeemba/eslint-plugin-json/blob/master/test/test.js
 */
describe("plugin", function() {
  describe("structure", function() {
    it("processors should a member of the plugin", function() {
      assert.property(
        plugin,
        "processors",
        "processors is not a member of the plugin"
      );
    });
    it(".json should be a member of processors", function() {
      assert.property(
        plugin.processors,
        ".json",
        ".json is not a member of processors"
      );
    });
    it("preprocess should be a member of .json", function() {
      assert.property(
        plugin.processors[".json"],
        "preprocess",
        "preprocess is not a member of .json"
      );
    });
    it("postprocess should be a member of .json", function() {
      assert.property(
        plugin.processors[".json"],
        "postprocess",
        "postprocess is not a member of .json"
      );
    });
  });
  describe("preprocess", function() {
    const preprocess = plugin.processors[".json"].preprocess;
    it("preprocess should prepend 'const json = ' to the input and return its singleton array", function() {
      const input = "input";
      const output = preprocess(input);
      assert.isArray(output, "preprocess should always return an array");
      assert.strictEqual(output[0], "const json = input");
    });
  });
  /**
   * TODO: Implement postprocess tests after functionality complete
   */
});
