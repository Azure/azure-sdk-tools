import * as fs from 'fs';
import { processItem } from './process-items/processItem';
import { CodeFile } from './utils/apiview-models';
import { Crate } from './utils/rustdoc-json-types/jsonTypes';

function main() {
    // Read the JSON file
    const data = fs.readFileSync('./inputs/docs_compact.json', 'utf8');
    // Parse the JSON data
    let apiJson: Crate = JSON.parse(data);
    // Create the CodeFile object
    const codeFile: CodeFile = {
        PackageName: "your-package-name",
        PackageVersion: "your-package-version",
        ParserVersion: "your-parser-version",
        Language: "Rust",
        ReviewLines: []
    };

    const reviewLines = processItem(apiJson, apiJson.index[apiJson.root]);
    if (reviewLines) {
        codeFile.ReviewLines.push(...reviewLines);
    }

    // Write the JSON output to a file
    const outputFilePath = 'outputs/docs.api.json';
    fs.writeFileSync(outputFilePath, JSON.stringify(codeFile, null, 2));
    console.log(`The exported API surface has been successfully saved to '${outputFilePath}'`);
}

main();
