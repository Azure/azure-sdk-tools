#!/usr/bin/env node

import {generateChangelogAndBumpVersion} from "./changelogGenerationAndVersionBumpCore/automaticGenerateChangeLogAndBumpVersion";
import {logger} from "./utils/logger";

const generateChangelogAndBumpVersionCLI = async (packageFolderPath: string | undefined) => {
    if (!packageFolderPath) {
        logger.logError(`invalid package path ${packageFolderPath}`);
    } else {
        await generateChangelogAndBumpVersion(packageFolderPath);
    }
};

const packageFolderPath = process.argv.pop();

generateChangelogAndBumpVersionCLI(packageFolderPath);
