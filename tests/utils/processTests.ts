/**
 * @fileoverview Helper to feed in processor information to RuleTester.
 * @author Arpan Laha
 */

import { processors } from "../../src/index";
import { Linter } from "eslint";

interface Info {
  preprocess: (text: string) => string[];
  postprocess: (messages: Linter.LintMessage[][]) => Linter.LintMessage[];
  filename: string;
}

export const processJSON = function(fileName: string): Info {
  return { ...processors[".json"], filename: fileName };
};
