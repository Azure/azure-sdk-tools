/**
 * @fileoverview Rule to force tsconfig.json's compilerOptions.module value to "es6".
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
      description:
        "force tsconfig.json's compilerOptions.module value to be set to 'es6'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-module.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "compilerOptions",
      inner: "module"
    });
    return stripPath(context.getFilename()) === "tsconfig.json"
      ? ({
          // callback functions

          // check to see if compilerOptions exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check that module is a member of compilerOptions
          "ExpressionStatement > ObjectExpression > Property[key.value='compilerOptions']":
            verifiers.isMemberOf,

          // check the node corresponding to compilerOptions.module to see if it is set to es6
          "ExpressionStatement > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='module']": (
            node: Property
          ): void => {
            // check to see that node value is a Literal before casting
            node.value.type !== "Literal" &&
              context.report({
                node: node.value,
                message:
                  "compilerOptions.module is not set to a literal (string | boolean | null | number | RegExp)"
              });

            const nodeValue: Literal = node.value as Literal;

            // check that module is set to es6
            !/^es6$/i.test(nodeValue.value as string) &&
              context.report({
                node: node,
                message:
                  "compilerOptions.module is set to {{ identifier }} when it should be set to ES6",
                data: {
                  identifier: nodeValue.value as string
                }
              });
          }
        } as Rule.RuleListener)
      : {};
  }
};
