/**
 * @fileoverview Rule to force usage of built-in promises over external ones.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Literal, Property, Function } from "estree";
import {} from "@typescript-eslint/typescript-estree"

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "force usage of built-in promises over external ones",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-use-promises.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "types",
      expected: false
    });
    return {
      "Function[returnType.typeAnnotation.typeName.name='Promise']": (node: Function): void => {
        node = node as any
        const returnType = function.retrunTy
      }
    } as Rule.RuleListener
  }
};
