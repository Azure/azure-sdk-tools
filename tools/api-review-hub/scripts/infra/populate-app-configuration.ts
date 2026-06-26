import { setTimeout } from "node:timers/promises";

import { AppConfigurationClient } from "@azure/app-configuration";
import { DefaultAzureCredential } from "@azure/identity";

import { type AppConfigurationSetting, loadVariables } from "./variables.js";

const credential = new DefaultAzureCredential();
const appConfigurationWriteRetryCount = 12;
const appConfigurationWriteRetryDelayMs = 10_000;

async function main(): Promise<void> {
    const variables = await loadVariables();
    const client = new AppConfigurationClient(variables.appConfigurationEndpoint, credential);

    await waitForAppConfigurationAccess(client, variables.appConfigurationName);

    console.log(`Writing App Configuration settings: ${variables.appConfigurationName}`);
    for (const setting of variables.appConfigurationSettings) {
        await setAppConfigurationSetting(client, setting);
    }

    console.log(`Wrote App Configuration settings: ${variables.appConfigurationName}`);
}

async function waitForAppConfigurationAccess(client: AppConfigurationClient, appConfigurationName: string): Promise<void> {
    const accessCheckKey = "__api_review_hub_access_check__";

    for (let attempt = 1; attempt <= appConfigurationWriteRetryCount; attempt++) {
        try {
            await client.setConfigurationSetting({
                key: accessCheckKey,
                value: new Date().toISOString(),
                contentType: "text/plain",
            });
            await client.deleteConfigurationSetting({ key: accessCheckKey });
            return;
        } catch (error) {
            if (!isAuthorizationPropagationError(error) || attempt === appConfigurationWriteRetryCount) {
                throw error;
            }

            console.log(
                `Waiting for App Configuration data-plane access to ${appConfigurationName} (${attempt}/${appConfigurationWriteRetryCount}): ${getErrorDescription(error)}`,
            );
            await setTimeout(appConfigurationWriteRetryDelayMs);
        }
    }
}

async function setAppConfigurationSetting(client: AppConfigurationClient, setting: AppConfigurationSetting): Promise<void> {
    for (let attempt = 1; attempt <= appConfigurationWriteRetryCount; attempt++) {
        try {
            await client.setConfigurationSetting({
                key: setting.key,
                value: setting.value,
                contentType: "text/plain",
            });
            return;
        } catch (error) {
            if (!isAuthorizationPropagationError(error) || attempt === appConfigurationWriteRetryCount) {
                throw error;
            }
            await setTimeout(appConfigurationWriteRetryDelayMs);
        }
    }
}

function isAuthorizationPropagationError(error: unknown): boolean {
    return typeof error === "object" && error !== null && "statusCode" in error && (error.statusCode === 401 || error.statusCode === 403);
}

function getErrorDescription(error: unknown): string {
    if (typeof error !== "object" || error === null) {
        return String(error);
    }

    const details: string[] = [];
    if ("statusCode" in error) {
        details.push(`status ${String(error.statusCode)}`);
    }
    if ("code" in error) {
        details.push(`code ${String(error.code)}`);
    }
    if ("message" in error) {
        details.push(String(error.message).split("\n", 1)[0]);
    }

    return details.join(", ");
}

main().catch((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`Failed to populate API Review Hub App Configuration: ${message}`);
    process.exitCode = 1;
});