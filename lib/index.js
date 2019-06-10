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

// export processors
module.exports.processors = {
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

//export configs
module.exports.configs = {
  recommended: {
    plugins: ["azure"],
    env: ["node"],
    parser: "@typescript-eslint/parser",
    rules: {
      "azure/ts-config-allowsyntheticdefaultimports": "error",
      "azure/ts-config-declaration": "error",
      "azure/ts-config-esmoduleinterop": "error",
      "azure/ts-config-forceconsistentcasinginfilenames": "error",
      "azure/ts-config-isolatedmodules": "warning",
      "azure/ts-config-module": "error",
      "azure/ts-config-no-experimentaldecorators": "error",
      "azure/ts-config-strict": "error"
    }
  }
}