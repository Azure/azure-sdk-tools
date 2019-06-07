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
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-strict"
    },
    fixable: "code",
    schema: [] // no options
  },
  create: function(context) {
    return {
      // callback functions

      // check to see if compilerOptions exists at the outermost level
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

      // check that strict is a member of compilerOptions
      "Property[key.value='compilerOptions']": function(node) {
        const properties = node.value.properties;
        let foundStrict = false;
        for (const property of properties) {
          if (property.key && property.key.value === "strict") {
            foundStrict = true;
            break;
          }
        }
        context.getFilename() === "tsconfig.json"
          ? foundStrict
            ? []
            : context.report({
                node: node,
                message:
                  "tsconfig.json: strict is not a member of compilerOptions"
              })
          : [];
      },

      // check the node corresponding to compilerOptions.strict to see if it is set to true
      "VariableDeclarator > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='strict']": function(
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
