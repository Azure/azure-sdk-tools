import { ApiJson, Item } from "./interfaces";

export function getRootModules(apiJson: ApiJson): Item[] {
    const childModuleIds = new Set<string>();
    Object.values(apiJson.index).forEach(item => {
        if (item.inner.module && item.inner.module.items) {
            item.inner.module.items.forEach((childId: string) => {
                childModuleIds.add(childId);
            });
        }
    });

    return Object.values(apiJson.index).filter(item =>
        item.inner.module &&
        item.id && !childModuleIds.has(item.id)
    );
}
