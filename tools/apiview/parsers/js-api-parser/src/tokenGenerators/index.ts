import { enumTokenGenerator } from "./enum";
import { ReviewLine, ReviewToken } from "../models";
import { ApiItem } from "@microsoft/api-extractor-model";
import { functionTokenGenerator } from "./function";
import { interfaceTokenGenerator } from "./interfaces";
import { classTokenGenerator } from "./class";
import { methodTokenGenerator } from "./method";
import { propertyTokenGenerator } from "./property";

/**
 * Interface for token generators that create ReviewLines from ApiItems.
 */
export interface TokenGenerator<T extends ApiItem = ApiItem> {
  /**
   * Validates if the given ApiItem can be processed by this token generator.
   * @param item - The ApiItem to validate.
   * @returns True if the item is valid; otherwise, false.
   */
  isValid(item: ApiItem): item is T;

  /**
   * Generates a ReviewLine from the given ApiItem, with optional child lines for complex types.
   * @param item - The ApiItem to process.
   * @param deprecated - Indicates if the Api is deprecated.
   * @returns A ReviewLine generated from the ApiItem.
   */
  generate(item: T, deprecated?: boolean): ReviewLine;
}

export const generators: TokenGenerator[] = [
  enumTokenGenerator,
  classTokenGenerator,
  functionTokenGenerator,
  interfaceTokenGenerator,
  methodTokenGenerator,
  propertyTokenGenerator,
];

export { propertyTokenGenerator } from "./property";
