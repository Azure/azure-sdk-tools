/**
 * @fileoverview Rule to require TSDoc comments to include '@ignore' if the object is not public-facing.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { createCompilerHost, ScriptTarget } from "typescript";
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
  create: async (context: Rule.RuleContext): Promise<Rule.RuleListener> => {
    if (!context.settings.public) {
      const parserServices: ParserServices = context.parserServices;
      if (parserServices.program === undefined) {
        console.log("program undefined");
        return {};
      }
      const typeChecker = parserServices.program.getTypeChecker();
      const compilerHost = createCompilerHost({});
      const sourceFile = compilerHost.getSourceFile(
        context.settings.main,
        ScriptTarget.Latest
      );
      if (sourceFile === undefined) {
        console.log("sourceFile undefined");
        return {};
      }
      console.log(sourceFile);
      const symbol = typeChecker.getSymbolAtLocation(sourceFile);
      if (symbol === undefined) {
        console.log("symbol undefined");
        return {};
      }
      const exports = typeChecker.getExportsOfModule(symbol);

      console.log(exports);
    }
    return {
      // callback functions
    } as Rule.RuleListener;
  }
};
