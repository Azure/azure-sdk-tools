/**
 * @fileoverview Rule to force package.json's files value to contain paths to the package contents.
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
      description: "files value to contain paths to the package contents",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-files-required"
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

          "Property[key.value='files'": (node: Property): void => {
            const nodeValue: ArrayExpression = node.value as ArrayExpression;
            const elements: Literal[] = nodeValue.elements as Literal[];

            const distPattern = /^(.\/)?dist\/?$/;
            !elements.find((element: Literal): boolean => {
              return distPattern.test(element.value as string);
            }) &&
              context.report({
                node: nodeValue,
                message: "dist is not included in files"
              });

            const distESMPattern = /^(.\/)?dist-esm\/?$/;
            !elements.find((element: Literal): boolean => {
              return distESMPattern.test(element.value as string);
            }) &&
              context.report({
                node: nodeValue,
                message: "dist-esm is not included in files"
              });
          }
        } as Rule.RuleListener)
      : {};
  }
};
