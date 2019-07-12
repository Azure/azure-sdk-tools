/**
 * @fileoverview Rule to require TSDoc comments to include '@ignore' if the object is not public-facing.
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
    const TSDocTags: string[] = tsNode.jsDoc
      .map((TSDocComment: any): string[] => {
        return TSDocComment.tags !== undefined
          ? TSDocComment.tags.map((TSDocTag: any): string => {
              return TSDocTag.tagName.escapedText;
            })
          : [];
      })
      .flat();
    const internalRegex = /internal/;
    TSDocTags.every((TSDocTag: string): boolean => {
      return !internalRegex.test(TSDocTag);
    }) &&
      context.report({
        node: node,
        message:
          "non-public facing items with TSDoc comments should include an @internal tag"
      });
  }
};

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "require TSDoc comments to include '@ignore' if the object is not public-facing",
      category: "Best Practices",
      recommended: true,
      url: "to be added" //TODO
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    if (!context.settings.exported) {
      const packageExports = getLocalExports(context);
      if (packageExports !== undefined) {
        context.settings.exported = packageExports;
      } else {
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
    //const sourceCode = context.getSourceCode();

    return {
      // callback functions
      ":matches(TSInterfaceDeclaration, ClassDeclaration)": (
        node: Node
      ): void => {
        reportInternal(node, context, converter, typeChecker);

        // const comments = sourceCode.getCommentsBefore(node);
        // const TSDocRegex = /^\*/;
        // const TSDocComments = comments.filter((comment: Comment): boolean => {
        //   return comment.type === "Block" && TSDocRegex.test(comment.value);
        // });
        // const ignoreRegex = /(@ignore)|(@internal)/;
        // TSDocComments.every((TSDocComment: Comment): boolean => {
        //   return !ignoreRegex.test(TSDocComment.value);
        // }) &&
        //   context.report({
        //     node: node,
        //     message:
        //       "non-public facing items with TSDoc comments should include an @ignore tag"
        //   });
      },

      ":function": (node: Node): void => {
        const ancestors = context.getAncestors();

        ancestors.find((ancestor: Node): boolean => {
          return ancestor.type === "ClassBody";
        }) === undefined &&
          reportInternal(node, context, converter, typeChecker);
      }
    };
  }
};
