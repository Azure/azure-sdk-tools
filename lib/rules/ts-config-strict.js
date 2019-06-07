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
    return {
      // callback functions

      // check to see if compilerOptions exists
      "VariableDeclarator > ObjectExpression": function(node) {
        const properties = node.properties;
        let foundCompilerOptions = false;
        for (const property of properties) {
          if (property.key && property.key.value === "compilerOptions") {
            foundCompilerOptions = true;
            break;
          }
        }
        context.getFilename() === "tsconfig.json"
          ? foundCompilerOptions
            ? []
            : context.report({
                node: node,
                message:
                  "tsconfig.json: compilerOptions does not exist at the outermost level"
              })
          : [];
      },

      // check the node corresponding to compilerOptions.strict to see if it is set to true
      "VariableDeclarator > ObjectExpression > Property[key.value='compilerOptions'] Property[key.value='strict']": function(
        node
      ) {
        context.getFilename() === "tsconfig.json"
          ? node.value.value === true
            ? []
            : context.report({
                node: node,
                message:
                  "tsconfig.json: compilerOptions.strict is set to {{ identifier }} when it should be set to true",
                data: {
                  identifier: node.value.value
                }
              })
          : [];
      }
    };
  }
};
