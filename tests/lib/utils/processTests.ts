/**
 * @fileoverview Helper to feed in processor information to RuleTester.
 * @author Arpan Laha
 */

import { processors } from "../../../src/index";

interface Info {
  preprocess: (text: string) => string[];
  postprocess: (messages: string[][]) => string[];
  filename: string;
}

export default function(fileName: string): Info {
  return { ...processors[".json"], filename: fileName };
}
