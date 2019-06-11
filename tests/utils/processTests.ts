/**
 * @fileoverview Helper to feed in processor information to RuleTester.
 * @author Arpan Laha
 */

import plugin from "../../src";
import { Linter } from "eslint";

interface Info {
  preprocess: (text: string) => string[];
  postprocess: (messages: Linter.LintMessage[][]) => Linter.LintMessage[];
  filename: string;
}

export const processJSON = function(fileName: string): Info {
  return { ...plugin.processors[".json"], filename: fileName };
};
