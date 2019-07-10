/**
 * @fileoverview Rule to force main to point to a CommonJS or UMD module.
 * @author Arpan Laha
 */

import { getVerifiers, stripPath } from "../utils/verifiers";
import { Rule } from "eslint";
import { Literal, Property } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "force main to point to a CommonJS or UMD module",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-main-is-cjs.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "main"
    });
    return stripPath(context.getFilename()) === "package.json"
      ? ({
          // callback functions

          // check to see if main exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check the node corresponding to main to see if its value is dist/index.js
          "ExpressionStatement > ObjectExpression > Property[key.value='main']": (
            node: Property
          ): void => {
            if (node.value.type !== "Literal") {
              context.report({
                node: node.value,
                message: "name is not a Literal"
              });
            }

            const nodeValue: Literal = node.value as Literal;

            const regex = /^(\.\/)?dist\/index\.js$/;

            !regex.test(nodeValue.value as string) &&
              context.report({
                node: nodeValue,
                message:
                  "main is set to {{ identifier }} when it should be set to dist/index.js",
                data: {
                  identifier: nodeValue.value as string
                }
              });
          }
        } as Rule.RuleListener)
      : {};
  }
};
