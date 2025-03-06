import { Id } from "../../../rustdoc-types/output/rustdoc-types";
import { getAPIJson } from "../../main";
import { ReviewLine, TokenKind } from "../../models/apiview-models";

/**
 * Processes external re-exports and creates a structured view
 * @param itemId id of the item being re-exported
 * @param apiJson The API JSON object
 * @returns Array of ReviewLine objects representing the external re-export
 */
export function externalReexports(itemId: Id): { items: ReviewLine[], modules: ReviewLine[] } {
  const apiJson = getAPIJson();
  if (!itemId || !apiJson || !apiJson.paths || !apiJson.paths[itemId]) {
    return { items: [], modules: [] };
  }

  const itemSummary = apiJson.paths[itemId];
  // Check if the item has a path
  if (!itemSummary.path || !Array.isArray(itemSummary.path) || itemSummary.path.length === 0) {
    return { items: [], modules: [] };
  }

  // Handle differently based on item kind
  if (itemSummary.kind === 'module') {
    const lines = [{
      LineId: itemId.toString(),
      Tokens: [{
        Kind: TokenKind.Keyword,
        Value: "pub",
      }, {
        Kind: TokenKind.Keyword,
        Value: itemSummary.kind,
      }, {
        Kind: TokenKind.TypeName,
        Value: itemSummary.path.concat().join("::"),
        RenderClasses: ['reexport'],
        NavigateToId: itemId.toString(),
      }, {
        Kind: TokenKind.Punctuation,
        Value: "{}"
      }],
    }];

    // For modules, find all related items (children of the module)

    return {
      modules: lines,
      items: []
    };
  } else {
    // For non-module items, just represent them directly
    return {
      items: [{
        LineId: itemId.toString(),
        Tokens: [{
          Kind: TokenKind.Keyword,
          Value: "pub",
        }, {
          Kind: TokenKind.Keyword,
          Value: itemSummary.kind,
        }, {
          Kind: TokenKind.TypeName,
          Value: itemSummary.path.concat().join("::"),
          RenderClasses: ['reexport'],
          NavigateToId: itemId.toString(),
        }],
      }],
      modules: []
    };
  }
}
