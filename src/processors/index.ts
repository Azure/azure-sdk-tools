/**
 * @fileoverview Definition of processors
 * @author Arpan Laha
 */

import { Linter } from "eslint";

export = {
  ".json": {
    preprocess: function(text: string): string[] {
      const code = "const json = " + text;
      return [code];
    },
    postprocess: function(
      messages: Linter.LintMessage[][]
    ): Linter.LintMessage[] {
      return messages[0].filter(function(message) {
        return message.ruleId !== "no-unused-vars";
      });
    }
  }
};
