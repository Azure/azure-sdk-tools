import { ApiJson, Item } from "./interfaces";

/**
 * Retrieves the root modules from the given ApiJson object.
 * Root modules are those that are not children of any other modules.
 *
 * @param {ApiJson} apiJson - The ApiJson object containing the API structure.
 * @returns {Item[]} An array of root module items.
 */
export function getRootModules(apiJson: ApiJson): Item[] {
    // Set to store IDs of all child modules
    const childModuleIds = new Set<string>();

    // Iterate over all items in the API index
    Object.values(apiJson.index).forEach(item => {
        // Check if the item is a module and has child items
        if (item.inner.module && item.inner.module.items) {
            // Add each child item ID to the set of child module IDs
            item.inner.module.items.forEach((childId: string) => {
                childModuleIds.add(childId);
            });
        }
    });

    // Filter out items that are root modules (not present in childModuleIds)
    return Object.values(apiJson.index).filter(item =>
        item.inner.module &&
        item.id && !childModuleIds.has(item.id)
    );
}