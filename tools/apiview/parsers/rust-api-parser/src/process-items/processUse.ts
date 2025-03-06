import { ReviewLine, TokenKind } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { isModuleItem, isUseItem } from "./utils/typeGuards";
import { processItem } from "./processItem";
import { externalReexports } from "./utils/externalReexports";
import { getAPIJson } from "../main";

export const reexportLines: {
  internal: ReviewLine[];
  external: { items: ReviewLine[]; modules: ReviewLine[] };
} = {
  internal: [],
  external: { items: [], modules: [] },
};

export function processUse(item: Item): ReviewLine[] | undefined {
  if (!isUseItem(item)) return;
  const apiJson = getAPIJson();
  const reviewLines: ReviewLine[] = [];
  if (item.docs) reviewLines.push(createDocsReviewLine(item));

  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub use",
  });

  let useValue = item.inner.use.source || "null";
  if (item.inner.use.is_glob) {
    useValue += "::*";
  }

  reviewLine.Tokens.push({
    Kind: TokenKind.TypeName,
    Value: useValue,
    RenderClasses: ["mname", "use"],
    NavigateToId: item.inner.use.id.toString(),
  });

  reviewLines.push(reviewLine);

  if (item.inner.use.id in apiJson.index) {
    // non external crates

    // 1. Option 1 is to make them all children of "use" line
    // reviewLine.Children = processItem(apiJson.index[item.inner.use.id], apiJson);

    // 2. Option 2 is to dump them all globally to the parent module
    // reexportLines.internal.push(...processItem(apiJson.index[item.inner.use.id], apiJson));

    // 3. Option 3 is a revised version of option 2
    // Dumps them all, but manages to keep the structure by encapsulating them in modules
    // for the re-exports in the crate
    if (isModuleItem(apiJson.index[item.inner.use.id])) {
      reexportLines.internal.push(...processItem(apiJson.index[item.inner.use.id]));
    } else {
      // Extract the base path by removing the item name from the source
      const useSource = item.inner.use.source || "";
      const useName = apiJson.index[item.inner.use.id].name || "";
      // Get the base path by removing the name from the end of the source
      const basePath = useSource.endsWith(useName)
        ? useSource.substring(0, useSource.length - useName.length - 2) // -2 for ::
        : useSource;

      const reviewLine: ReviewLine = {
        LineId: apiJson.index[item.inner.use.id].id.toString() + "_reexport_parent",
        Tokens: [
          {
            Kind: TokenKind.Keyword,
            Value: "pub mod",
          },
          {
            Kind: TokenKind.TypeName,
            Value: basePath,
            RenderClasses: ["mname"],
            NavigateToId: apiJson.index[item.inner.use.id].id.toString() + "_reexport_parent",
            NavigationDisplayName: basePath,
          },
          {
            Kind: TokenKind.Punctuation,
            Value: "{",
            HasSuffixSpace: false,
          },
        ],
        Children: processItem(apiJson.index[item.inner.use.id]),
      };
      reexportLines.internal.push(reviewLine);
      reexportLines.internal.push({
        RelatedToLine: apiJson.index[item.inner.use.id].id.toString() + "_reexport_parent",
        Tokens: [
          {
            Kind: TokenKind.Punctuation,
            Value: "}",
          },
        ],
      });
    }
  } else if (item.inner.use.id in apiJson.paths) {
    // for the re-exports in the external crates
    const lines = externalReexports(item.inner.use.id);
    if (
      !reexportLines.external.items.some((line) => line.LineId === item.inner.use.id.toString())
    ) {
      reexportLines.external.items.push(...lines.items);
    }
    reexportLines.external.modules.push(...lines.modules);
  }

  return reviewLines;
}
