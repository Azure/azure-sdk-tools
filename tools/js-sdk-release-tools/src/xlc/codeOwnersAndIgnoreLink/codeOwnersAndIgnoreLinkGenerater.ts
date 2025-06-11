import { ICodeOwnersAndIgnoreLinkGenerator } from "../../common/interfaces.js";
import { SDKType } from "../../common/types.js";
import { logger } from "../../utils/logger.js";
import * as mlcApi from "../../mlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";
import * as hlcApi from "../../hlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";
import shell from 'shelljs';

export const generateCodeOwnersAndIgnoreLink: ICodeOwnersAndIgnoreLinkGenerator =
    async (options: {
        sdkType: SDKType;
        typeSpecDirectory: string;
        skipGeneration: boolean;
    }): Promise<void> => {
        switch (options.sdkType) {
            case SDKType.ModularClient:
                const sdkRepoRoot = String(shell.pwd()).replaceAll('\\', '/')
                return await mlcApi.generateCodeOwnersAndIgnoreLink({
                    typeSpecDirectory: options.typeSpecDirectory,
                    sdkRepoRoot: sdkRepoRoot,
                });
            case SDKType.HighLevelClient:
                return await hlcApi.generateCodeOwnersAndIgnoreLink(
                    options.skipGeneration,
                );
            default:
                logger.warn(
                    `Unsupported SDK type ${options.sdkType} to generate CODEOWNERS and ignore link.`,
                );
                return;
        }
    };
