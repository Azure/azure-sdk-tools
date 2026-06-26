import { spawn } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { loadVariables } from "./variables.js";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const packageDirectory = resolve(scriptDirectory, "../..");
const cryptoUserRoleName = "Key Vault Crypto User";

async function main(): Promise<void> {
    const variables = await loadVariables();
    const githubAppKeyVaultName = getKeyVaultName(variables.githubAppKeyVaultUrl);
    const principalId = await getWebAppPrincipalId(variables.resourceGroupName, variables.webAppName, variables.subscriptionId);
    const isRbacEnabled = await isKeyVaultRbacEnabled(githubAppKeyVaultName, variables.subscriptionId);

    if (isRbacEnabled) {
        const keyId = await getKeyId(githubAppKeyVaultName, variables.githubAppKeyName, variables.subscriptionId);
        await grantRbacKeyAccess(principalId, keyId);
        console.log(`Granted ${cryptoUserRoleName} to ${variables.webAppName} on ${keyId}`);
        return;
    }

    await grantAccessPolicyKeyAccess(githubAppKeyVaultName, principalId, variables.subscriptionId);
    console.log(`Granted key get/sign access policy to ${variables.webAppName} on ${githubAppKeyVaultName}`);
}

async function getWebAppPrincipalId(resourceGroupName: string, webAppName: string, subscriptionId: string): Promise<string> {
    const principalId = await runAz([
        "webapp",
        "identity",
        "show",
        "--resource-group",
        resourceGroupName,
        "--name",
        webAppName,
        "--subscription",
        subscriptionId,
        "--query",
        "principalId",
        "-o",
        "tsv",
    ]);

    if (!principalId) {
        throw new Error(`Web app ${webAppName} does not have a system-assigned managed identity.`);
    }

    return principalId;
}

async function isKeyVaultRbacEnabled(keyVaultName: string, subscriptionId: string): Promise<boolean> {
    const value = await runAz([
        "keyvault",
        "show",
        "--name",
        keyVaultName,
        "--subscription",
        subscriptionId,
        "--query",
        "properties.enableRbacAuthorization",
        "-o",
        "tsv",
    ]);
    return value.toLowerCase() === "true";
}

async function getKeyId(keyVaultName: string, keyName: string, subscriptionId: string): Promise<string> {
    return runAz([
        "keyvault",
        "key",
        "show",
        "--vault-name",
        keyVaultName,
        "--name",
        keyName,
        "--subscription",
        subscriptionId,
        "--query",
        "id",
        "-o",
        "tsv",
    ]);
}

async function grantRbacKeyAccess(principalId: string, keyId: string): Promise<void> {
    await runAz([
        "role",
        "assignment",
        "create",
        "--assignee-object-id",
        principalId,
        "--assignee-principal-type",
        "ServicePrincipal",
        "--role",
        cryptoUserRoleName,
        "--scope",
        keyId,
    ], true);
}

async function grantAccessPolicyKeyAccess(keyVaultName: string, principalId: string, subscriptionId: string): Promise<void> {
    await runAz([
        "keyvault",
        "set-policy",
        "--name",
        keyVaultName,
        "--object-id",
        principalId,
        "--key-permissions",
        "get",
        "sign",
        "--subscription",
        subscriptionId,
    ]);
}

function getKeyVaultName(keyVaultUrl: string): string {
    const hostName = new URL(keyVaultUrl).hostname;
    const [keyVaultName] = hostName.split(".", 1);
    if (!keyVaultName) {
        throw new Error(`Unable to parse Key Vault name from URL: ${keyVaultUrl}`);
    }

    return keyVaultName;
}

async function runAz(args: readonly string[], ignoreExistingRoleAssignment = false): Promise<string> {
    const command = process.platform === "win32" ? "az.cmd" : "az";
    console.log(`Running command: az ${args.join(" ")}`);

    return new Promise<string>((resolvePromise, reject) => {
        let stdout = "";
        let stderr = "";
        const child = process.platform === "win32"
            ? spawn([command, ...args].map(quoteWindowsShellArgument).join(" "), {
                cwd: packageDirectory,
                shell: true,
            })
            : spawn(command, args, {
                cwd: packageDirectory,
                shell: false,
            });

        child.stdout.setEncoding("utf8");
        child.stderr.setEncoding("utf8");
        child.stdout.on("data", (data: string) => {
            stdout += data;
        });
        child.stderr.on("data", (data: string) => {
            stderr += data;
        });
        child.on("error", reject);
        child.on("exit", (code) => {
            if (code === 0) {
                resolvePromise(stdout.trim());
                return;
            }

            if (ignoreExistingRoleAssignment && stderr.includes("RoleAssignmentExists")) {
                console.log("Role assignment already exists.");
                resolvePromise(stdout.trim());
                return;
            }

            reject(new Error(`Command failed with exit code ${code}: az ${args.join(" ")}\n${stderr}`));
        });
    });
}

function quoteWindowsShellArgument(argument: string): string {
    return /[\s"]/.test(argument) ? `"${argument.replace(/"/g, '\\"')}"` : argument;
}

main().catch((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`Failed to grant GitHub App key access: ${message}`);
    process.exitCode = 1;
});