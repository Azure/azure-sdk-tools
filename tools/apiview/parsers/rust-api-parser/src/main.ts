import * as fs from 'fs';
import { processItem } from './process-items/processItem';
import { CodeFile } from './utils/apiview-models';
import { Crate } from './utils/rustdoc-json-types/jsonTypes';

function main() {
    // Read the JSON file
    const args = process.argv.slice(2);
    const packageArg = args.find(arg => arg.startsWith('--package='));
    if (!packageArg) {
        throw new Error("Please provide a --package argument");
    }
    const packageName = packageArg.split('=')[1];
    const data = fs.readFileSync(`./inputs/${packageName}_compact.json`, 'utf8');
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
    const outputFilePath = `outputs/${packageName}.api.json`;
    fs.writeFileSync(outputFilePath, JSON.stringify(codeFile, null, 2));
    console.log(`The exported API surface has been successfully saved to '${outputFilePath}'`);
}

main();
