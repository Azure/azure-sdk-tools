import { createTestHost, createTestWrapper } from "@typespec/compiler/testing";
import { RestTestLibrary } from "@typespec/rest/testing";
import { HttpTestLibrary } from "@typespec/http/testing";
import { VersioningTestLibrary } from "@typespec/versioning/testing";
import { AzureCoreTestLibrary } from "@azure-tools/typespec-azure-core/testing";
import { ApiViewTestLibrary } from "../src/testing/index.js";
import "@azure-tools/typespec-apiview";
import { ApiViewEmitterOptions } from "../src/lib.js";
import { Diagnostic, resolvePath } from "@typespec/compiler";
import { strictEqual } from "assert";
import { CodeFile } from "../src/schemas.js";
import { reviewLineText } from "../src/util.js";

export async function createApiViewTestHost() {
  return createTestHost({
    libraries: [ApiViewTestLibrary, RestTestLibrary, HttpTestLibrary, VersioningTestLibrary, AzureCoreTestLibrary],
  });
}

export async function createApiViewTestRunner({
  withVersioning,
}: { withVersioning?: boolean } = {}) {
  const host = await createApiViewTestHost();
  const autoUsings = [
    "TypeSpec.Rest",
    "TypeSpec.Http",
  ]
  if (withVersioning) {
    autoUsings.push("TypeSpec.Versioning");
  }
  return createTestWrapper(host, {
    autoUsings: autoUsings,
    compilerOptions: {
      emit: ["@azure-tools/typespec-apiview"],
    }
  });
}

export async function diagnosticsFor(code: string, options: ApiViewEmitterOptions): Promise<readonly Diagnostic[]> {
  const runner = await createApiViewTestRunner({withVersioning: true});
  const outPath = resolvePath("/apiview.json");
  const diagnostics = await runner.diagnose(code, {
    noEmit: false,
    emit: ["@azure-tools/typespec-apiview"],
    options: {
      "@azure-tools/typespec-apiview": {
        ...options,
        "output-file": outPath,  
      }
    },
    miscOptions: { "disable-linter": true },
  });
  return diagnostics;
}

export async function apiViewFor(code: string, options: ApiViewEmitterOptions): Promise<CodeFile> {
  const runner = await createApiViewTestRunner({withVersioning: true});
  const outPath = resolvePath("/apiview.json");
  await runner.compile(code, {
    noEmit: false,
    emit: ["@azure-tools/typespec-apiview"],
    options: {
      "@azure-tools/typespec-apiview": {
        ...options,
        "output-file": outPath,  
      }
    },
    miscOptions: { "disable-linter": true },
  });

  const jsonText = runner.fs.get(outPath)!;
  const apiview = JSON.parse(jsonText) as CodeFile;
  return apiview;
}

export function apiViewText(apiview: CodeFile): string[] {
  return apiview.ReviewLines.map(l => reviewLineText(l, 0)).join("\n").split("\n");
}

function getBaseIndent(lines: string[]): number {
  for (const line of lines) {
    if (line.trim() !== "") {
      return line.length - line.trimStart().length;
    }
  }
  return 0;
}

/** Eliminates leading indentation and blank links that can mess with comparisons */
function trimLines(lines: string[]): string[] {
  const trimmed: string[] = [];
  const indent = getBaseIndent(lines);
  
  // if first line is blank, skip it
  if (lines[0].trim() === "") {
    lines = lines.slice(1);
  }

  for (const line of lines) {
    if (line.trim() === "") {
      // ensure blank lines are compared consistently
      trimmed.push("");
    } else {
      // remove any leading indentation
      trimmed.push(line.substring(indent));
    }
  }

  // if last line is blank, skip it
  const lastLine = trimmed.pop();
  if (lastLine && lastLine.trim() !== "") {
    trimmed.push(lastLine)
  }
  return trimmed;
}

/** Compares an expected string to a subset of the actual output. */
export function compare(expect: string, lines: string[], offset: number) {
  // split the input into lines and ignore leading or trailing empty lines.
  const expectedLines = trimLines(expect.split("\n"));
  const actualLines = trimLines(lines.slice(offset));
  for (let x = 0; x < actualLines.length; x++) {
    strictEqual(actualLines[x], expectedLines[x], `Actual differed from expected at line #${x + 1}\nACTUAL: '${actualLines[x]}'\nEXPECTED: '${expectedLines[x]}'`);
  }
}
