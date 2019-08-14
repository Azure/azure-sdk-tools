/**
 * @fileoverview Rule to require copyright headers in every source file.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Comment, Node } from "estree";
import { get as getLevenshteinDistance } from "fast-levenshtein";
import { getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

const expectedComments =
  "// Copyright (c) Microsoft Corporation.\n// Licensed under the MIT license.\n";

const expectedLine1 = "Copyright (c) Microsoft Corporation.";
const expectedLine2 = "Licensed under the MIT license.";

const noCaseRegex1 = /Copyright \(c\) Microsoft Corporation\./i;
const noCaseRegex2 = /Licensed under the MIT license\./i;

/**
 * Arbitrary Levenshtein distance cutoff.
 */
const levenshteinCutoff = 10;

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
                  !comment.value.includes(expectedLine1)
              ) ||
              headerComments.every(
                (comment: Comment): boolean =>
                  !comment.value.includes(expectedLine2)
              )
            ) {
              context.report({
                node: node,
                message:
                  "copyright header not properly configured - expected value:\n" +
                  "Copyright (c) Microsoft Corporation.\nLicensed under the MIT license.\n",
                fix: (fixer: Rule.RuleFixer): Rule.Fix => {
                  // iterate over comments and replace with proper value if close enough
                  for (const comment of headerComments) {
                    const value = comment.value;
                    if (
                      !value.includes(expectedLine1) &&
                      (noCaseRegex1.test(value) ||
                        getLevenshteinDistance(expectedLine1, value) <
                          levenshteinCutoff)
                    ) {
                      return fixer.replaceText(
                        comment as any,
                        `// ${expectedLine1}`
                      );
                    }
                    if (
                      !value.includes(expectedLine2) &&
                      (noCaseRegex2.test(value) ||
                        getLevenshteinDistance(expectedLine2, value) <
                          levenshteinCutoff)
                    ) {
                      return fixer.replaceText(
                        comment as any,
                        `// ${expectedLine2}`
                      );
                    }
                  }
                  return fixer.insertTextBefore(node, expectedComments);
                }
              });
            }
          }
        }
      : {}
};
