/**
 * @fileoverview Rule to force package.json's repository value to be set to github:Azure/azure-sdk-for-js.
 * @author Arpan Laha
 */

import { structure } from "../utils/structure";
import { Rule } from "eslint";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export default {
  meta: {
    type: "problem",

    docs: {
      description:
        "force package.json's repository value to be 'github:Azure/azure-sdk-for-js'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-repo"
    },
    schema: [] // no options
  },
  create: function(context: Rule.RuleContext) {
    var checkers = structure(context, {
      outer: "repository",
      expectedValue: "github:Azure/azure-sdk-for-js",
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if compilerOptions exists at the outermost level
      "VariableDeclarator > ObjectExpression": checkers.existsInFile,

      // check that repository is a member of compilerOptions
      "Property[key.value='compilerOptions']": checkers.isMemberOf,

      // check the node corresponding to repository to see if it's value is github:Azure/azure-sdk-for-js
      "VariableDeclarator > ObjectExpression > Property[key.value='repository']":
        checkers.outerMatchesExpected
    } as Rule.RuleListener;
  }
};
