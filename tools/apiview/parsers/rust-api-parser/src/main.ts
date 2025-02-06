import * as fs from "fs";
import { processItem } from "./process-items/processItem";
import { CodeFile } from "./models/apiview-models";
import { Crate } from "./models/rustdoc-json-types";

function main() {
  // Read the JSON file
  const args = process.argv.slice(2);
  if (args.length < 2) {
    throw new Error("Please provide input and output file paths as arguments");
  }
  const inputFilePath = args[0];
  const outputFilePath = args[1];

  const data = fs.readFileSync(inputFilePath, "utf8");
  // Parse the JSON data
  let apiJson: Crate = JSON.parse(data);
  // Create the CodeFile object
  const codeFile: CodeFile = {
    PackageName: apiJson.index[apiJson.root].name || "unknown",
    PackageVersion: apiJson["crate_version"] || "unknown",
    ParserVersion: "1.0.0",
    Language: "Rust",
    ReviewLines: [],
  };

  const reviewLines = processItem(apiJson, apiJson.index[apiJson.root]);
  if (reviewLines) {
    codeFile.ReviewLines.push(...reviewLines);
  }

  // Write the JSON output to a file
  fs.writeFileSync(outputFilePath, JSON.stringify(codeFile, null, 2));
  console.log(`The exported API surface has been successfully saved to '${outputFilePath}'`);
}

main();
