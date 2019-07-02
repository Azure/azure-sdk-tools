/**
 * @fileoverview Rule to encourage usage of interfaces over classes as function parameters.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import {
  ArrowFunctionExpression,
  AssignmentPattern,
  BlockStatement,
  ClassBody,
  FunctionDeclaration,
  FunctionExpression,
  Identifier,
  Node,
  MethodDefinition,
  Pattern,
  Program,
  VariableDeclarator
} from "estree";
import { SymbolFlags, TypeChecker } from "typescript";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

type FunctionType =
  | FunctionExpression
  | FunctionDeclaration
  | ArrowFunctionExpression;
type BodyNodeType = BlockStatement | ClassBody | Program;

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
    const verified: string[] = [];
    let name = "";
    return {
      ":function": (node: FunctionType): void => {
        const ancestors = context.getAncestors().reverse();
        switch (node.type) {
          case "FunctionExpression": {
            const parent:
              | MethodDefinition
              | VariableDeclarator = ancestors[0] as
              | MethodDefinition
              | VariableDeclarator;
            if (parent.type === "MethodDefinition") {
              const key: Identifier = parent.key as Identifier;
              name = key !== undefined ? key.name : "";
            } else {
              // VariableDeclarator
              const id: Identifier = parent.id as Identifier;
              name = id !== undefined ? id.name : "";
            }
            break;
          }
          case "FunctionDeclaration": {
            const id: Identifier = node.id as Identifier;
            name = id !== undefined ? id.name : "";
            break;
          }
          case "ArrowFunctionExpression": {
            const parent: VariableDeclarator = ancestors[0] as VariableDeclarator;
            const id: Identifier = parent.id as Identifier;
            name = id !== undefined ? id.name : "";
            break;
          }
        }

        if (name !== "" && name !== undefined && verified.includes(name)) {
          return;
        }

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

          if (symbol === undefined || symbol.getFlags() !== SymbolFlags.Class) {
            return;
          }

          const bodyNode: BodyNodeType = ancestors.find(
            (ancestor: Node): boolean => {
              return ["BlockStatement", "ClassBody", "Program"].includes(
                ancestor.type
              );
            }
          ) as BodyNodeType;
          const body = bodyNode.body as Node[];

          let overloads: FunctionType[] = [];
          if (node.type === "FunctionExpression") {
            const parent: Node = ancestors[0];
            if (parent.type === "MethodDefinition") {
              overloads = body
                .filter((element: Node): boolean => {
                  if (element.type !== "MethodDefinition") {
                    return false;
                  }
                  const methodDefinition = element as MethodDefinition;
                  const key: Identifier = methodDefinition.key as Identifier;
                  const functionExpression = methodDefinition.value;
                  return (
                    key.name === name &&
                    functionExpression.params !== node.params
                  );
                })
                .map(
                  (element: Node): FunctionExpression => {
                    const methodDefinition = element as MethodDefinition;
                    return methodDefinition.value;
                  }
                );
            }
          } else if (node.type === "FunctionDeclaration") {
            overloads = body.filter((element: Node): boolean => {
              if (element.type !== "FunctionDeclaration") {
                return false;
              }
              const functionDeclaration = element as FunctionDeclaration;
              const key: Identifier = functionDeclaration.id as Identifier;
              return (
                key.name === name && functionDeclaration.params !== node.params
              );
            }) as FunctionDeclaration[];
          }

          const interfacesOnly = overloads.find(
            (overload: FunctionType): boolean => {
              return (
                overload.params.find((overloadParam: Pattern): boolean => {
                  let overloadIdentifier = overloadParam;
                  if (overloadParam.type === "AssignmentPattern") {
                    const assignmentPattern: AssignmentPattern = overloadParam as AssignmentPattern;
                    overloadIdentifier = assignmentPattern.left;
                  }
                  overloadIdentifier = overloadIdentifier as Identifier;
                  const overloadTSNode = parserServices.esTreeNodeToTSNodeMap.get(
                    overloadIdentifier
                  );
                  const overloadType = typeChecker.getTypeAtLocation(
                    overloadTSNode
                  );

                  const overloadSymbol = overloadType.getSymbol();

                  return (
                    overloadSymbol !== undefined &&
                    overloadSymbol.getFlags() === SymbolFlags.Class
                  );
                }) === undefined
              );
            }
          );

          if (overloads.length !== 0 && interfacesOnly) {
            verified.push(name);
            return;
          }

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
