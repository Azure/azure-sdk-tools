// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { existsSync } from "node:fs";
import { createRequire } from "node:module";
import { resolve, join } from "node:path";
import { pathToFileURL } from "node:url";

// Resolve the compiler and versioning library from the inspected project, not
// from the azsdk installation. This uses the same libraries as the spec build.
const project = resolve(process.argv[2]);
const require = createRequire(join(project, "package.json"));
const compiler = await import(pathToFileURL(require.resolve("@typespec/compiler")).href);
const versioning = await import(pathToFileURL(require.resolve("@typespec/versioning")).href);
const entrypoint = join(project, existsSync(join(project, "main.tsp")) ? "main.tsp" : "client.tsp");
const [options, configDiagnostics] = await compiler.resolveCompilerOptions(compiler.NodeHost, {
    entrypoint,
    configPath: join(project, "tspconfig.yaml"),
    cwd: project,
});
const program = await compiler.compile(compiler.NodeHost, entrypoint, { ...options, noEmit: true });
const errors = [...configDiagnostics, ...program.diagnostics].filter((diagnostic) => diagnostic.severity === "error");
if (errors.length) {
    throw new Error(errors.map((diagnostic) => diagnostic.message).join("\n"));
}
const services = compiler.listServices(program);
if (services.length !== 1) {
    throw new Error("Release target validation requires exactly one versioned service in the TypeSpec project.");
}
const [, versions] = versioning.getVersions(program, services[0].type);
const available = versions?.getVersions().map((version) => version.value) ?? [];
if (!available.length) {
    throw new Error("No API versions were declared by the service. An explicit versioned release target is required.");
}
console.log(JSON.stringify(available));
