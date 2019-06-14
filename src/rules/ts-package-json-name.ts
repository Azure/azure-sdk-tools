/**
 * @fileoverview Rule to force package.json's name value to be set to @azure/<service>.
 * @author Arpan Laha
 */

import getVerifiers from "../utils/verifiers";
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
        "force package.json's name value to be set to @azure/<service>",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-name"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "name",
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if name exists at the outermost level
      "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

      // check the node corresponding to name to see if its value is @azure/<service>
      "ExpressionStatement > ObjectExpression > Property[key.value='name']": (
        node: Property
      ): void => {
        if (context.getFilename().replace(/^.*[\\\/]/, "") === "package.json") {
          const prefixRegex = /^@azure\//;

          const nodeValue: Literal = node.value as Literal;
          const value: string = nodeValue.value as string;

          !prefixRegex.test(value) &&
            context.report({
              node: node,
              message: "name is not set to @azure/<service>"
            });

          const kebabRegex = /^@azure\/([a-z]+-)*[a-z]+$/;

          prefixRegex.test(value) &&
            !kebabRegex.test(value) &&
            context.report({
              node: node,
              message:
                "service name is not in kebab-case (lowercase and separated by hyphens)"
            });
        }
      }
    } as Rule.RuleListener;
  }
};
