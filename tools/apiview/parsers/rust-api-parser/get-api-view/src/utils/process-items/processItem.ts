import { ReviewLine, } from "../apiview-models";
import { Crate, Item, } from "../rustdoc-json-types/jsonTypes";
import { processFunction } from "./processFunction";
import { processModule } from "./processModule";
import { processStruct } from "./processStruct";
import { processTrait } from "./processTrait";

const processedItems = new Set<string>();

/**
 * Processes an item from the API JSON and returns a ReviewLine object.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The item to process.
 * @returns {ReviewLine | null} The ReviewLine object or null if the item is not processed.
 */
export function processItem(apiJson: Crate, item: Item, reviewLines?: ReviewLine[]): ReviewLine[] | null {
    // Check if the item has already been processed
    if (item.name && processedItems.has(item.name)) {
        return null;
    }
    item.name && processedItems.add(item.name);

    if (!reviewLines) {
        reviewLines = [];
    }

    // Add documentation token if available
    // if (item.docs) {
    // TODO: Push to children and add link with "related to line"
    // reviewLine.Tokens.push({
    //     Kind: TokenKind.Comment,
    //     Value: `/// ${item.docs}`,
    //     IsDocumentation: true
    // });
    // }
    if (typeof item.inner === 'object') {
        if ('module' in item.inner) {
            processModule(apiJson, item,  reviewLines);
        } else if ('function' in item.inner) {
            processFunction(item,  reviewLines);
        } else if ('struct' in item.inner) {
            processStruct(apiJson, item,  reviewLines);
        } else if ('trait' in item.inner) {
            processTrait(apiJson, item,  reviewLines);
        }
    }

    return reviewLines;
}
