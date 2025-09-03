import { PACKAGE_NAME } from "../main";
import { ReviewLine } from "../models/apiview-models";

export const lineIdMap = new Map<string, string>();

/**
 * Sanitizes a string for use in line IDs while preserving meaningful distinctions
 * @param input The input string to sanitize
 * @returns A sanitized string suitable for line IDs
 */
export function sanitizeForLineId(input: string): string {
  return input
    // Replace reference symbols with meaningful text
    .replace(/&mut\s+/g, "refmut_")
    .replace(/&\s*/g, "ref_")
    // Replace generic brackets with meaningful separators
    .replace(/</g, "_of_")
    .replace(/>/g, "_")
    // Replace common punctuation with meaningful separators
    .replace(/\s+/g, "_") // spaces to underscores
    .replace(/:/g, "_")   // colons to underscores
    .replace(/,/g, "_")   // commas to underscores
    .replace(/\(/g, "_")  // parentheses
    .replace(/\)/g, "_")
    .replace(/\[/g, "_")  // brackets
    .replace(/\]/g, "_")
    .replace(/\{/g, "_")  // braces
    .replace(/\}/g, "_")
    // Clean up multiple consecutive underscores
    .replace(/_+/g, "_")
    // Remove leading/trailing underscores
    .replace(/^_+|_+$/g, "");
}

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

/**
 * Validates that all line IDs are unique and throws an error if duplicates are found.
 * This is a defensive measure to catch any edge cases not handled by sanitization.
 * @param reviewLines The review lines to validate
 * @throws Error if duplicate line IDs are found
 */
export function ensureUniqueLineIds(reviewLines: ReviewLine[]): void {
  const usedLineIds = new Set<string>();
  const duplicates: string[] = [];

  function validateLineIds(line: ReviewLine): void {
    if (line.LineId) {
      if (usedLineIds.has(line.LineId)) {
        duplicates.push(line.LineId);
      } else {
        usedLineIds.add(line.LineId);
      }
    }

    // Process children recursively
    if (line.Children && Array.isArray(line.Children)) {
      line.Children.forEach(validateLineIds);
    }
  }

  // Process all review lines
  if (reviewLines && Array.isArray(reviewLines)) {
    reviewLines.forEach(validateLineIds);
  }

  // If duplicates were found, throw an error
  if (duplicates.length > 0) {
    const uniqueDuplicates = [...new Set(duplicates)];
    throw new Error(
      `Duplicate line IDs detected: ${uniqueDuplicates.join(', ')}. ` +
      `This will cause Copilot review failures. Please fix the line ID generation logic.`
    );
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
  
  // Final validation: ensure all line IDs are unique before serialization
  ensureUniqueLineIds(reviewLines);
}
