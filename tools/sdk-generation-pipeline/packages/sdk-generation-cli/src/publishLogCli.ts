#!/usr/bin/env node
import {PublishLogCliConfig, publishLogCliConfig} from "./cliSchema/publishLogCliConfig";
import * as fs from "fs";
import {logger, requireJsonc, AzureBlobClient} from "@azure-tools/sdk-generation-lib";
import {SdkGenerationServerClient} from "./utils/SdkGenerationServerClient";

export async function main() {
    const config: PublishLogCliConfig = publishLogCliConfig.getProperties();
    const azureBlobClient = new AzureBlobClient(config.azureStorageBlobSasUrl, config.azureBlobContainerName);
    const sdkGenerationServerClient = new SdkGenerationServerClient(config.sdkGenerationServiceHost, config.certPath, config.keyPath);
    if (fs.existsSync(config.taskFullLog)) {
        await azureBlobClient.publishBlob(config.taskFullLog, `${config.buildId}/log/${config.sdkGenerationName}-${config.taskName}.full.log`);
    }
    if (fs.existsSync(config.pipeLog)) {
        await azureBlobClient.publishBlob(config.pipeLog, `${config.buildId}/log/${config.sdkGenerationName}-${config.taskName}.log`);
        await sdkGenerationServerClient.publishTaskResult(config.sdkGenerationName, config.buildId, requireJsonc(config.pipeLog));
    }
    if (fs.existsSync(config.mockServerLog)) {
        await azureBlobClient.publishBlob(config.pipeFullLog, `${config.buildId}/log/${config.sdkGenerationName}-${config.taskName}.mockserver.log`);
    }
}

main().catch(e => {
    logger.error(`${e.message}
    ${e.stack}`);
    process.exit(1);
});
