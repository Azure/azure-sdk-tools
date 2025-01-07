import { ApiJson, Item } from "./interfaces";

let document = '';
const processedItems = new Set<string>();

/**
 * Processes an item from the API JSON and appends its documentation to the document.
 * 
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The item to process.
 * @param {string} [indent=''] - The indentation to use for the item's documentation.
 */
export function processItem(apiJson: ApiJson, item: Item, indent: string = '') {
    // Check if the item has already been processed
    if (item.name && processedItems.has(item.name)) {
        return;
    }
    item.name && processedItems.add(item.name);

    // Append the item's documentation to the document
    if (item.docs) {
        document += `${indent}/// ${item.docs}\n`;
    }

    // Process the item based on its visibility and type
    if (item.visibility === 'public') {
        if (item.inner.module) {
            processModule(apiJson, item, indent);
        } else if (item.inner.function) {
            processFunction(item, indent);
        } else if (item.inner.struct) {
            processStruct(apiJson, item, indent);
        } else if (item.inner.trait) {
            processTrait(apiJson, item, indent);
        }
    }
}

/**
 * Processes a module item and appends its documentation to the document.
 * 
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The module item to process.
 * @param {string} indent - The indentation to use for the module's documentation.
 */
function processModule(apiJson: ApiJson, item: Item, indent: string) {
    document += `${indent}pub mod ${item.name} {\n`;
    if (item.inner.module.items) {
        item.inner.module.items.forEach((childId: string) => {
            const childItem = apiJson.index[childId];
            processItem(apiJson, childItem, indent + '    ');
        });
    }
    document += `${indent}}\n`;
}

/**
 * Processes a function item and appends its documentation to the document.
 * 
 * @param {Item} item - The function item to process.
 * @param {string} indent - The indentation to use for the function's documentation.
 */
function processFunction(item: Item, indent: string) {
    document += `${indent}pub fn ${item.name}`;
    if (item.inner.function.generics.params.length > 0) {
        document += `<${item.inner.function.generics.params.map((param: any) => param.name).join(', ')}>`;
    }
    document += `(${item.inner.function.sig.inputs.map((input: any) => {
        if (input[1].primitive) {
            return `${input[0]}: ${input[1].primitive}`;
        } else if (input[1].resolved_path) {
            return `${input[0]}: ${input[1].resolved_path.name}`;
        } else if (input[1].borrowed_ref) {
            return `${input[0]}: &${input[1].borrowed_ref.type.generic}`;
        } else {
            return `${input[0]}: unknown`;
        }
    }).join(', ')})`;
    if (item.inner.function.sig.output) {
        if (item.inner.function.sig.output.primitive) {
            document += ` -> ${item.inner.function.sig.output.primitive}`;
        } else if (item.inner.function.sig.output.resolved_path) {
            document += ` -> ${item.inner.function.sig.output.resolved_path.name}`;
        }
    }
    document += `\n`;
}

/**
 * Processes a struct item and appends its documentation to the document.
 * 
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The struct item to process.
 * @param {string} indent - The indentation to use for the struct's documentation.
 */
function processStruct(apiJson: ApiJson, item: Item, indent: string) {
    document += `${indent}pub struct ${item.name} {\n`;
    if (item.inner.struct.kind.plain.fields) {
        item.inner.struct.kind.plain.fields.forEach((fieldId: string) => {
            const fieldItem = apiJson.index[fieldId];
            if (fieldItem && fieldItem.inner.struct_field) {
                if (fieldItem.inner.struct_field.primitive) {
                    document += `${indent}    pub ${fieldItem.name}: ${fieldItem.inner.struct_field.primitive},\n`;
                } else if (fieldItem.inner.struct_field.resolved_path) {
                    document += `${indent}    pub ${fieldItem.name}: ${fieldItem.inner.struct_field.resolved_path.name},\n`;
                }
            }
        });
    }
    document += `${indent}}\n`;
}

/**
 * Processes a trait item and appends its documentation to the document.
 * 
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The trait item to process.
 * @param {string} indent - The indentation to use for the trait's documentation.
 */
function processTrait(apiJson: ApiJson, item: Item, indent: string) {
    document += `${indent}pub trait ${item.name} {\n`;
    if (item.inner.trait.items) {
        item.inner.trait.items.forEach((methodId: string) => {
            const methodItem = apiJson.index[methodId];
            if (methodItem.inner.function) {
                document += `${indent}    fn ${methodItem.name}`;
                if (methodItem.inner.function.generics.params.length > 0) {
                    document += `<${methodItem.inner.function.generics.params.map((param: any) => param.name).join(', ')}>`;
                }
                document += `(${methodItem.inner.function.sig.inputs.map((input: any) => {
                    if (input[1].primitive) {
                        return `${input[0]}: ${input[1].primitive}`;
                    } else if (input[1].resolved_path) {
                        return `${input[0]}: ${input[1].resolved_path.name}`;
                    } else if (input[1].borrowed_ref) {
                        return `${input[0]}: &${input[1].borrowed_ref.type.generic}`;
                    } else {
                        return `${input[0]}: unknown`;
                    }
                }).join(', ')})`;
                if (methodItem.inner.function.sig.output) {
                    if (methodItem.inner.function.sig.output.primitive) {
                        document += ` -> ${methodItem.inner.function.sig.output.primitive}`;
                    } else if (methodItem.inner.function.sig.output.resolved_path) {
                        document += ` -> ${methodItem.inner.function.sig.output.resolved_path.name}`;
                    }
                }
                document += `;\n`;
            }
        });
    }
    document += `${indent}}\n`;
}

export function getDocument() {
    return document;
}