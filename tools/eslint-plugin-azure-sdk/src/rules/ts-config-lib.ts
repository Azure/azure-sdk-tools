/**
 * @fileoverview Rule to force tsconfig.json's compilerOptions.lib value to be an empty array.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { ArrayExpression, Property } from "estree";
import { getRuleMetaData, getVerifiers, stripPath } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-config-lib",
    "force tsconfig.json's compilerOptions.lib value to be an empty array"
  ),
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
              const nodeValue = node.value as ArrayExpression;
              nodeValue.elements.length !== 0 &&
                context.report({
                  node: nodeValue,
                  message: "compilerOptions.lib is not set to an empty array"
                });
            } else {
              context.report({
                node: node.value,
                message: "compilerOptions.lib is not set to an empty array"
              });
            }
          }
        } as Rule.RuleListener)
      : {};
  }
};
