#!/usr/bin/env node

import { generateChangelogFromApiReviewFiles } from "./common/changelog/automaticGenerateChangeLogAndBumpVersion.js";
import { SDKType } from "./common/types.js";
import { logger } from "./utils/logger.js";
import commandLineArgs from "command-line-args";

const compareApiReviewCli = async (
    oldApiMdPath: string | undefined,
    newApiMdPath: string | undefined,
    oldSDKType: string | undefined,
    newSDKType: string | undefined
) => {
    if (!oldApiMdPath || !newApiMdPath) {
        logger.error(`Both oldApiMdPath and newApiMdPath are required.`);
        logger.error(`Usage: compare-api-review --oldApiMd <path> --newApiMd <path> [--oldSDKType <type>] [--newSDKType <type>]`);
        return;
    }

    // Default to HighLevelClient if not specified
    const oldType = (oldSDKType as SDKType) || SDKType.HighLevelClient;
    const newType = (newSDKType as SDKType) || SDKType.HighLevelClient;

    logger.info(`Comparing API review files:`);
    logger.info(`  Old: ${oldApiMdPath} (${oldType})`);
    logger.info(`  New: ${newApiMdPath} (${newType})`);

    try {
        await generateChangelogFromApiReviewFiles(
            oldApiMdPath,
            newApiMdPath,
            oldType,
            newType
        );
    } catch (error) {
        logger.error(`Failed to generate changelog: ${error}`);
        process.exit(1);
    }
};

const optionDefinitions = [
    { name: "oldApiMd", type: String },
    { name: "newApiMd", type: String },
    { name: "oldSDKType", type: String },
    { name: "newSDKType", type: String },
];

const options = commandLineArgs(optionDefinitions);

compareApiReviewCli(
    options.oldApiMd,
    options.newApiMd,
    options.oldSDKType,
    options.newSDKType
);
