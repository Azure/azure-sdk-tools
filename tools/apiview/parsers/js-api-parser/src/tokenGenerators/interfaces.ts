import { ApiItem } from '@microsoft/api-extractor-model';
import { ReviewToken } from '../models';

/**
 * Interface for token generators that create ReviewTokens from ApiItems.
 */
export interface ITokenGenerator {
    /**
     * Validates if the given ApiItem can be processed by this token generator.
     * @param item - The ApiItem to validate.
     * @returns True if the item is valid; otherwise, false.
     */
    isValid(item: ApiItem): boolean;

    /**
     * Generates ReviewTokens from the given ApiItem.
     * @param item - The ApiItem to process.
     * @returns An array of ReviewTokens generated from the ApiItem.
     */
    generate(item: ApiItem): ReviewToken[];
}