/**
 * @fileoverview Rule to force the original source to be included in the package.
 * @author Arpan Laha
 */

import { getVerifiers, stripPath } from "../utils/verifiers";
import { Rule } from "eslint";
import { ArrayExpression, Literal, Property } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "force the original source to be included in the package",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-include-original-source"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "files"
    });
    return stripPath(context.getFilename()) === "package.json"
      ? ({
          // callback functions

          // check to see if files exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          "ExpressionStatement > ObjectExpression > Property[key.value='files']": (
            node: Property
          ): void => {
            !(node.value.type === "ArrayExpression") &&
              context.report({
                node: node.value,
                message: "files is not set to an array"
              });

            const nodeValue: ArrayExpression = node.value as ArrayExpression;
            const elements: Literal[] = nodeValue.elements as Literal[];

            const pattern = /^(.\/)?src\/?/;

            !elements.find(element => {
              return pattern.test(element.value as string);
            }) &&
              context.report({
                node: nodeValue,
                message: "src is not included in files"
              });
          }
        } as Rule.RuleListener)
      : {};
  }
};
