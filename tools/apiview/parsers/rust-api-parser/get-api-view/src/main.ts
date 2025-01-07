import * as fs from 'fs';
import { ApiJson, Item } from './utils/interfaces';
import { getDocument, processItem } from './utils/processItem';

function main() {
    // Read the JSON file
    const data = fs.readFileSync('../clean-rust-doc-output/outputs/docs.api.json', 'utf8');

    // Parse the JSON data
    let apiJson: ApiJson = JSON.parse(data);

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
    rootModules.forEach(rootModule => processItem(apiJson, rootModule));

    // Write the document to a file
    const outputFilePath = 'outputs/docs.rs';
    fs.writeFileSync(outputFilePath, getDocument());

    console.log(`The exported API surface has been successfully saved to '${outputFilePath}'`);
}

main();
