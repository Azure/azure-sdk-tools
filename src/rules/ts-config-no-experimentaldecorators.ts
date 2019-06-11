/**
 * @fileoverview Rule to force tsconfig.json's compilerOptions.experimentalDecorators value to be false.
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
        "force tsconfig.json's compilerOptions.experimentalDecorators value to be false",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-experimentalDecorators"
    },
    schema: [] // no options
  },
  create: function(context: Rule.RuleContext) {
    var checkers = structure(context, {
      outer: "compilerOptions",
      inner: "experimentalDecorators",
      expectedValue: false,
      fileName: "tsconfig.json"
    });
    return {
      // callback functions

      // check to see if compilerOptions exists at the outermost level
      "VariableDeclarator > ObjectExpression": checkers.existsInFile,

      // check the node corresponding to compilerOptions.experimentalDecorators to see if it is set to false
      "VariableDeclarator > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='experimentalDecorators']":
        checkers.innerMatchesExpected
    } as Rule.RuleListener;
  }
};
