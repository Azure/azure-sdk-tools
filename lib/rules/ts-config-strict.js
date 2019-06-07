/**
 * @fileoverview Rule to force tsconfig.json's compilerOptions.strict value to be true.
 * @author Arpan Laha
 */

"use strict";

var structure = require("../utils/structure");

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

module.exports = {
  meta: {
    type: "Problem",

    docs: {
      description:
        "force tsconfig.json's compilerOptions.strict value to be true",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-strict"
    },
    fixable: "code",
    schema: [] // no options
  },
  create: function(context) {
    var checkers = structure(context, {
      outer: "compilerOptions",
      inner: "strict",
      expectedValue: true,
      fileName: "tsconfig.json"
    });
    return {
      // callback functions

      // check to see if compilerOptions exists at the outermost level
      "VariableDeclarator > ObjectExpression": checkers.existsInFile,

      // check that strict is a member of compilerOptions
      "Property[key.value='compilerOptions']": checkers.isMemberOf,

      // check the node corresponding to compilerOptions.strict to see if it is set to true
      "VariableDeclarator > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='strict']":
        checkers.innerMatchesExpected
    };
  }
};
