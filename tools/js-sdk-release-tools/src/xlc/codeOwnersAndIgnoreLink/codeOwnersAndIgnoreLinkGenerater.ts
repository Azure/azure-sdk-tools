import { getSDKType } from "../../common/utils.js";
import { ICodeOwnersAndIgnoreLinkGenerator } from "../../common/interfaces.js";
import { SDKType } from "../../common/types.js";
import { logger } from "../../utils/logger.js";
import * as mlcApi from "../../mlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";
import * as hlcApi from "../../hlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";

export const generateCodeOwnersAndIgnoreLink: ICodeOwnersAndIgnoreLinkGenerator =
    async (options: {
        sdkType: SDKType;
        typeSpecDirectory: string;
        sdkRepoRoot: string;
        skipGeneration: boolean;
    }): Promise<void> => {
        switch (options.sdkType) {
            case SDKType.ModularClient:
                return await mlcApi.generateCodeOwnersAndIgnoreLink({
                    typeSpecDirectory: options.typeSpecDirectory,
                    sdkRepoRoot: options.sdkRepoRoot,
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
