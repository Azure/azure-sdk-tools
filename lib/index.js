/**
 * @fileoverview Linting rules for the JavaScript/TypeScript Azure SDK
 * @author Arpan Laha
 */
"use strict";

//------------------------------------------------------------------------------
// Requirements
//------------------------------------------------------------------------------

var requireIndex = require("requireindex");

//------------------------------------------------------------------------------
// Plugin Definition
//------------------------------------------------------------------------------

// import all rules in lib/rules
module.exports.rules = requireIndex(__dirname + "/rules");

// import processors
module.exports.processors = {
  // add your processors here
  ".json": {
    preprocess: function(text) {
      const code = "const json = " + text;
      return [code];
    },
    postprocess: function(messages) {
      return messages[0];
    }
  }
};
