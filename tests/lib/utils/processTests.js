/**
 * @fileoverview Helper to feed in processor information to RuleTester.
 * @author Arpan Laha
 */

"use strict";

var processJSON = require("../../../lib/index").processors[".json"];

module.exports = function(fileName) {
  return Object.assign(processJSON, { filename: fileName });
};
