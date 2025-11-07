import { ReviewLine, ReviewToken } from "../models/apiview-models";

/**
 * Maps content-based LineIds to their corresponding original item IDs
 * Key: content-based LineId (e.g., "pub.struct.Client.field.endpoint")
 * Value: original item.id (e.g., "123")
 */
export const lineIdMap = new Map<string, string>();

/**
 * Tracks which LineIds are from external items
 * Key: content-based LineId
 * Value: true if external, false if internal
 */
export const externalItemsMap = new Map<string, boolean>();

/**
 * Tracks all LineIds and their first occurrence for navigation
 * Key: original item.id
 * Value: content-based LineId of first occurrence
 */
export const navigationMap = new Map<string, string>();

/**
 * Extracts content from tokens by concatenating their values
 * @param tokens Array of review tokens
 * @returns Concatenated content string
 */
function extractTokenContent(tokens: ReviewToken[]): string {
  const values = tokens
    .map(token => token.Value)
    .filter(value => value.trim() !== ""); // Filter out empty values

  // Join tokens with underscores if they don't already contain spaces or separators
  let result = "";
  for (let i = 0; i < values.length; i++) {
    const value = values[i];
    if (i > 0) {
      const prevValue = values[i - 1];
      const currentValue = value;

      // Add underscore if neither the previous value ends with separator nor current starts with separator
      if (!prevValue.match(/[\s_\-:.]$/) && !currentValue.match(/^[\s_\-:.]/)) {
        result += "_";
      }
    }
    result += value;
  }

  // Don't strip important symbols here - let sanitizeForLineId handle them properly
  return sanitizeForLineId(result);
}

/**
 * Sanitizes input string for use as LineId by replacing special characters
 * @param input Input string to sanitize
 * @returns Sanitized string suitable for LineId
 */
export function sanitizeForLineId(input: string): string {
  return input
    // Handle dereferencing and pointer operations first (preserve workflow context)
    .replace(/\*mut\s+/g, "deref_mut_")
    .replace(/\*const\s+/g, "deref_const_")
    .replace(/\*\s*/g, "deref_")
    // Replace reference symbols with meaningful text (preserve reference context)
    .replace(/&mut\s+/g, "refmut_")
    .replace(/&\s*/g, "ref_")
    // Handle module path separators with context preservation
    .replace(/::/g, "_path_")
    // Handle trait bounds and where clauses
    .replace(/\bwhere\b/g, "_where_")
    .replace(/\+\s*/g, "_and_")
    // Replace generic brackets with meaningful separators
    .replace(/</g, "_of_")
    .replace(/>/g, "_end_")
    // Handle function pointers and closures with context
    .replace(/\bfn\s*\(/g, "fnptr_")
    .replace(/\|([^|]*)\|/g, "_closure_$1_")
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
    .replace(/'/g, "_lifetime_") // lifetime parameters
    .replace(/\$/g, "_dollar_")  // macro variables
    .replace(/-/g, "_dash_")     // preserve dashes for crate names
    // Clean up multiple consecutive underscores
    .replace(/_+/g, "_")
    // Remove leading/trailing underscores
    .replace(/^_+|_+$/g, "");
}

/**
 * Creates a content-based LineId using hierarchical path
 * @param tokens The tokens for this line
 * @param lineIdPrefix The prefix from ancestors
 * @param originalId The original item.id
 * @returns The content-based LineId
 */
export function createContentBasedLineId(
  tokens: ReviewToken[],
  lineIdPrefix: string,
  originalId: string
): string {
  const content = extractTokenContent(tokens);
  const fullPath = lineIdPrefix ? `${lineIdPrefix}.${content}` : content;

  // Store mapping
  lineIdMap.set(fullPath, originalId);

  // Track first occurrence for navigation
  if (!navigationMap.has(originalId)) {
    navigationMap.set(originalId, fullPath);
  }

  return fullPath;
}

/**
 * Marks a LineId as external
 * @param lineId The content-based LineId
 */
export function markAsExternal(lineId: string): void {
  externalItemsMap.set(lineId, true);
}

/**
 * Post-processes all review lines to set proper NavigateToId values
 * @param reviewLines The array of review lines to process
 */
export function setNavigationIds(reviewLines: ReviewLine[]): void {
  // Group LineIds by their mapped original item.id value
  const valueGroups = new Map<string, string[]>();

  for (const [lineId, originalId] of lineIdMap.entries()) {
    if (!valueGroups.has(originalId)) {
      valueGroups.set(originalId, []);
    }
    valueGroups.get(originalId)!.push(lineId);
  }

  // Determine navigation targets
  const navigationTargets = new Map<string, string>();

  for (const [originalId, lineIds] of valueGroups.entries()) {
    let targetLineId: string;

    if (lineIds.length === 1) {
      // Case 3: Only one occurrence, link to self
      targetLineId = lineIds[0];
    } else {
      // Multiple occurrences - check for external references
      const externalLineIds = lineIds.filter(lineId => externalItemsMap.get(lineId) === true);

      if (externalLineIds.length > 0) {
        // Case 1: External reference found, use first external as leader
        targetLineId = externalLineIds[0];
      } else {
        // Case 2: No external, use first occurrence
        targetLineId = navigationMap.get(originalId) || lineIds[0];
      }
    }

    // Set navigation target for all LineIds with this value
    for (const lineId of lineIds) {
      navigationTargets.set(lineId, targetLineId);
    }
  }

  // Apply navigation targets to review lines
  function updateNavigationIds(lines: ReviewLine[]) {
    for (const line of lines) {
      if (line.LineId && navigationTargets.has(line.LineId)) {
        const targetLineId = navigationTargets.get(line.LineId)!;

        // Update NavigateToId in tokens
        for (const token of line.Tokens || []) {
          if (token.NavigateToId) {
            token.NavigateToId = targetLineId;
          }
        }
      }

      if (line.Children) {
        updateNavigationIds(line.Children);
      }
    }
  }

  updateNavigationIds(reviewLines);
}

/**
 * Validates that all line IDs in the review lines are unique
 * @param reviewLines The array of review lines to validate
 * @returns Object with validation result and duplicate count
 */
export function ensureUniqueLineIds(reviewLines: ReviewLine[]): {
  isValid: boolean;
  duplicateCount: number;
  duplicates: string[];
} {
  const lineIdCounts = new Map<string, number>();
  const duplicates: string[] = [];

  function countLineIds(lines: ReviewLine[]) {
    for (const line of lines) {
      if (line.LineId) {
        const count = lineIdCounts.get(line.LineId) || 0;
        lineIdCounts.set(line.LineId, count + 1);

        if (count === 1) { // First duplicate occurrence
          duplicates.push(line.LineId);
        }
      }

      if (line.Children) {
        countLineIds(line.Children);
      }
    }
  }

  countLineIds(reviewLines);

  const duplicateCount = duplicates.length;
  const isValid = duplicateCount === 0;

  if (!isValid) {
    throw new Error(`Line ID validation failed: ${duplicateCount} duplicate${duplicateCount > 1 ? 's' : ''} found: ${duplicates.join(', ')}`);
  }

  return {
    isValid,
    duplicateCount,
    duplicates
  };
}
