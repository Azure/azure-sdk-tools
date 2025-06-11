import { ICodeOwnersAndIgnoreLinkGenerator } from "../../common/interfaces.js";
import { SDKType } from "../../common/types.js";
import { logger } from "../../utils/logger.js";
import * as mlcApi from "../../mlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";
import * as hlcApi from "../../hlc/codeownersAndignorelink/codeOwnersAndIgnoreLinkGenerater.js";

export const generateCodeOwnersAndIgnoreLink: ICodeOwnersAndIgnoreLinkGenerator =
    async (options: { sdkType: SDKType; packages: any }): Promise<void> => {
        if (options.packages.length === 0) {
            logger.warn("No packages found in the packages");
            return;
        }
        switch (options.sdkType) {
            case SDKType.ModularClient:
                return await mlcApi.generateCodeOwnersAndIgnoreLink(
                    options.packages,
                );
            case SDKType.HighLevelClient:
                return await hlcApi.generateCodeOwnersAndIgnoreLink(
                    options.packages,
                );
            default:
                logger.warn(
                    `Unsupported SDK type ${options.sdkType} to generate CODEOWNERS and ignore link.`,
                );
                return;
        }
    };
