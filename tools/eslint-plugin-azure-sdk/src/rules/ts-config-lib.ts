/**
 * @fileoverview Rule to force tsconfig.json's compilerOptions.lib value to be an empty array.
 * @author Arpan Laha
 */

import { getVerifiers, stripPath } from "../utils/verifiers";
import { Rule } from "eslint";
import { ArrayExpression, Property } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "force tsconfig.json's compilerOptions.lib value to be an empty array",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-lib.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "compilerOptions",
      inner: "lib"
    });
    return stripPath(context.getFilename()) === "tsconfig.json"
      ? ({
          // callback functions

          // check to see if compilerOptions exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check that lib is a member of compilerOptions
          "ExpressionStatement > ObjectExpression > Property[key.value='compilerOptions']":
            verifiers.isMemberOf,

          // check the node corresponding to compilerOptions.lib to see if it is set to an empty array
          "ExpressionStatement > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='lib']": (
            node: Property
          ): void => {
            if (node.value.hasOwnProperty("elements")) {
              const nodeValue: ArrayExpression = node.value as ArrayExpression;
              nodeValue.elements.length !== 0 &&
                context.report({
                  node: node,
                  message: "compilerOptions.lib is not set to an empty array"
                });
            } else {
              context.report({
                node: node,
                message: "compilerOptions.lib is not set to an empty array"
              });
            }
          }
        } as Rule.RuleListener)
      : {};
  }
};
