/**
 * @fileoverview Testing processors
 * @author Arpan Laha
 */

import * as plugin from "../src";
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
  });
});
