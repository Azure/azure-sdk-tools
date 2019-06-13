/**
 * @fileoverview Rule to force package.json's homepage value to be a URL pointing to your library's readme inside the git repo.
 * @author Arpan Laha
 */

import structure from "../utils/structure";
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
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-homepage"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = structure(context, {
      outer: "homepage",
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if homepage exists at the outermost level
      "VariableDeclarator > ObjectExpression": verifiers.existsInFile,

      // check the node corresponding to homepage to see if its value is a URL pointing to your library's readme inside the git repo
      "VariableDeclarator > ObjectExpression > Property[key.value='homepage']": (
        node: Property
      ): void => {
        if (context.getFilename().replace(/^.*[\\\/]/, "") === "package.json") {
          const regex = /^https:\/\/github.com\/Azure\/azure-sdk-for-js\/blob\/master\/sdk\/([a-z-_]+\/)+README\.md$/;

          const nodeValue: Literal = node.value as Literal;
          const value: string = nodeValue.value as string;

          !regex.test(value) &&
            context.report({
              node: node,
              message:
                "homepage is not a URL pointing to your library's readme inside the git repo"
            });
        }
      }
    } as Rule.RuleListener;
  }
};
