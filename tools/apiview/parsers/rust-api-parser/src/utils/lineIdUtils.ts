import { PACKAGE_NAME } from "../main";
import { ReviewLine } from "../models/apiview-models";

export const lineIdMap = new Map<string, string>();

function postProcessLineIdMap(reviewLines: ReviewLine[], updatedLineIdMap: Map<string, string>) {
  function recurse(lines: ReviewLine[], prefix: string) {
    for (const line of lines) {
      if (line.LineId) {
        let existingId = lineIdMap.has(line.LineId) ? lineIdMap.get(line.LineId) : line.LineId;
        const newId = `${prefix}_${existingId}`;
        updatedLineIdMap.set(line.LineId, newId);
        if (line.Children && line.Children.length > 0) {
          recurse(line.Children, newId);
        }
      }
    }
  }
  if (reviewLines && Array.isArray(reviewLines)) {
    recurse(reviewLines, `root_mod_${PACKAGE_NAME}`);
  }
}

export function updateReviewLinesWithStableLineIds(reviewLines: ReviewLine[]) {
  const updatedLineIdMap = new Map<string, string>();
  postProcessLineIdMap(reviewLines, updatedLineIdMap);
  function updateLineIdReferences(reviewLines: ReviewLine[]) {
    if (reviewLines && Array.isArray(reviewLines)) {
      for (const line of reviewLines) {
        if (line.LineId) {
          line.LineId = updatedLineIdMap.get(line.LineId) || line.LineId;
        }
        if (line.RelatedToLine) {
          line.RelatedToLine = updatedLineIdMap.get(line.RelatedToLine) || line.RelatedToLine;
        }
        if (line.Tokens.length > 0) {
          for (const token of line.Tokens) {
            if (token.NavigateToId) {
              token.NavigateToId = updatedLineIdMap.get(token.NavigateToId) || token.NavigateToId;
            }
          }
        }
        if (line.Children && Array.isArray(line.Children) && line.Children.length > 0) {
          updateLineIdReferences(line.Children);
        }
      }
    }
  }
  updateLineIdReferences(reviewLines);
}
