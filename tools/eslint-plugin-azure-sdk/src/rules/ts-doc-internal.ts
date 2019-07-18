/**
 * @fileoverview Rule to require TSDoc comments to include internal or ignore tags if the object is internal.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { getLocalExports } from "../utils";
import { Node } from "estree";
import {
  ArrayLiteralExpression,
  createCompilerHost,
  JsonObjectExpressionStatement,
  JsonSourceFile,
  Node as TSNode,
  NodeArray,
  ObjectLiteralExpression,
  PropertyAssignment,
  ScriptTarget,
  StringLiteral,
  TypeChecker
} from "typescript";
import { ParserWeakMap } from "@typescript-eslint/typescript-estree/dist/parser-options";
import {
  ParserServices,
  TSESTree
} from "@typescript-eslint/experimental-utils";
import { getRuleMetaData } from "../utils";
// @ts-ignore
import { relative } from "path";
import { sync } from "glob";

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

/**
 * Determine whether this rule should examine a given file
 * @param fileName the filename of the file in question
 * @param exclude the list of files excluded by TypeDoc (other than those in node_modues)
 * @returns false if not in src or is excluded by TypeDoc
 */
const shouldExamineFile = (fileName: string, exclude: string[]): boolean => {
  if (!/src/.test(fileName)) {
    return false;
  }
  const relativePath = relative("", fileName).replace(/\\/g, "/");
  return !exclude.includes(relativePath);
};

let exclude: string[] = [];
const JSONHost = createCompilerHost({});
const typeDoc = JSONHost.getSourceFile("typedoc.json", ScriptTarget.JSON) as
  | JsonSourceFile
  | undefined;

if (typeDoc !== undefined) {
  typeDoc.statements.forEach(
    (statement: JsonObjectExpressionStatement): void => {
      const expression = statement.expression as ObjectLiteralExpression;
      const properties = expression.properties as NodeArray<PropertyAssignment>;
      properties.forEach((property: PropertyAssignment): void => {
        const name = property.name as StringLiteral;
        if (name.text === "exclude") {
          const initializer = property.initializer as ArrayLiteralExpression;
          const elements = initializer.elements as NodeArray<StringLiteral>;
          elements.forEach((element: StringLiteral): void => {
            exclude = exclude.concat(
              sync(element.text).filter((excludeFile: string): boolean => {
                return !/node_modules/.test(excludeFile);
              })
            );
          });
        }
      });
    }
  );
}

export = {
  meta: getRuleMetaData(
    "ts-doc-internal",
    "require TSDoc comments to include an '@internal' or '@ignore' tag if the object is not public-facing"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const fileName = context.getFilename();

    if (context.settings.exported === undefined && /\.ts$/.test(fileName)) {
      const packageExports = getLocalExports(context);
      if (packageExports !== undefined) {
        context.settings.exported = packageExports;
      } else {
        context.settings.exported = [];
        return {};
      }
    }

    const parserServices = context.parserServices as ParserServices;
    if (
      parserServices.program === undefined ||
      parserServices.esTreeNodeToTSNodeMap === undefined
    ) {
      return {};
    }

    const typeChecker = parserServices.program.getTypeChecker();
    const converter = parserServices.esTreeNodeToTSNodeMap;

    return shouldExamineFile(fileName, exclude)
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
