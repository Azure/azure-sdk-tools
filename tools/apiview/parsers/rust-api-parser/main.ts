import * as fs from 'fs';

interface Span {
    filename: string;
    begin: [number, number];
    end: [number, number];
}

interface Item {
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

function main() {
    // Read the JSON file
    const data = fs.readFileSync('inputs/docs.api.json', 'utf8');

    // Parse the JSON data
    const apiJson: ApiJson = JSON.parse(data);

    // Create a structured document
    let document = '';

    function processItem(item: Item, indent: string = '') {
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
                document += `${indent}pub fn ${item.name}()\n`;
            } else if (item.inner.struct) {
                document += `${indent}pub struct ${item.name} {\n`;
                if (item.inner.struct.fields) {
                    item.inner.struct.fields.forEach((fieldId: string) => {
                        const fieldItem = apiJson.index[fieldId];
                        if (fieldItem && fieldItem.inner.struct_field) {
                            document += `${indent}    pub ${fieldItem.name}: ${fieldItem.inner.struct_field.primitive},\n`;
                        }
                    });
                }
                document += `${indent}}\n`;
            } else if (item.inner.trait) {
                document += `${indent}pub trait ${item.name} {\n`;
                if (item.inner.trait.items) {
                    item.inner.trait.items.forEach((methodId: string) => {
                        const methodItem = apiJson.index[methodId];
                        if (methodItem && methodItem.name) {
                            document += `${indent}    fn ${methodItem.name}(&self);\n`;
                        }
                    });
                }
                document += `${indent}}\n`;
            }
        }
    }

    for (const item of Object.values(apiJson.index)) {
        processItem(item);
    }

    // Write the document to a file
    const outputFilePath = 'outputs/exported_api_surface.rs';
    fs.writeFileSync(outputFilePath, document);

    console.log(`The exported API surface has been successfully saved to '${outputFilePath}'`);
}

main();