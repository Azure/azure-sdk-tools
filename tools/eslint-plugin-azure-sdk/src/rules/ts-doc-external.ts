/**
 * @fileoverview Rule to require TSDoc comments on external objects and forbid usage of internal and ignore tags.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { getLocalExports } from "../utils";
import { TSESTree, TSNode } from "@typescript-eslint/typescript-estree";
import { ParserWeakMap } from "@typescript-eslint/typescript-estree/dist/parser-options";
import { ParserServices } from "@typescript-eslint/experimental-utils";
import { Node } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

const reportExternal = (
  node: any,
  context: Rule.RuleContext,
  converter: ParserWeakMap<TSESTree.Node, TSNode>
): void => {
  if (node.accessibility === "private") {
    return;
  }

  const tsNode = converter.get(node as TSESTree.Node) as any;
  if (tsNode.jsDoc === undefined) {
    context.report({
      node: node,
      message: "all external items must include TSDoc comments"
    });
    return;
  }

  if (node.kind === "constructor") {
    return;
  }

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
  TSDocTags.some((TSDocTag: string): boolean => {
    return internalRegex.test(TSDocTag);
  }) &&
    context.report({
      node: node,
      message:
        "external items' TSDoc comments should not include an @internal or @ignore tag"
    });
};

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "require TSDoc comments on external objects and forbid usage of @internal and @ignore tags",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-doc-external.md"
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
        node: any
      ): void => {
        const tsNode = converter.get(node) as any;
        const type = typeChecker.getTypeAtLocation(tsNode);
        const symbol = type.getSymbol();

        if (context.settings.exported.includes(symbol)) {
          reportExternal(node, context, converter);
          const body: Node[] = node.body.body;
          body.forEach((member: Node): void => {
            reportExternal(member, context, converter);
          });
        }
      }
    } as Rule.RuleListener;
  }
};
