import * as fs from 'fs';
import { processItem } from './utils/process-items/processItem';
import { getRootModules } from './utils/rootModules';
import { CodeFile } from './utils/apiview-models';
import { Crate } from './utils/rustdoc-json-types/jsonTypes';

function main() {
    // Read the JSON file
    const data = fs.readFileSync('../clean-rust-doc-output/outputs/docs.api.json', 'utf8');
    // Parse the JSON data
    let apiJson: Crate = JSON.parse(data);
    const rootModules = getRootModules(apiJson);

    // Create the CodeFile object
    const codeFile: CodeFile = {
        PackageName: "your-package-name",
        PackageVersion: "your-package-version",
        ParserVersion: "your-parser-version",
        Language: "Rust",
        ReviewLines: []
    };

    // Process each root module and add to ReviewLines
    rootModules.forEach(rootModule => {
        const reviewLines = processItem(apiJson, rootModule);
        if (reviewLines) {
            codeFile.ReviewLines.push(...reviewLines);
        }
    });

    // Write the JSON output to a file
    const outputFilePath = 'outputs/docs.api.json';
    fs.writeFileSync(outputFilePath, JSON.stringify(codeFile, null, 2));
    console.log(`The exported API surface has been successfully saved to '${outputFilePath}'`);
}

main();
