import { spawn } from "node:child_process";
import { existsSync, rmSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import AdmZip from "adm-zip";

import { loadVariables } from "./infra/variables.js";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const packageDirectory = resolve(scriptDirectory, "..");
const artifactPath = resolve(packageDirectory, "api-review-hub.zip");
const startupCommand = "npm start";

async function main(): Promise<void> {
    const variables = await loadVariables();

    console.log(`Deploying API Review Hub to ${variables.webAppName} in ${variables.resourceGroupName}`);
    await runCommand(getNpmExecutable(), ["run", "build"], packageDirectory);
    createDeploymentZip();

    try {
        await deployZip(variables.resourceGroupName, variables.webAppName, variables.subscriptionId);
        await setStartupCommand(variables.resourceGroupName, variables.webAppName, variables.subscriptionId);
        console.log(`Deployed API Review Hub to ${variables.webAppEndpoint}`);
    } finally {
        if (existsSync(artifactPath)) {
            rmSync(artifactPath);
            console.log(`Removed temporary deployment package: ${artifactPath}`);
        }
    }
}

function createDeploymentZip(): void {
    console.log("Creating deployment package...");
    if (existsSync(artifactPath)) {
        rmSync(artifactPath);
    }

    const zip = new AdmZip();
    zip.addLocalFolder(resolve(packageDirectory, "dist"), "dist");
    zip.addLocalFolder(resolve(packageDirectory, "src"), "src");
    zip.addLocalFile(resolve(packageDirectory, "package.json"));
    zip.addLocalFile(resolve(packageDirectory, "package-lock.json"));
    zip.addLocalFile(resolve(packageDirectory, "tsconfig.json"));
    zip.writeZip(artifactPath);
    console.log(`Created deployment package: ${artifactPath}`);
}

async function deployZip(resourceGroupName: string, webAppName: string, subscriptionId: string): Promise<void> {
    console.log("Deploying package to Azure App Service...");
    await runCommand(getAzureCliExecutable(), [
        "webapp",
        "deploy",
        "--resource-group",
        resourceGroupName,
        "--name",
        webAppName,
        "--src-path",
        artifactPath,
        "--subscription",
        subscriptionId,
        "--type",
        "zip",
    ]);
}

async function setStartupCommand(resourceGroupName: string, webAppName: string, subscriptionId: string): Promise<void> {
    console.log(`Setting Azure App Service startup command to: ${startupCommand}`);
    await runCommand(getAzureCliExecutable(), [
        "webapp",
        "config",
        "set",
        "--resource-group",
        resourceGroupName,
        "--name",
        webAppName,
        "--startup-file",
        startupCommand,
        "--subscription",
        subscriptionId,
    ]);
}

function getAzureCliExecutable(): string {
    return process.platform === "win32" ? "az.cmd" : "az";
}

function getNpmExecutable(): string {
    return process.platform === "win32" ? "npm.cmd" : "npm";
}

async function runCommand(command: string, args: readonly string[], cwd = packageDirectory): Promise<void> {
    console.log(`Running command: ${command} ${args.join(" ")}`);

    await new Promise<void>((resolvePromise, reject) => {
        const child = process.platform === "win32"
            ? spawn([command, ...args].map(quoteWindowsShellArgument).join(" "), {
                cwd,
                stdio: "inherit",
                shell: true,
            })
            : spawn(command, args, {
                cwd,
                shell: false,
            });

        child.on("error", reject);
        child.on("exit", (code) => {
            if (code === 0) {
                resolvePromise();
                return;
            }

            reject(new Error(`Command failed with exit code ${code}: ${command} ${args.join(" ")}`));
        });
    });
}

function quoteWindowsShellArgument(argument: string): string {
    return /[\s"]/.test(argument) ? `"${argument.replace(/"/g, '\\"')}"` : argument;
}

main().catch((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`Failed to deploy API Review Hub: ${message}`);
    process.exitCode = 1;
});