import { NodeHost, compile, getSourceLocation } from "@typespec/compiler";
import { parse, isImportStatement } from "@typespec/compiler";
import { Logger } from "./log.js";

export function resolveCliOptions(opts: string[]): Record<string, Record<string, unknown>> {
  const options: Record<string, Record<string, string>> = {};
  for (const option of opts ?? []) {
    const optionParts = option.split("=");
    if (optionParts.length !== 2) {
      throw new Error(
        `The --option parameter value "${option}" must be in the format: <emitterName>.some-options=value`
      );
    }
    let optionKeyParts = optionParts[0]!.split(".");
    if (optionKeyParts.length > 2) {
      // support emitter/path/file.js.option=xyz
      optionKeyParts = [
        optionKeyParts.slice(0, -1).join("."),
        optionKeyParts[optionKeyParts.length - 1]!,
      ];
    }
    let emitterName = optionKeyParts[0];
    emitterName = emitterName?.replace(".", "/")
    const key = optionKeyParts[1];
    if (!(emitterName! in options)) {
      options[emitterName!] = {};
    }
    options[emitterName!]![key!] = optionParts[1]!;
  }
  return options;
}

export async function resolveImports(file: string): Promise<string[]> {
  const imports: string[] = [];
  const node = await parse(file);
  for (const statement of node.statements) {
    if (isImportStatement(statement)) {
      imports.push(statement.path.value);
    }
  }
  return imports;
}

export async function compileTsp({
  emitterPackage,
  outputPath,
  resolvedMainFilePath,
  options,
}: {
  emitterPackage: string;
  outputPath: string;
  resolvedMainFilePath: string;
  options: Record<string, Record<string, unknown>>;
}) {
  Logger.debug(`Using emitter output dir: ${outputPath}`);
  // compile the local copy of the root file
  const program = await compile(NodeHost, resolvedMainFilePath, {
    outputDir: outputPath,
    emit: [emitterPackage],
    options: options,
  });

  if (program.diagnostics.length > 0) {
    for (const diagnostic of program.diagnostics) {
      const location = getSourceLocation(diagnostic.target);
      const source = location ? location.file.path : "unknown";
      console.error(
        `${diagnostic.severity}: ${diagnostic.code} - ${diagnostic.message} @ ${source}`,
      );
    }
  } else {
    Logger.success("generation complete");
  }
}
