/**
 * @fileoverview Rule to force tsconfig.json's compilerOptions.strict value to be true.
 * @author Arpan Laha
 */

"use strict";

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
        "https://github.com/arpanlaha/eslint-plugin-azure/blob/master/docs/rules/ts-config-strict.md"
    },
    fixable: "code",
    schema: [] // no options
  },
  create: function(context) {
    if (context.getFilename() !== "tsconfig.json") {
      return [];
    }
    return {
      // callback functions
      "VariableDeclaration > VariableDeclarator > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='strict']": function(
        node
      ) {
        node.value.value === true
          ? []
          : context.report({
              node: node,
              message:
                "tsconfig.json: compilerOptions.strict is set to {{ identifier }} when it should be set to true",
              data: {
                identifier: node.value.value
              }
            });
      }
    };
  }
};
