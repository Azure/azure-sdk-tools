/**
 * @fileoverview Rule to encourage usage of interfaces over classes as function parameters.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import {
  ArrowFunctionExpression,
  AssignmentPattern,
  FunctionDeclaration,
  FunctionExpression,
  Identifier,
  Pattern
} from "estree";
import { SymbolFlags, TypeChecker } from "typescript";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "encourage usage of interfaces over classes as function parameters",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-use-interface-parameters"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    return {
      ":function": (
        node: FunctionExpression | FunctionDeclaration | ArrowFunctionExpression
      ): void => {
        node.params.forEach((param: Pattern): void => {
          let identifier = param;
          if (param.type === "AssignmentPattern") {
            const assignmentPattern: AssignmentPattern = param as AssignmentPattern;
            identifier = assignmentPattern.left;
          }
          identifier = identifier as Identifier;
          const parserServices = context.parserServices;
          const typeChecker: TypeChecker = parserServices.program.getTypeChecker();
          const TSNode = parserServices.esTreeNodeToTSNodeMap.get(identifier);
          const type = typeChecker.getTypeAtLocation(TSNode);
          const symbol = type.getSymbol();
          symbol &&
            symbol.getFlags() === SymbolFlags.Class &&
            context.report({
              node: identifier,
              message:
                "type {{ type }} of parameter {{ param }} is a class, not an interface",
              data: {
                type: typeChecker.typeToString(type),
                param: identifier.name
              }
            });
        });
      }
    } as Rule.RuleListener;
  }
};
