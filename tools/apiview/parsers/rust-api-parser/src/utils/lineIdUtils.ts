// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { ReviewLine } from "../models/apiview-models";

/**
 * Generates a stable ID for a review line based on its content
 * @param reviewLine The review line to generate an ID for
 * @returns A stable ID based on the content of the review line
 */
export function generateStableLineId(reviewLine: ReviewLine): string {
  // Create a string representation of the content
  let content = "";
  
  // Add token values to the content
  if (reviewLine.Tokens && reviewLine.Tokens.length > 0) {
    content = reviewLine.Tokens.map(token => token.Value || "").join("");
  }
  
  // For documentation lines, we need to include a docs prefix to differentiate them
  const isDocumentation = reviewLine.Tokens && 
    reviewLine.Tokens.length > 0 && 
    reviewLine.Tokens[0].IsDocumentation;
  
  if (isDocumentation) {
    content = "docs_" + content;
  }
  
  // Sanitize the content to make it a valid ID (remove special characters, limit length)
  content = content
    .replace(/[^\w\s]/g, "") // Remove special characters
    .replace(/\s+/g, "_") // Replace spaces with underscores
    .substring(0, 50); // Limit length to prevent very long IDs
  
  // If the original line ID includes a meaningful name part, preserve it
  let prefix = "";
  if (reviewLine.LineId) {
    // Try to extract any meaningful name part from the original ID
    const originalIdParts = reviewLine.LineId.split("_");
    if (originalIdParts.length > 0 && isNaN(Number(originalIdParts[0]))) {
      prefix = originalIdParts[0] + "_"; // TODO: replace the lineId part if it starts with `{number}_` with the 
    }
  }
  
  return prefix + content;
}

/**
 * Creates a mapping of old line IDs to new stable line IDs
 * @param reviewLines The review lines to process
 * @returns A map of old line IDs to new stable line IDs
 */
export function createLineIdMapping(reviewLines: ReviewLine[]): Map<string, string> {
  const idMap = new Map<string, string>();
  
  // Process all review lines recursively
  function processLines(lines: ReviewLine[]) {
    if (!lines) return;
    
    for (const line of lines) {
      if (line.LineId) {
        const stableId = generateStableLineId(line);
        idMap.set(line.LineId, stableId);
      }
      
      // Process any children
      if (line.Children && line.Children.length > 0) {
        processLines(line.Children);
      }
    }
  }
  
  processLines(reviewLines);
  return idMap;
}

/**
 * Updates line IDs and related line references to use stable IDs
 * @param reviewLines The review lines to update
 * @param idMap Mapping of old line IDs to new stable line IDs
 */
export function updateLineIds(reviewLines: ReviewLine[], idMap: Map<string, string>): void {
  // Process all review lines recursively
  function processLines(lines: ReviewLine[]) {
    if (!lines) return;
    
    for (const line of lines) {
      // Update the line ID if it exists
      if (line.LineId && idMap.has(line.LineId)) {
        line.LineId = idMap.get(line.LineId);
      }
      
      // Update any related line references
      if (line.RelatedToLine && idMap.has(line.RelatedToLine)) {
        line.RelatedToLine = idMap.get(line.RelatedToLine);
      }
      
      // Process any children
      if (line.Children && line.Children.length > 0) {
        processLines(line.Children);
      }
    }
  }
  
  processLines(reviewLines);
}

/**
 * Post-processes the code file to ensure line IDs are stable across versions
 * @param reviewLines The review lines to process
 */
export function applyStableLineIds(reviewLines: ReviewLine[]): void {
  // Create mapping from old IDs to new stable IDs
  const idMap = createLineIdMapping(reviewLines);
  
  // Update all lines to use the new stable IDs
  updateLineIds(reviewLines, idMap);
}