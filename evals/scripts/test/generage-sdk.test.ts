import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import type { ExecNpmOptions, ExecOptions } from "../../../eng/common/scripts/eval/lib/exec.ts";
import { generateSdk } from "../generage-sdk.ts";

const repositories = [
    { name: "azure-sdk-for-js", packageFile: ["sdk", "widget", "arm-widget", "package.json"] },
    { name: "azure-sdk-for-net", packageFile: ["sdk", "widget", "Azure.ResourceManager.Widget", "src", "Azure.ResourceManager.Widget.csproj"] },
    { name: "azure-sdk-for-python", packageFile: ["sdk", "widget", "azure-mgmt-widget", "setup.py"] },
    { name: "azure-sdk-for-java", packageFile: ["sdk", "widget", "azure-resourcemanager-widget", "pom.xml"] },
    { name: "azure-sdk-for-go", packageFile: ["sdk", "resourcemanager", "widget", "armwidget", "go.mod"] },
];

const scenarios = ["success", "clone failure", "sparse-checkout failure", "npm failure", "generation failure", "missing output", "missing config", "missing CLI"] as const;

for (const { repository, scenario } of repositories.flatMap(repository =>
    scenarios.map(scenario => ({ repository, scenario })))) {
    test(`generateSdk: ${repository.name}: ${scenario}`, async () => {
        const workspace = await mkdtemp(join(tmpdir(), "generate-sdk-test with spaces-"));
        try {
            const config = resolve(workspace, "specification", "widget", "resource-manager", "Microsoft.Widget", "Widget", "tspconfig.yaml");
            const azsdk = resolve(workspace, ".artifacts", "mcp", "cli", process.platform === "win32" ? "azsdk.exe" : "azsdk");
            const sdkRepo = resolve(workspace, repository.name);
            const packageFile = resolve(sdkRepo, ...repository.packageFile);
            const sdkRepoName = repository.name === "azure-sdk-for-js" ? undefined : repository.name;
            for (const file of [
                ...(scenario === "missing CLI" ? [] : [azsdk]),
                ...(scenario === "missing config" ? [] : [config]),
            ]) {
                await mkdir(dirname(file), { recursive: true });
                await writeFile(file, "");
            }

            const calls: { file: string; args: string[]; options: ExecNpmOptions }[] = [];
            const failure = new Error("Command failed");
            const commands = {
                async execFile(file: string, args: string[] = [], options: ExecOptions = {}) {
                    calls.push({ file, args, options });
                    if ((file === "git" && args[0] === "clone" && scenario === "clone failure")
                        || (file === "git" && args.includes("sparse-checkout") && scenario === "sparse-checkout failure")
                        || (file === azsdk && scenario === "generation failure")) {
                        throw failure;
                    }
                    if (file === azsdk && scenario === "success") {
                        await mkdir(dirname(packageFile), { recursive: true });
                        await writeFile(packageFile, "");
                    }
                    return { stdout: "", stderr: "" };
                },
                async execNpm(args: string[], options: ExecNpmOptions = {}) {
                    calls.push({ file: "npm", args, options });
                    if (scenario === "npm failure") {
                        throw failure;
                    }
                    return { stdout: "", stderr: "" };
                },
            };

            if (scenario === "success") {
                await generateSdk(workspace, sdkRepoName, commands);
                assert.deepEqual(calls.map(({ file }) => file), ["git", "git", "npm", azsdk]);
                assert.deepEqual(calls[0].args, [
                    "clone", "--depth", "1", "--filter=blob:none", "--sparse",
                    `https://github.com/Azure/${repository.name}.git`, sdkRepo,
                ]);
                assert.deepEqual(calls[1].args, [
                    "-C", sdkRepo, "sparse-checkout", "set",
                    repository.name === "azure-sdk-for-js" ? "eng/common/tsp-client" : "eng",
                ]);
                assert.deepEqual(calls[2].args, ["ci"]);
                assert.equal(calls[2].options.prefix, resolve(sdkRepo, "eng", "common", "tsp-client"));
                assert.deepEqual(calls[3].args, [
                    "package", "generate", "--local-sdk-repo-path", sdkRepo,
                    "--tsp-config-path", config, "--output", "json",
                ]);
                assert.ok(calls.every(({ options }) => options.cwd === workspace));
            } else if (scenario === "missing output") {
                await assert.rejects(generateSdk(workspace, sdkRepoName, commands), /did not create an accessible/);
                assert.equal(calls.length, 4);
            } else if (scenario === "missing config" || scenario === "missing CLI") {
                await assert.rejects(generateSdk(workspace, sdkRepoName, commands), /ENOENT/);
                assert.equal(calls.length, 0);
            } else {
                await assert.rejects(generateSdk(workspace, sdkRepoName, commands), (error: unknown) => error === failure);
                const expectedCalls = {
                    "clone failure": 1,
                    "sparse-checkout failure": 2,
                    "npm failure": 3,
                    "generation failure": 4,
                };
                assert.equal(calls.length, expectedCalls[scenario]);
            }

        } finally {
            await rm(workspace, { recursive: true, force: true });
        }
    });
}

test("generateSdk rejects unsupported repositories before accessing the workspace", async () => {
    await assert.rejects(generateSdk("nonexistent-workspace", "../other-repo"), /Unsupported SDK repository/);
    await assert.rejects(generateSdk("nonexistent-workspace", "azure-sdk-for-rust"), /Unsupported SDK repository/);
});
