import * as fs from 'fs';

interface Span {
    filename: string;
    begin: [number, number];
    end: [number, number];
}

interface Item {
    id: string;
    name?: string;
    docs?: string;
    visibility: string;
    span?: Span;
    inner: any;
}

interface ApiJson {
    crate_version: string;
    includes_private: boolean;
    index: { [key: string]: Item };
}

let apiJson: ApiJson;

// Create a structured document
let document = '';
const processedItems = new Set<string>();

function processItem(item: Item, indent: string = '') {
    if (item.name && processedItems.has(item.name)) {
        return;
    }
    item.name && processedItems.add(item.name);

    if (item.docs) {
        document += `${indent}/// ${item.docs}\n`;
    }
    if (item.visibility === 'public') {
        if (item.inner.module) {
            document += `${indent}pub mod ${item.name} {\n`;
            if (item.inner.module.items) {
                item.inner.module.items.forEach((childId: string) => {
                    const childItem = apiJson.index[childId];
                    processItem(childItem, indent + '    ');
                });
            }
            document += `${indent}}\n`;
        } else if (item.inner.function) {
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
        } else if (item.inner.struct) {
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
        } else if (item.inner.trait) {
            document += `${indent}pub trait ${item.name} {\n`;
            if (item.inner.trait.items) {
                item.inner.trait.items.forEach((methodId: string) => {
                    const methodItem = apiJson.index[methodId];
                    if (methodItem && methodItem.inner.function) {
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
    }
}

function main() {
    // Read the JSON file
    const data = fs.readFileSync('inputs/docs.api.json', 'utf8');

    // Parse the JSON data
    apiJson = JSON.parse(data);

    // Identify root modules
    const childModuleIds = new Set<string>();
    Object.values(apiJson.index).forEach(item => {
        if (item.inner.module && item.inner.module.items) {
            item.inner.module.items.forEach((childId: string) => {
                childModuleIds.add(childId);
            });
        }
    });

    const rootModules = Object.values(apiJson.index).filter(item =>
        item.inner.module &&
        item.id && !childModuleIds.has(item.id)
    );

    // Debug: Print root modules
    console.log('Root Modules:', rootModules.map(item => item.name));

    // Process each root module
    rootModules.forEach(rootModule => processItem(rootModule));

    // Write the document to a file
    const outputFilePath = 'outputs/docs.rs';
    fs.writeFileSync(outputFilePath, document);

    console.log(`The exported API surface has been successfully saved to '${outputFilePath}'`);
}

main();
