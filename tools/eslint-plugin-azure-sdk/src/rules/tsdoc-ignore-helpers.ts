/**
 * @fileoverview Rule to require TSDoc comments to include '@ignore' if the object is not public-facing.
 * @author Arpan Laha
 */

//import { stripPath } from "../utils/verifiers";
import { Rule } from "eslint";
//import { Declaration, ExportNamedDeclaration } from "estree";

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
      const module = await import(context.settings.main);
      context.settings.public = Object.keys(module);
    }
    return {
      // callback functions
      // ExportNamedDeclaration: (node: ExportNamedDeclaration): void => {
      //   const declaration = node.declaration as Declaration;
      // }
    } as Rule.RuleListener;
  }
};
