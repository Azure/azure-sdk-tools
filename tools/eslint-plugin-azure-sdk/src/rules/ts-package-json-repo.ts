/**
 * @fileoverview Rule to force package.json's repository value to be set to github:Azure/azure-sdk-for-js.
 * @author Arpan Laha
 */

import { getVerifiers, stripPath } from "../utils/verifiers";
import { Rule } from "eslint";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "force package.json's repository value to be 'github:Azure/azure-sdk-for-js'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-repo.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "repository",
      expected: "github:Azure/azure-sdk-for-js"
    });
    return stripPath(context.getFilename()) === "package.json"
      ? {
          // callback functions

          // check to see if repository exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check the node corresponding to repository to see if its value is github:Azure/azure-sdk-for-js
          "ExpressionStatement > ObjectExpression > Property[key.value='repository']":
            verifiers.outerMatchesExpected
        }
      : {};
  }
};
