import * as fs from 'fs';
import { ApiJson } from './utils/interfaces';
import { getDocument, processItem } from './utils/processItem';
import { getRootModules } from './utils/rootModules';

function main() {
    // Read the JSON file
    const data = fs.readFileSync('../clean-rust-doc-output/outputs/docs.api.json', 'utf8');

    // Parse the JSON data
    let apiJson: ApiJson = JSON.parse(data);

    const rootModules = getRootModules(apiJson);

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
