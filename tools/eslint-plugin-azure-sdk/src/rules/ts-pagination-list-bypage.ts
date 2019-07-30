/**
 * @fileoverview Rule to require byPage methods returned in client list methods to contain continuationToken and maxPageSize options.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import {
  FunctionExpression,
  ArrowFunctionExpression,
  Identifier,
  ObjectExpression,
  Pattern,
  Property,
  ReturnStatement
} from "estree";
import { getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-pagination-list",
    "require byPage methods returned in client list methods to contain continuationToken and maxPageSize option"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener =>
    ({
      // callback functions

      // call on return statements returning objects from client list methods
      "ClassDeclaration[id.name=/Client$/] > MethodDefinition[key.name=/^list($|([A-Z][a-zA-Z]*s$))/] ReturnStatement[argument.type='ObjectExpression']": (
        node: ReturnStatement
      ): void => {
        const returned = node.argument as ObjectExpression;

        // look for byPage function in returned object
        const byPageProperty = returned.properties.find(
          (property: Property): boolean =>
            property.key.type === "Identifier" &&
            property.key.name === "byPage" &&
            ["FunctionExpression", "ArrowFunctionExpression"].includes(
              property.value.type
            )
        );

        // report if none found
        if (byPageProperty === undefined) {
          context.report({
            node: node,
            message: "returned object does not contain a byPage function"
          });
          return;
        }

        // look for continuationToken and maxPageSize options
        const byPage = byPageProperty.value as
          | ArrowFunctionExpression
          | FunctionExpression;
        ["continuationToken", "maxPageSize"].forEach(
          (expectedName: string): void => {
            const identifierParams = byPage.params.filter(
              (param: Pattern): boolean => param.type === "Identifier"
            ) as Identifier[];
            if (
              !identifierParams.every(
                (param: Identifier): boolean => param.name !== expectedName
              )
            ) {
              context.report({
                node: node,
                message: `byPage does not contain an option for ${expectedName}`
              });
            }
          }
        );
      }
    } as Rule.RuleListener)
};
