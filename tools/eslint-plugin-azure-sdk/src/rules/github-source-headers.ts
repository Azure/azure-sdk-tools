/**
 * @fileoverview Rule to require copyright headers in every source file.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Comment, Node } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "require copyright headers in every source file",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#github-source-headers"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    // regex checking file ending
    const sourceFileRegex = /\.ts$/; // ...src/...ts

    return sourceFileRegex.test(context.getFilename())
      ? {
          Program: (node: Node): void => {
            const sourceCode = context.getSourceCode();

            // gets comments at top of file
            const headerComments = sourceCode.getCommentsBefore(node);

            // check that there are any header comments at all
            if (!headerComments.length) {
              context.report({
                node: node,
                message: "no copyright header found"
              });
              return;
            }

            // expoected copyright header
            const copyright =
              "Copyright (c) Microsoft Corporation. All rights reserved.\nLicensed under the MIT License.\n";

            // copyright header line regexes
            const line1Regex = /Copyright \(c\) Microsoft Corporation\. All rights reserved\./;
            const line2Regex = /Licensed under the MIT License\./;

            const adheres =
              headerComments.find((comment: Comment): boolean => {
                return line1Regex.test(comment.value);
              }) &&
              headerComments.find((comment: Comment): boolean => {
                return line2Regex.test(comment.value);
              });

            // look for both lines
            !adheres &&
              context.report({
                node: node,
                message:
                  "copyright header not properly configured - expected value:\n" +
                  copyright
              });
          }
        }
      : {};
  }
};
