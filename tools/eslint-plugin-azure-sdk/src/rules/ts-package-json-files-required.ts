/**
 * @fileoverview Rule to force package.json's files value to contain paths to the package contents.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { ArrayExpression, Literal, Property } from "estree";
import { getRuleMetaData, getVerifiers, stripPath } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-package-json-files-required",
    "requires package.json's files value to contain paths to the package contents"
  ),
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

            const nodeValue = node.value as ArrayExpression;
            const elements = nodeValue.elements as Literal[];

            elements.every((element: Literal): boolean => {
              // looks for 'dist' with optional leading './' and optional trailing '/'
              return !/^(.\/)?((dist\/)|(dist$))/.test(element.value as string);
            }) &&
              context.report({
                node: nodeValue,
                message: "dist is not included in files"
              });

            elements.every((element: Literal): boolean => {
              // looks for 'dist-esm/src' with optional leading './' and optional trailing '/'
              return !/^(.\/)?dist-esm\/((src\/)|(src$))/.test(
                element.value as string
              );
            }) &&
              context.report({
                node: nodeValue,
                message: "dist-esm/src is not included in files"
              });

            elements.every((element: Literal): boolean => {
              // looks for 'src' with optional leading './' and optional trailing '/ '
              return !/^(.\/)?((src\/)|(src$))/.test(element.value as string);
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
