/**
 * @fileoverview Rule to force package.json's scripts value to at least contain build and test.
 * @author Arpan Laha
 */

import { getVerifiers, stripPath } from "../utils/verifiers";
import { Rule } from "eslint";
import { Property } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "force package.json's scripts value to at least contain build, test, and prepack",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-required-scripts.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const buildVerifiers = getVerifiers(context, {
      outer: "scripts",
      inner: "build"
    });
    const testVerifiers = getVerifiers(context, {
      outer: "scripts",
      inner: "test"
    });
    return stripPath(context.getFilename()) === "package.json"
      ? ({
          // callback functions

          // check to see if scripts exists at the outermost level
          "ExpressionStatement > ObjectExpression": buildVerifiers.existsInFile,

          // check to see if scripts contains both build and test
          "ExpressionStatement > ObjectExpression > Property[key.value='scripts']": (
            node: Property
          ): void => {
            buildVerifiers.isMemberOf(node);
            testVerifiers.isMemberOf(node);
          }
        } as Rule.RuleListener)
      : {};
  }
};
