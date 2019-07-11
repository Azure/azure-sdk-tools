/**
 * @fileoverview Rule to force package.json's homepage value to be a URL pointing to your library's readme inside the git repo.
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
        "force package.json's homepage value to be a URL pointing to your library's readme inside the git repo",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-homepage.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "homepage"
    });
    return stripPath(context.getFilename()) === "package.json"
      ? ({
          // callback functions

          // check to see if homepage exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check the node corresponding to homepage to see if its value is a URL pointing to your library's readme inside the git repo
          "ExpressionStatement > ObjectExpression > Property[key.value='homepage']": (
            node: Property
          ): void => {
            const regex = /^https:\/\/github.com\/Azure\/azure-sdk-for-js\/blob\/master\/sdk\/(([a-z]+-)*[a-z]+\/)+(README\.md)?$/;

            const nodeValue: Literal = node.value as Literal;
            const value: string = nodeValue.value as string;

            !regex.test(value) &&
              context.report({
                node: node,
                message:
                  "homepage is not a URL pointing to your library's readme inside the git repo"
              });
          }
        } as Rule.RuleListener)
      : {};
  }
};
