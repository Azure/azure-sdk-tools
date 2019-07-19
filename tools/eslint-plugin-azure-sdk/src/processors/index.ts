/**
 * @fileoverview Definition of processors
 * @author Arpan Laha
 */

import { Linter } from "eslint";

/**
 * An object containing processors used by the plugin
 */
export = {
  /**
   * The processor for JSON files
   * Ignores the no-unused-expressions ESLint rule
   */
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
