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
 * Ensures all line IDs are unique by automatically appending counters to duplicates.
 * This is a defensive measure to catch any edge cases not handled by sanitization.
 * @param reviewLines The review lines to validate and fix
 */
export function ensureUniqueLineIds(reviewLines: ReviewLine[]): void {
  const usedLineIds = new Set<string>();
  const lineIdCounters = new Map<string, number>();
  const lineIdMappings = new Map<string, string>(); // Track original -> updated mappings

  // First pass: collect all line IDs and fix duplicates
  // 
  // Why we need this duplicate fixing mechanism:
  // 1. EXTERNAL RE-EXPORTS: The same external/internal item (like std::HashMap) can be re-exported
  //    from multiple modules, creating identical line IDs in externalReexports.ts
  // 2. TRAIT IMPLEMENTATIONS: Multiple impl blocks can generate similar line IDs after
  //    sanitization, especially with generic types and reference patterns
  // 3. MODULE PATH COLLISIONS: Different module structures can result in identical
  //    final line IDs after path processing and sanitization
  // 4. GENERIC TYPE VARIATIONS: Complex generic types with nested parameters can
  //    collapse to similar sanitized forms (e.g., Box<T>, Arc<T> both become Box_of_T, Arc_of_T)
  // 5. CROSS-REFERENCE CONFLICTS: Items referenced from multiple contexts (inherent impls,
  //    trait impls, external paths) can create overlapping line ID spaces
  // 
  // Without this deduplication, Copilot reviews fail immediately with no diagnostics,
  // making it impossible to identify and fix the root cause of the duplicate IDs.
  function collectAndFixLineIds(line: ReviewLine): void {
    if (line.LineId) {
      let finalLineId = line.LineId;
      const originalLineId = line.LineId;
      
      // If this line ID is already used, append a counter
      if (usedLineIds.has(finalLineId)) {
        const baseId = finalLineId;
        const counter = (lineIdCounters.get(baseId) || 1) + 1;
        lineIdCounters.set(baseId, counter);
        finalLineId = `${baseId}_${counter}`;
        
        // Keep incrementing until we find a unique ID
        while (usedLineIds.has(finalLineId)) {
          const newCounter = lineIdCounters.get(baseId)! + 1;
          lineIdCounters.set(baseId, newCounter);
          finalLineId = `${baseId}_${newCounter}`;
        }
        
        // Update the line ID and track the mapping
        line.LineId = finalLineId;
        lineIdMappings.set(originalLineId, finalLineId);
      }
      
      usedLineIds.add(finalLineId);
    }

    // Process children recursively
    if (line.Children && Array.isArray(line.Children)) {
      line.Children.forEach(collectAndFixLineIds);
    }
  }

  // Second pass: update all references to changed line IDs
  function updateReferences(line: ReviewLine): void {
    // Update RelatedToLine references
    if (line.RelatedToLine && lineIdMappings.has(line.RelatedToLine)) {
      line.RelatedToLine = lineIdMappings.get(line.RelatedToLine)!;
    }

    // Update NavigateToId references in tokens
    if (line.Tokens && line.Tokens.length > 0) {
      for (const token of line.Tokens) {
        if (token.NavigateToId && lineIdMappings.has(token.NavigateToId)) {
          token.NavigateToId = lineIdMappings.get(token.NavigateToId)!;
        }
      }
    }

    // Process children recursively
    if (line.Children && Array.isArray(line.Children)) {
      line.Children.forEach(updateReferences);
    }
  }

  // Process all review lines
  if (reviewLines && Array.isArray(reviewLines)) {
    // First pass: fix duplicate line IDs
    reviewLines.forEach(collectAndFixLineIds);
    
    // Second pass: update references if any IDs were changed
    if (lineIdMappings.size > 0) {
      reviewLines.forEach(updateReferences);
    }
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
