import { NodeHost, compile, getSourceLocation } from "@typespec/compiler";
import { parse, isImportStatement } from "@typespec/compiler";
import { Logger } from "./log.js";
import { spawn } from "node:child_process";

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
