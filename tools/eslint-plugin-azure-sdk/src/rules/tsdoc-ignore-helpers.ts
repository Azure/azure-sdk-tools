/**
 * @fileoverview Rule to require TSDoc comments to include '@ignore' if the object is not public-facing.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { ParserServices } from "@typescript-eslint/experimental-utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "require TSDoc comments to include '@ignore' if the object is not public-facing",
      category: "Best Practices",
      recommended: true,
      url: "to be added" //TODO
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    if (!context.settings.public) {
      const parserServices: ParserServices = context.parserServices;
      if (parserServices.program === undefined) {
        return {};
      }
      const program = parserServices.program;
      const typeChecker = program.getTypeChecker();
      const sourceFile = program.getSourceFile(context.settings.main);
      if (sourceFile === undefined) {
        return {};
      }
      const symbol = typeChecker.getSymbolAtLocation(sourceFile);
      if (symbol === undefined) {
        return {};
      }
      const exports = typeChecker.getExportsOfModule(symbol);
      console.log(exports);
      context.settings.public = exports;
    }
    return {
      // callback functions
    } as Rule.RuleListener;
  }
};
