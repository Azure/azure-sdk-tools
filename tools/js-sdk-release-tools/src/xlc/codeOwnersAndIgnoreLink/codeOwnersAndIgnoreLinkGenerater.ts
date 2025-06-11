import { ICodeOwnersAndIgnoreLinkGenerator } from "../../common/interfaces.js";
import { SDKType } from "../../common/types.js";
import { logger } from "../../utils/logger.js";
import * as mlcApi from "../../mlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";
import * as hlcApi from "../../hlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";
import shell from 'shelljs';
import * as path from 'path';

export const generateCodeOwnersAndIgnoreLink: ICodeOwnersAndIgnoreLinkGenerator =
    async (options: {
        sdkType: SDKType;
        specFolder: string;
        typespecProject?: string;
        skipGeneration: boolean;
    }): Promise<void> => {
        switch (options.sdkType) {
            case SDKType.ModularClient:
                const typeSpecDirectory = path.posix.join(options.specFolder, options.typespecProject!);
                const sdkRepoRoot = String(shell.pwd()).replaceAll('\\', '/');
                return await mlcApi.generateCodeOwnersAndIgnoreLink({
                    typeSpecDirectory: typeSpecDirectory,
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
