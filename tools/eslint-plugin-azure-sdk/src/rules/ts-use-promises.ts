/**
 * @fileoverview Rule to force usage of built-in promises over external ones.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { ParserServices } from "@typescript-eslint/experimental-utils";
import { isExternalModule } from "typescript";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "force usage of built-in promises over external ones",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-use-promises.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const parserServices: ParserServices = context.parserServices;
    if (
      parserServices.program === undefined ||
      parserServices.esTreeNodeToTSNodeMap === undefined
    ) {
      return {};
    }
    const typeChecker = parserServices.program.getTypeChecker();
    const converter = parserServices.esTreeNodeToTSNodeMap;
    return {
      ":function[returnType.typeAnnotation.typeName.name='Promise']": (
        node: any
      ): void => {
        const name = node.returnType.typeAnnotation;
        const tsNode = converter.get(name);
        const type = typeChecker.getTypeAtLocation(tsNode);
        const symbol = type.getSymbol();
        if (symbol === undefined) {
          return;
        }
        const declaration = symbol.valueDeclaration;
        if (declaration === undefined) {
          return;
        }
        const sourceFile = declaration.getSourceFile();
        isExternalModule(sourceFile) &&
          context.report({
            node: node,
            message:
              "promises should use the in-built Promise type, not libraries or polyfills"
          });
      }
    } as Rule.RuleListener;
  }
};
