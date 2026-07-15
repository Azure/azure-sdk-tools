// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { afterAll, describe, expect, it } from "vitest";

const testFilePath = fileURLToPath(import.meta.url);
const testDir = path.dirname(testFilePath);
const repoRoot = path.dirname(testDir);
const cliPath = path.join(repoRoot, "dist", "src", "main.js");
const artifactsRoot = path.join(testDir, ".artifacts");

afterAll(() => {
  fs.rmSync(artifactsRoot, { force: true, recursive: true });
});

function makeRustdocInput(overrides: Record<string, unknown> = {}) {
  return {
    root: 0,
    crate_version: "1.2.3",
    index: {
      0: {
        id: 0,
        crate_id: 0,
        name: "sample_crate",
        docs: "crate docs",
        inner: {
          module: {
            is_crate: true,
            items: [],
          },
        },
      },
    },
    paths: {},
    external_crates: {},
    format_version: 45,
    ...overrides,
  };
}

function runCli(testName: string, inputContents: string) {
  const workDir = path.join(artifactsRoot, testName);
  const inputPath = path.join(workDir, "input.json");
  const outputPath = path.join(workDir, "output.json");

  fs.rmSync(workDir, { force: true, recursive: true });
  fs.mkdirSync(workDir, { recursive: true });
  fs.writeFileSync(inputPath, inputContents);

  execFileSync("node", [cliPath, inputPath, outputPath], {
    cwd: repoRoot,
    encoding: "utf8",
    stdio: "pipe",
  });

  const outputContents = fs.readFileSync(outputPath, "utf8");
  return {
    outputContents,
    outputJson: JSON.parse(outputContents),
  };
}

describe("main", () => {
  it("passes through APIView token files with ParserVersion >= 2.0.0", () => {
    const inputContents = `{
  "PackageName": "sample_crate",
  "PackageVersion": "1.2.3",
  "ParserVersion": "2.0.0",
  "Language": "Rust",
  "ReviewLines": []
}
`;

    const { outputContents } = runCli("passthrough-parser-version-2", inputContents);
    expect(outputContents).toBe(inputContents);
  });

  it("falls back to legacy conversion when ParserVersion is missing", () => {
    const inputContents = JSON.stringify(makeRustdocInput({ format_version: undefined }), null, 2);

    const { outputJson } = runCli("fallback-missing-parser-version", inputContents);
    expect(outputJson.PackageName).toBe("sample_crate");
    expect(outputJson.PackageVersion).toBe("1.2.3");
    expect(outputJson.ParserVersion).toBe("1.1.1");
    expect(outputJson.Language).toBe("Rust");
  });

  it("keeps converting legacy rustdoc JSON inputs", () => {
    const inputContents = JSON.stringify(makeRustdocInput(), null, 2);

    const { outputJson } = runCli("legacy-rustdoc-json", inputContents);
    expect(outputJson.PackageName).toBe("sample_crate");
    expect(outputJson.PackageVersion).toBe("1.2.3");
    expect(outputJson.ParserVersion).toBe("1.1.1");
    expect(Array.isArray(outputJson.ReviewLines)).toBe(true);
  });
});
