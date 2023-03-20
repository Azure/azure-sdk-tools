import { createTestHost, createTestWrapper } from "@typespec/compiler/testing";
import { RestTestLibrary } from "@typespec/rest/testing";
import { HttpTestLibrary } from "@typespec/http/testing";
import { VersioningTestLibrary } from "@typespec/versioning/testing";
import { AzureCoreTestLibrary } from "@azure-tools/typespec-azure-core/testing";
import { ApiViewTestLibrary } from "../src/testing/index.js";
import "@azure-tools/typespec-apiview";
import { ApiViewEmitterOptions } from "../src/lib.js";
import { ApiViewDocument, ApiViewTokenKind } from "../src/apiview.js";
import { Diagnostic, resolvePath } from "@typespec/compiler";
import { strictEqual } from "assert";

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
    emitters: { "@azure-tools/typespec-apiview": { ...options, "output-file": outPath } },
    miscOptions: { "disable-linter": true },
  });
  return diagnostics;
}

export async function apiViewFor(code: string, options: ApiViewEmitterOptions): Promise<ApiViewDocument> {
  const runner = await createApiViewTestRunner({withVersioning: true});
  const outPath = resolvePath("/apiview.json");
  await runner.compile(code, {
    noEmit: false,
    emitters: { "@azure-tools/typespec-apiview": { ...options, "output-file": outPath } },
    miscOptions: { "disable-linter": true },
  });

  const jsonText = runner.fs.get(outPath)!;
  const apiview = JSON.parse(jsonText) as ApiViewDocument;
  return apiview;
}

export function apiViewText(apiview: ApiViewDocument): string[] {
  const vals = new Array<string>;
  for (const token of apiview.Tokens) {
    switch (token.Kind) {
      case ApiViewTokenKind.Newline:
        vals.push("\n");
        break;
      default:
        if (token.Value !== undefined) {
          vals.push(token.Value);
        }
        break;
    }
  }
  return vals.join("").split("\n");
}

function getIndex(lines: string[]): number {
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
  const indent = getIndex(lines);
  for (const line of lines) {
    if (line.trim() === '') {
      // skip blank lines
      continue;
    } else {
      // remove any leading indentation
      trimmed.push(line.substring(indent));
    }
  }
  return trimmed;
}

/** Compares an expected string to a subset of the actual output. */
export function compare(expect: string, lines: string[], offset: number) {
  // split the input into lines and ignore leading or trailing empty lines.
  const expectedLines = trimLines(expect.split("\n"));
  const checkLines = trimLines(lines.slice(offset));
  strictEqual(expectedLines.length, checkLines.length);
  for (let x = 0; x < checkLines.length; x++) {
    strictEqual(expectedLines[x], checkLines[x], `Actual differed from expected at line #${x + 1}\nACTUAL: '${checkLines[x]}'\nEXPECTED: '${expectedLines[x]}'`);
  }
}
