/**
 * @fileoverview Rule to require TSDoc comments to include internal or ignore tags if the object is internal.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { getLocalExports } from "../utils";
import { Node } from "estree";
import { Node as TSNode, TypeChecker } from "typescript";
import { ParserWeakMap } from "@typescript-eslint/typescript-estree/dist/parser-options";
import {
  ParserServices,
  TSESTree
} from "@typescript-eslint/experimental-utils";
import { getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

/**
 * Helper method for reporting on a node
 * @param node the Node being operated on
 * @param context the ESLint runtime context
 * @param converter a converter from TSESTree Nodes to TSNodes
 * @param typeChecker the TypeScript TypeChecker
 * @throws if the Node passes throught the initial checks and does not have an internal or ignore tag
 */
const reportInternal = (
  node: Node,
  context: Rule.RuleContext,
  converter: ParserWeakMap<TSESTree.Node, TSNode>,
  typeChecker: TypeChecker
): void => {
  const tsNode = converter.get(node as TSESTree.Node) as any;
  const symbol = typeChecker.getTypeAtLocation(tsNode).getSymbol();

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

    TSDocTags.every((TSDocTag: string): boolean => {
      return !/(ignore)|(internal)/.test(TSDocTag);
    }) &&
      context.report({
        node: node,
        message:
          "internal items with TSDoc comments should include an @internal or @ignore tag"
      });
  }
};

export = {
  meta: getRuleMetaData(
    "ts-doc-internal",
    "require TSDoc comments to include an '@internal' or '@ignore' tag if the object is not public-facing"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const fileName = context.getFilename();
    if (/\.ts$/.test(fileName) && !context.settings.exported) {
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

    const typeChecker = parserServices.program.getTypeChecker();
    const converter = parserServices.esTreeNodeToTSNodeMap;

    return /src/.test(fileName)
      ? {
          // callback functions
          ":matches(TSInterfaceDeclaration, ClassDeclaration, TSModuleDeclaration)": (
            node: Node
          ): void => {
            reportInternal(node, context, converter, typeChecker);
          },

          ":function": (node: Node): void => {
            context.getAncestors().every((ancestor: Node): boolean => {
              return ![
                "ClassBody",
                "TSInterfaceBody",
                "TSModuleBlock"
              ].includes(ancestor.type);
            }) && reportInternal(node, context, converter, typeChecker);
          }
        }
      : {};
  }
};
