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

          // check that files contains dist, dist-esm, and src
          "ExpressionStatement > ObjectExpression > Property[key.value='files']": (
            node: Property
          ): void => {
            // check that files is set to an array
            if (node.value.type !== "ArrayExpression") {
              context.report({
                node: node.value,
                message: "files is not set to an array"
              });
              return;
            }

            const nodeValue: ArrayExpression = node.value as ArrayExpression;
            const elements: Literal[] = nodeValue.elements as Literal[];

            const distPattern = /^(.\/)?((dist\/)|(dist$))/; // looks for 'dist' with optional leading './' and optional trailing '/'
            elements.every((element: Literal): boolean => {
              return !distPattern.test(element.value as string);
            }) &&
              context.report({
                node: nodeValue,
                message: "dist is not included in files"
              });

            const distESMPattern = /^(.\/)?dist-esm\/((src\/)|(src$))/; // looks for 'dist-esm/src' with optional leading './' and optional trailing '/'
            elements.every((element: Literal): boolean => {
              return !distESMPattern.test(element.value as string);
            }) &&
              context.report({
                node: nodeValue,
                message: "dist-esm/src is not included in files"
              });

            const srcPattern = /^(.\/)?((src\/)|(src$))/; // looks for 'src' with optional leading './' and optional trailing '/ '
            elements.every((element: Literal): boolean => {
              return !srcPattern.test(element.value as string);
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
