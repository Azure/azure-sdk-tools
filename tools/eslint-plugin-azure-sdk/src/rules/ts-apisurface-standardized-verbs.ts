/**
 * @fileoverview Rule to require client methods to use standardized verb prefixes and suffixes.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { ClassDeclaration, Identifier, MethodDefinition } from "estree";
import { getPublicMethods, getRuleMetaData } from "../utils";

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

      // call on Client classes
      "ClassDeclaration[id.name=/Client$/]": (node: ClassDeclaration): void => {
        getPublicMethods(node).forEach((method: MethodDefinition): void => {
          const key = method.key as Identifier;
          const methodName = key.name;

          // report if no matches
          if (
            verbRegexes.every(
              (verbRegex: RegExp): boolean => !verbRegex.test(methodName)
            )
          ) {
            context.report({
              node: method,
              message: `method name ${methodName} does not include one of the approved verb prefixes or suffixes`
            });
          }
        });
      }
    } as Rule.RuleListener)
};
