/**
 * @fileoverview Rule to require TSDoc comments to include internal or ignore tags if the object is internal.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { getLocalExports } from "../utils";
import { Node } from "estree";
import { TypeChecker } from "typescript";
import { TSESTree, TSNode } from "@typescript-eslint/typescript-estree";
import { ParserWeakMap } from "@typescript-eslint/typescript-estree/dist/parser-options";
import { ParserServices } from "@typescript-eslint/experimental-utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

const reportInternal = (
  node: Node,
  context: Rule.RuleContext,
  converter: ParserWeakMap<TSESTree.Node, TSNode>,
  typeChecker: TypeChecker
): void => {
  const tsNode = converter.get(node as TSESTree.Node) as any;
  const type = typeChecker.getTypeAtLocation(tsNode);
  const symbol = type.getSymbol();

  if (
    !context.settings.exported.includes(symbol) &&
    tsNode.jsDoc !== undefined
  ) {
    let TSDocTags: string[] = [];
    tsNode.jsDoc.forEach((TSDocComment: any): void => {
      TSDocTags = TSDocTags.concat(
        TSDocComment.tags !== undefined
          ? TSDocComment.tags.map((TSDocTag: any): string => {
              return TSDocTag.tagName.escapedText;
            })
          : []
      );
    });

    const internalRegex = /(ignore)|(internal)/;
    TSDocTags.every((TSDocTag: string): boolean => {
      return !internalRegex.test(TSDocTag);
    }) &&
      context.report({
        node: node,
        message:
          "internal items with TSDoc comments should include an @internal or @ignore tag"
      });
  }
};

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "require TSDoc comments to include an '@internal' or '@ignore' tag if the object is not public-facing",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/arpanlaha/azure-sdk-tools/blob/ruleset-two/tools/eslint-plugin-azure-sdk/docs/rules/ts-doc-internal.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    if (!context.settings.exported) {
      const packageExports = getLocalExports(context);
      if (packageExports !== undefined) {
        context.settings.exported = packageExports;
      } else {
        context.settings.exported = [];
        return {};
      }
    }
    const parserServices: ParserServices = context.parserServices as ParserServices;
    if (
      parserServices.program === undefined ||
      parserServices.esTreeNodeToTSNodeMap === undefined
    ) {
      return {};
    }

    const program = parserServices.program;
    const typeChecker = program.getTypeChecker();
    const converter = parserServices.esTreeNodeToTSNodeMap;

    return {
      // callback functions
      ":matches(TSInterfaceDeclaration, ClassDeclaration)": (
        node: Node
      ): void => {
        reportInternal(node, context, converter, typeChecker);
      },

      ":function": (node: Node): void => {
        const ancestors = context.getAncestors();

        ancestors.every((ancestor: Node): boolean => {
          return ancestor.type !== "ClassBody";
        }) && reportInternal(node, context, converter, typeChecker);
      }
    };
  }
};
