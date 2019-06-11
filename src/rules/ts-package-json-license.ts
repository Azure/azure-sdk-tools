/**
 * @fileoverview Rule to force package.json's license value to be set to "MIT".
 * @license Arpan Laha
 */

import structure from "../utils/structure";
import { Rule } from "eslint";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "force package.json's license value to be 'MIT'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-license"
    },
    schema: [] // no options
  },
  create: function(context: Rule.RuleContext) {
    var checkers = structure(context, {
      outer: "license",
      expectedValue: "MIT",
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if license exists at the outermost level
      "VariableDeclarator > ObjectExpression": checkers.existsInFile,

      // check the node corresponding to license to see if it's value is "MIT"
      "VariableDeclarator > ObjectExpression > Property[key.value='license']":
        checkers.outerMatchesExpected
    } as Rule.RuleListener;
  }
};
