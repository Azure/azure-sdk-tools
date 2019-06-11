/**
 * @fileoverview Rule to force package.json's bugs.url value to be "https://github.com/Azure/azure-sdk-for-js/issues".
 * @author Arpan Laha
 */

import { structure } from "../utils/structure";
import { Rule } from "eslint";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "force package.json's bugs.url value to be 'https://github.com/Azure/azure-sdk-for-js/issues'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-bugs"
    },
    schema: [] // no options
  },
  create: function(context: Rule.RuleContext) {
    var checkers = structure(context, {
      outer: "bugs",
      inner: "url",
      expectedValue: "https://github.com/Azure/azure-sdk-for-js/issues",
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if bugs exists at the outermost level
      "VariableDeclarator > ObjectExpression": checkers.existsInFile,

      // check that url is a member of bugs
      "Property[key.value='bugs']": checkers.isMemberOf,

      // check the node corresponding to bugs.url to see if it is set to 'https://github.com/Azure/azure-sdk-for-js/issues'
      "VariableDeclarator > ObjectExpression > Property[key.value='bugs'] > ObjectExpression > Property[key.value='url']":
        checkers.innerMatchesExpected
    } as Rule.RuleListener;
  }
};
