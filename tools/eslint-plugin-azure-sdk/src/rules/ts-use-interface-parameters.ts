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
          const parserServices = context.parserServices;
          const typeChecker: TypeChecker = parserServices.program.getTypeChecker();
          const TSNode = parserServices.esTreeNodeToTSNodeMap.get(identifier);
          const type = typeChecker.getTypeAtLocation(TSNode);
          type.symbol.flags === SymbolFlags.Class &&
            context.report({
              node: identifier,
              message: "parameters should be interfaces, not classes"
            });
        });
      }
    } as Rule.RuleListener;
  }
};
