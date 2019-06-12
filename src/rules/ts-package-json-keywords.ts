/**
 * @fileoverview Rule to force package.json's keywords value to contain at least "Azure" and "cloud".
 * @author Arpan Laha
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
      description:
        "force package.json's author value to be 'Microsoft Corporation'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-author"
    },
    schema: [] // no options
  },
  create: function(context: Rule.RuleContext) {
    var checkers = structure(context, {
      outer: "keywords",
      expected: ["Azure", "cloud"],
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if keywords exists at the outermost level
      "VariableDeclarator > ObjectExpression": checkers.existsInFile,

      // check the node corresponding to keywords to see if its value contains "Azure" and "cloud"
      "VariableDeclarator > ObjectExpression > Property[key.value='keywords']":
        checkers.outerContainsExpected
    } as Rule.RuleListener;
  }
};
