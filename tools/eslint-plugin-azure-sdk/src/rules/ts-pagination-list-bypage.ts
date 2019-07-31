/**
 * @fileoverview Rule to require byPage methods returned in client list methods to contain continuationToken and maxPageSize options.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import {
  FunctionExpression,
  ArrowFunctionExpression,
  Identifier,
  ObjectPattern,
  Pattern,
  Property
} from "estree";
import { getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-pagination-list",
    "require byPage methods returned in client list methods to contain continuationToken and maxPageSize options"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener =>
    ({
      // callback functions

      // call on return statements returning objects from client list methods
      "ClassDeclaration[id.name=/Client$/] MethodDefinition[key.name=/^list($|([A-Z][a-zA-Z]*s$))/] ReturnStatement > ObjectExpression > Property[key.name=byPage]": (
        node: Property
      ): void => {
        const byPage = node.value as
          | ArrowFunctionExpression
          | FunctionExpression;

        // look for continuationToken and maxPageSize options
        ["continuationToken", "maxPageSize"].forEach(
          (expectedName: string): void => {
            const objectParams = byPage.params.filter(
              (param: Pattern): boolean => param.type === "ObjectPattern"
            ) as ObjectPattern[];

            // report if no object params or every object doesn't have the excected name as a property
            if (
              objectParams.length === 0 ||
              objectParams.every(
                (objectParam: ObjectPattern): boolean =>
                  !objectParam.properties
                    .filter(
                      (property: Property): boolean =>
                        property.key.type === "Identifier"
                    )
                    .map((property: Property): string => {
                      const identifier = property.key as Identifier;
                      return identifier.name;
                    })
                    .includes(expectedName)
              )
            ) {
              context.report({
                node: byPage,
                message: `byPage does not contain an option for ${expectedName}`
              });
            }
          }
        );
      }
    } as Rule.RuleListener)
};
