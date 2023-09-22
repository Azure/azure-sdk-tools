#!/usr/bin/env node

import {generateChangelogAndBumpVersion} from "./hlc/utils/automaticGenerateChangeLogAndBumpVersion";
import {logger} from "./utils/logger";

const changelogToolCli = async (packageFolderPath: string | undefined) => {
    if (!packageFolderPath) {
        logger.logError(`invalid package path ${packageFolderPath}`);
    } else {
        await generateChangelogAndBumpVersion(packageFolderPath);
    }
};

const packageFolderPath = process.argv.pop();

changelogToolCli(packageFolderPath);
