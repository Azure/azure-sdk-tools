import { getSDKType } from "../../common/utils.js";
import { ICodeOwnersAndIgnoreLinkGenerator } from "../../common/interfaces.js";
import { SDKType } from "../../common/types.js";
import { logger } from "../../utils/logger.js";
import * as mlcApi from "../../mlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";
import * as hlcApi from "../../hlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";

export const generateCodeOwnersAndIgnoreLink: ICodeOwnersAndIgnoreLinkGenerator =
    async (
        sdkType: SDKType,
        options: {
            typespecProject?: string;
            typeSpecDirectory: string;
            sdkRepo: string;
            skipGeneration: boolean;
        },
    ): Promise<void> => {
        switch (sdkType) {
            case SDKType.ModularClient:
                return await mlcApi.generateCodeOwnersAndIgnoreLink(options);
            case SDKType.HighLevelClient:
                return await hlcApi.generateCodeOwnersAndIgnoreLink(
                    options.skipGeneration,
                );
            default:
                logger.warn(
                    `Unsupported SDK type ${sdkType} to generate CODEOWNERS and ignore link.`,
                );
                return;
        }
    };
