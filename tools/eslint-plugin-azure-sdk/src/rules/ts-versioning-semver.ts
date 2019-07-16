/**
 * @fileoverview Rule to force adherence to SemVer guidelines.
 * @author Arpan Laha
 */

import { getVerifiers, stripPath } from "../utils";
import { Rule } from "eslint";
import { Literal, Property } from "estree";
import { getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-versioning-semver",
    "force adherence to SemVer guidelines"
  ),
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
            const version = nodeValue.value as string;

            // check for violations specific to semver
            if (!/^((0|[1-9](\d*))\.){2}(0|[1-9](\d*))(-|$)/.test(version)) {
              context.report({
                node: nodeValue,
                message: "version is not in semver"
              });
              return;
            }
            // check that if preview is in proper syntax if provided
            !/^((0|[1-9](\d*))\.){2}(0|[1-9](\d*))(-preview\.(0|([1-9](\d*))))?$/.test(
              version
            ) &&
              context.report({
                node: nodeValue,
                message: "preview format is not x.y.z-preview.i"
              });

            // check if major version is 0
            /^0\./.test(version) &&
              context.report({
                node: nodeValue,
                message: "major version should not be set to 0"
              });
          }
        } as Rule.RuleListener)
      : {};
  }
};
