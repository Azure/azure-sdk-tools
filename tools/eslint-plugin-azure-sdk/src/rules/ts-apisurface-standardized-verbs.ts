/**
 * @fileoverview Rule to require client methods to use standardized verb prefixes and suffixes.
 * @author Arpan Laha
 */

import { TSESTree } from "@typescript-eslint/experimental-utils";
import { Rule } from "eslint";
import { ClassDeclaration, Identifier, MethodDefinition } from "estree";
import { getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

/**
 * A list of regexes corresponding to approved verb prefixes and suffixes
 * Needs updating as definitions change
 */
const verbRegexes = [
  /^create($|([A-Z]))/,
  /^upsert($|([A-Z]))/,
  /^set($|([A-Z]))/,
  /^update($|([A-Z]))/,
  /^replace($|([A-Z]))/,
  /^append($|([A-Z]))/,
  /^add($|([A-Z]))/,
  /^get($|([A-Z]))/,
  /^list($|([A-Z][a-zA-Z]*s$))/,
  /((^e)|E)xists$/,
  /^delete($|([A-Z]))/,
  /^remove($|([A-Z]))/
];

export = {
  meta: getRuleMetaData(
    "ts-apisurface-standardized-verbs",
    "require client methods to use standardized verb prefixes and suffixes"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener =>
    ({
      // callback functions

      ClassDeclaration: (node: ClassDeclaration): void => {
        if (node.id === null || !/Client$/.test(node.id.name)) {
          return;
        }

        const publicMethods = node.body.body.filter(
          (method: MethodDefinition): boolean => {
            const TSMethod = method as TSESTree.MethodDefinition;
            return (
              method.type === "MethodDefinition" &&
              TSMethod.accessibility !== "private"
            );
          }
        );
        publicMethods.forEach((method: MethodDefinition): void => {
          const key = method.key as Identifier;
          if (
            verbRegexes.every(
              (verbRegex: RegExp): boolean => !verbRegex.test(key.name)
            )
          ) {
            context.report({
              node: method,
              message: `method name ${key.name} does not include one of the approved verb prefixes or suffixes`
            });
          }
        });
      }
    } as Rule.RuleListener)
};
