import { enumTokenGenerator } from "./enum";
import { ReviewToken } from "../models";
import { ApiItem } from "@microsoft/api-extractor-model";
import { functionTokenGenerator } from "./function";
import { interfaceTokenGenerator } from "./interfaces";
import { classTokenGenerator } from "./class";

/**
 * Interface for token generators that create ReviewTokens from ApiItems.
 */
export interface TokenGenerator<T extends ApiItem = ApiItem> {
  /**
   * Validates if the given ApiItem can be processed by this token generator.
   * @param item - The ApiItem to validate.
   * @returns True if the item is valid; otherwise, false.
   */
  isValid(item: ApiItem): item is T;

  /**
   * Generates ReviewTokens from the given ApiItem.
   * @param item - The ApiItem to process.
   * @param deprecated - Indicates if the Api is deprecated.
   * @returns An array of ReviewTokens generated from the ApiItem.
   */
  generate(item: T, deprecated?: boolean): ReviewToken[];
}

export const generators: TokenGenerator[] = [
  enumTokenGenerator,
  classTokenGenerator,
  functionTokenGenerator,
  interfaceTokenGenerator,
];
