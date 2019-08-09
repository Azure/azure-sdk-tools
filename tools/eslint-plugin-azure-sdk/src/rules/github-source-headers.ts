/**
 * @fileoverview Rule to require copyright headers in every source file.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Comment, Node } from "estree";
import { getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

const expectedComments =
  "// Copyright (c) Microsoft Corporation.\n// Licensed under the MIT license.\n";

export = {
  meta: getRuleMetaData(
    "github-source-headers",
    "require copyright headers in every source file",
    "code"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener =>
    /\.ts$/.test(context.getFilename())
      ? {
          // callback functions

          // check top-level node
          Program: (node: Node): void => {
            const headerComments = context
              .getSourceCode()
              .getCommentsBefore(node);

            // check that there are any header comments at all
            if (headerComments.length === 0) {
              context.report({
                node: node,
                message: "no copyright header found",
                fix: (fixer: Rule.RuleFixer): Rule.Fix =>
                  fixer.insertTextBefore(node, expectedComments)
              });
              return;
            }

            // check for existence of both lines
            if (
              headerComments.every(
                (comment: Comment): boolean =>
                  !/Copyright \(c\) Microsoft Corporation\./.test(
                    comment.value
                  ) ||
                  headerComments.every(
                    (comment: Comment): boolean =>
                      !/Licensed under the MIT license\./.test(comment.value)
                  )
              )
            ) {
              context.report({
                node: node,
                message:
                  "copyright header not properly configured - expected value:\n" +
                  "Copyright (c) Microsoft Corporation.\nLicensed under the MIT license.\n",
                fix: (fixer: Rule.RuleFixer): Rule.Fix =>
                  fixer.insertTextBefore(node, expectedComments)
              });
            }
          }
        }
      : {}
};
