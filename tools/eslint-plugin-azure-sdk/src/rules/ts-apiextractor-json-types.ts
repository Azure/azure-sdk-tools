/**
 * @file Rule to force the inclusion of type declarations in the package.
 * @author Arpan Laha
 * @author Will Temple
 */

import { Rule } from "eslint";
import { Literal, Property } from "estree";
import { getRuleMetaData, getVerifiers, stripPath } from "../utils";
import { stripFileName } from '../utils/verifiers';

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-apiextractor-json-types",
    "force api-extractor.json to configure types in a consistent way"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "dtsRollup",
      inner: "publicTrimmedFilePath",
      expected: false
    });
    const fileName = context.getFilename();
    return stripPath(fileName) === "api-extractor.json"
      ? ({
          // callback functions
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,
          // check to see if dtsRollup.publicTrimmedFilePath is defined
          "ExpressionStatement > ObjectExpression > Property[key.value='dtsRollup']" : verifiers.isMemberOf,

          // check the node corresponding to types to see if its value is a TypeScript declaration file
          "ExpressionStatement > ObjectExpression > Property[key.value='dtsRollup'] > ObjectExpression > Property[key.value='publicTrimmedFilePath']": (
            node: Property
          ): void => {
            if (node.value.type !== "Literal" || typeof node.value.value !== "string") {
              context.report({
                node: node.value,
                message: ".d.ts rollup path is not set to a string"
              });
            } else if (typeof node.value.value !== "string") {

            }
            const nodeValue = node.value as Literal;
            const baseName = stripPath(nodeValue.value as string);
            // Get the name of the package directory
            const packageDirectory = stripPath(stripFileName(fileName));
            const typesOutputName = baseName.replace(/\..*$/, "");
            // filename ending in '.d.ts' and matches package name
            if (!/\.d\.ts$/.test(baseName as string)) {
              context.report({
                node: nodeValue,
                message:
                  "provided .d.ts rollup path is not a TypeScript declaration file"
              });
            } else if (typesOutputName !== packageDirectory) {
              context.report({
                node: nodeValue,
                message:
                  `provided .d.ts rollup path should be named '${packageDirectory}.d.ts' after the package directory` 
              })
            }
          }
        } as Rule.RuleListener)
      : {};
  }
};
