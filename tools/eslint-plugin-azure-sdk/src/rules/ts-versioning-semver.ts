/**
 * @fileoverview Rule to force adherence to semver guidelines.
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
      description: "force adherence to semver guidelines",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-versioning-no-version-0"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "version"
    });
    return stripPath(context.getFilename()) === "package.json"
      ? ({
          // callback functions

          // check to see if version exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check the node corresponding to types to see if its value is a TypeScript declaration file
          "ExpressionStatement > ObjectExpression > Property[key.value='version']": (
            node: Property
          ): void => {
            if (node.value.type !== "Literal") {
              context.report({
                node: node.value,
                message: "version is not set to a string"
              });
              return;
            }
            const nodeValue: Literal = node.value as Literal;

            // check for violations specific to semver
            const semverPattern = /^((0|[1-9](\d*))\.){2}(0|[1-9](\d*))(-|$)/;
            !semverPattern.test(nodeValue.value as string) &&
              context.report({
                node: nodeValue,
                message: "version is not in semver"
              });

            // check that if preview is in proper syntax if provided
            const previewPattern = /^((0|[1-9](\d*))\.){2}(0|[1-9](\d*))(-preview-(0|([1-9](\d*))))?$/;
            semverPattern.test(nodeValue.value as string) &&
              !previewPattern.test(nodeValue.value as string) &&
              context.report({
                node: nodeValue,
                message: "preview format is not x.y.z-preview-i"
              });

            // check that major version is not set to 0
            const major0Pattern = /^0\./;
            semverPattern.test(nodeValue.value as string) &&
              major0Pattern.test(nodeValue.value as string) &&
              context.report({
                node: nodeValue,
                message: "major version should not be set to 0"
              });
          }
        } as Rule.RuleListener)
      : {};
  }
};
