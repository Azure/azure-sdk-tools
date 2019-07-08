/**
 * @fileoverview Rule to force the inclusion of type declarations in the package.
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
      description: "force the inclusion of type declarations in the package",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-types"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "types",
      expected: false
    });
    return stripPath(context.getFilename()) === "package.json"
      ? ({
          // callback functions

          // check to see if types exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check the node corresponding to types to see if its value is a TypeScript declaration file
          "ExpressionStatement > ObjectExpression > Property[key.value='types']": (
            node: Property
          ): void => {
            node.value.type !== "Literal" &&
              context.report({
                node: node.value,
                message: "types is not set to a string"
              });
            const nodeValue: Literal = node.value as Literal;

            const pattern = /\.d\.ts$/; // filename ending in '.d.ts'
            !pattern.test(nodeValue.value as string) &&
              context.report({
                node: nodeValue,
                message:
                  "provided types path is not a TypeScript declaration file"
              });
          }
        } as Rule.RuleListener)
      : {};
  }
};
