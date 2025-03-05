import * as fs from "fs";
import { processItem } from "./process-items/processItem";
import { CodeFile } from "./models/apiview-models";
import { Crate, FORMAT_VERSION } from "../rustdoc-types/output/rustdoc-types";

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

  let hasFormatMismatch = false;
  if (apiJson.format_version !== FORMAT_VERSION) {
    hasFormatMismatch = true;
    console.warn(
      `Warning: Different format version detected: ${apiJson.format_version}, parser supports ${FORMAT_VERSION}. This may cause errors or unexpected results.`,
    );
  }

  // Create the CodeFile object
  const codeFile: CodeFile = {
    PackageName: apiJson.index[apiJson.root].name || "unknown",
    PackageVersion: apiJson["crate_version"] || "unknown",
    ParserVersion: "1.0.0",
    Language: "Rust",
    ReviewLines: [],
  };
  try {
    const reviewLines = processItem(apiJson.index[apiJson.root], apiJson);
    if (reviewLines) {
      codeFile.ReviewLines.push(...reviewLines);
    }

    // Write the JSON output to a file
    fs.writeFileSync(outputFilePath, JSON.stringify(codeFile, null, 2));
    console.log(`The exported API surface has been successfully saved to '${outputFilePath}'`);
  } catch (error) {
    const errorMessage = hasFormatMismatch
      ? `Failed to generate API surface (possibly due to format version mismatch): ${error.message}`
      : `Failed to generate API surface: ${error.message}`;
    throw new Error(errorMessage);
  }
}

main();
