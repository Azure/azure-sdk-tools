import { ReviewLine, } from "../models/apiview-models";
import { Crate, Item, } from "../models/rustdoc-json-types";
import { processConstant } from "./processConstant";
import { processEnum } from "./processEnum";
import { processFunction } from "./processFunction";
import { processModule } from "./processModule";
import { processStatic } from "./processStatic";
import { processStruct } from "./processStruct";
import { processTrait } from "./processTrait";
import { processUse } from "./processUse";

/**
 * Processes an item from the API JSON and returns a ReviewLine object.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The item to process.
 * @returns {ReviewLine | null} The ReviewLine object or null if the item is not processed.
 */
export function processItem(apiJson: Crate, item: Item, reviewLines?: ReviewLine[]): ReviewLine[] | null {
    if (!reviewLines) {
        reviewLines = [];
    }

    if (typeof item.inner === 'object') {
        if ('module' in item.inner) {
            processModule(apiJson, item, reviewLines);
        } else if ('use' in item.inner) {
            processUse(item, reviewLines);
        } else if ('function' in item.inner) {
            processFunction(item, reviewLines);
        } else if ('struct' in item.inner) {
            processStruct(apiJson, item, reviewLines);
        } else if ('trait' in item.inner) {
            processTrait(apiJson, item, reviewLines);
        } else if ('static' in item.inner) {
            processStatic(item, reviewLines);
        } else if ('constant' in item.inner) {
            processConstant(item, reviewLines);
        } else if ('enum' in item.inner) {
            processEnum(apiJson, item, reviewLines);
        }
    }

    return reviewLines;
}