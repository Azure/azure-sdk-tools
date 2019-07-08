/**
 * @fileoverview Definition of processors
 * @author Arpan Laha
 */

import { Linter } from "eslint";

export = {
  ".json": {
    preprocess: (text: string): string[] => {
      return [text];
    },
    postprocess: (messages: Linter.LintMessage[][]): Linter.LintMessage[] => {
      return messages[0].filter((message: Linter.LintMessage): boolean => {
        return message.ruleId !== "no-unused-expressions";
      });
    }
  }
};
