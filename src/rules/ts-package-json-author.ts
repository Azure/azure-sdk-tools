/**
 * @fileoverview Rule to force package.json's author value to be set to "Microsoft Corporation".
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
      outer: "author",
      expectedValue: "Microsoft Corporation",
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if author exists at the outermost level
      "VariableDeclarator > ObjectExpression": checkers.existsInFile,

      // check the node corresponding to author to see if it's value is "Microsoft Corporation"
      "VariableDeclarator > ObjectExpression > Property[key.value='author']":
        checkers.outerMatchesExpected
    } as Rule.RuleListener;
  }
};
