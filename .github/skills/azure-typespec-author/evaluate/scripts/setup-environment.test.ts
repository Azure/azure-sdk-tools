import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test, type TestContext } from "node:test";
import { fixtureDependencies } from "./setup-fixture-files.ts";

function fixture(t: TestContext) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "tools-eval-setup with spaces-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const evaluate = path.join(root, ".github", "skills", "azure-typespec-author", "evaluate");
  const scripts = path.join(evaluate, "scripts");
  const tools = path.join(root, "eng", "common", "scripts", "eval");
  const bin = path.join(root, "bin");
  const log = path.join(root, "commands.jsonl");
  const mock = path.join(root, "commands.cjs");
  fs.mkdirSync(scripts, { recursive: true });
  fs.mkdirSync(path.join(evaluate, "evals"));
  fs.writeFileSync(path.join(evaluate, "evals", "example.eval.yaml"), "agent_environment: azsdk-mcp\n");
  for (const script of ["setup-environment.ts", "setup-fixture-files.ts", "install-pnpm.ts", "check-node-dependencies.ts"]) {
    fs.copyFileSync(new URL(script, import.meta.url), path.join(scripts, script));
  }
  fs.mkdirSync(path.join(tools, "node_modules", ".bin"), { recursive: true });
  fs.writeFileSync(path.join(tools, "package.json"),
    JSON.stringify({ devDependencies: { "@microsoft/vally-cli": "0.19.0" } }));
  fs.mkdirSync(bin);
  fs.writeFileSync(mock, `
    const fs = require("node:fs");
    const path = require("node:path");
    const [command, ...args] = process.argv.slice(2);
    fs.appendFileSync(process.env.TEST_COMMAND_LOG, JSON.stringify({ command, args, cwd: process.cwd() }) + "\\n");
    if (args[0] === "--version") {
      console.log(command === "vally" ? "0.19.0" : "9.0.0");
    } else if (command === "pnpm" && args[0] === "install") {
      for (const name of Object.keys(JSON.parse(fs.readFileSync("package.json", "utf8")).devDependencies)) {
        const directory = path.join("node_modules", ...name.split("/"));
        fs.mkdirSync(directory, { recursive: true });
        fs.writeFileSync(path.join(directory, "package.json"), "{}");
      }
    } else if (command !== "npm" || args[0] !== "ci") {
      throw new Error("Unexpected command: " + command + " " + args.join(" "));
    }
  `);
  for (const [command, directory] of [["npm", bin], ["pnpm", bin],
    ["vally", path.join(tools, "node_modules", ".bin")]]) {
    const content = process.platform === "win32"
      ? `@"${process.execPath}" "${mock}" ${command} %*\r\n`
      : `#!/bin/sh\nexec "${process.execPath}" "${mock}" ${command} "$@"\n`;
    fs.writeFileSync(path.join(directory, command + (process.platform === "win32" ? ".cmd" : "")),
      content, { mode: 0o755 });
  }
  const specs = path.join(root, "spec checkout");
  fs.mkdirSync(path.join(specs, ".github"), { recursive: true });
  fs.writeFileSync(path.join(specs, ".github", "copilot-instructions.md"), "# Specs instructions\n");
  fs.writeFileSync(path.join(specs, "package.json"), JSON.stringify({ packageManager: "pnpm@9.0.0" }));
  fs.writeFileSync(path.join(specs, "pnpm-workspace.yaml"),
    `catalog:\n${fixtureDependencies.map((name) => `  "${name}": 1.0.0`).join("\n")}\n`);
  const mcpRoot = path.join(root, "mcp artifacts");
  fs.mkdirSync(path.join(mcpRoot, "mock"), { recursive: true });
  const dll = path.join(mcpRoot, "mock", "azsdk-mock.dll");
  fs.writeFileSync(dll, "");
  return {
    root, evaluate, tools, log, dll,
    run: () => spawnSync(process.execPath, ["--experimental-strip-types", path.join(scripts, "setup-environment.ts"),
      "--mcp-kind", "mock", "--mcp-root", mcpRoot, "--spec-root", specs], {
      cwd: root, encoding: "utf8",
      env: { ...process.env, PATH: `${bin}${path.delimiter}${process.env.PATH}`,
        SHELL: "", BASH: "", TEST_COMMAND_LOG: log },
    }),
  };
}

test("local setup installs only the standalone fixture using a separate specs checkout", (t) => {
  const setup = fixture(t);
  const result = setup.run();
  assert.ifError(result.error);
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const widget = path.join(setup.evaluate, "fixtures", "Microsoft.Widget", "Widget");
  const commands: { command: string; args: string[]; cwd: string }[] =
    fs.readFileSync(setup.log, "utf8").trim().split(/\r?\n/).map((line) => JSON.parse(line));
  assert.deepEqual(commands.filter((command) => command.args[0] !== "--version"), [
    { command: "npm", args: ["ci", "--no-audit", "--no-fund"], cwd: setup.tools },
    { command: "pnpm", args: ["install"], cwd: widget },
  ]);
  assert.ok(result.stdout.includes(path.join(widget, "node_modules")));
  assert.ok(result.stdout.trim().split(/\r?\n/).every((line) =>
    line.startsWith(process.platform === "win32" ? "$env:" : "export ")));
  assert.equal(fs.readFileSync(path.join(setup.evaluate, "fixtures", "instructions-test", "copilot-instructions.md"),
    "utf8"), "# Specs instructions\n");
  assert.equal(fs.readFileSync(path.join(setup.evaluate, "evals", "example.eval.yaml"), "utf8"),
    "agent_environment: azsdk-mcp-mock-local\n");
  assert.equal(fs.existsSync(path.join(setup.root, "pnpm-workspace.yaml")), false);
});

test("missing supplied MCP artifacts fail before installs or eval rewrites", (t) => {
  const setup = fixture(t);
  fs.rmSync(setup.dll);
  const result = setup.run();
  assert.ifError(result.error);
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /MCP artifact is missing or is not a file/);
  assert.equal(fs.existsSync(setup.log), false);
  assert.equal(fs.readFileSync(path.join(setup.evaluate, "evals", "example.eval.yaml"), "utf8"),
    "agent_environment: azsdk-mcp\n");
});
